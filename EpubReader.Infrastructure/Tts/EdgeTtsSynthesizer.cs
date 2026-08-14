using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EpubReader.Infrastructure.Tts;

internal sealed class EdgeTtsSynthesizer
{
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string SecMsGecVersion = "1-143.0.3650.96";
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public async Task<byte[]?> SynthesizeAsync(
        string text,
        string language,
        double rate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var voice = ResolveVoice(language);
        var ratePercent = RateToPercent(rate);
        var requestId = Guid.NewGuid().ToString("N");
        var ssml = BuildSsml(text, voice, language, ratePercent);

        var urls = BuildUrls(requestId);
        Exception? last = null;
        foreach (var url in urls)
        {
            try
            {
                return await SynthesizeWithUrlAsync(url, requestId, ssml, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("Edge TTS niedostępne");
    }

    private static async Task<byte[]> SynthesizeWithUrlAsync(
        string url,
        string requestId,
        string ssml,
        CancellationToken cancellationToken)
    {
        using var ws = new ClientWebSocket();
        TrySetHeader(ws, "Pragma", "no-cache");
        TrySetHeader(ws, "Cache-Control", "no-cache");
        TrySetHeader(ws, "Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        TrySetHeader(ws, "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        await ws.ConnectAsync(new Uri(url), timeout.Token);

        var config =
            "Content-Type:application/json; charset=utf-8\r\n" +
            "Path:speech.config\r\n\r\n" +
            "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{" +
            "\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}," +
            "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}\r\n";

        await ws.SendAsync(Utf8.GetBytes(config), WebSocketMessageType.Text, true, timeout.Token);

        var ssmlMsg =
            $"X-RequestId:{requestId}\r\n" +
            "Content-Type:application/ssml+xml\r\n" +
            $"X-Timestamp:{DateTime.UtcNow:yyy-MM-ddTHH:mm:ss.fffZ}Z\r\n" +
            "Path:ssml\r\n\r\n" +
            ssml;

        await ws.SendAsync(Utf8.GetBytes(ssmlMsg), WebSocketMessageType.Text, true, timeout.Token);

        using var audio = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (ws.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    break;
                }

                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var payload = message.ToArray();
            if (result.MessageType == WebSocketMessageType.Binary)
            {
                AppendAudioBinary(payload, audio);
            }
            else
            {
                var textMsg = Utf8.GetString(payload);
                if (textMsg.Contains("Path:turn.end", StringComparison.Ordinal))
                    break;
            }
        }

        if (audio.Length == 0)
            throw new InvalidOperationException("Edge TTS: pusty strumień audio");

        return audio.ToArray();
    }

    private static void AppendAudioBinary(byte[] payload, MemoryStream audio)
    {
        // Header is 2-byte big-endian length + UTF-8 header ending with "Path:audio\r\n"
        if (payload.Length < 2) return;
        var headerLen = (payload[0] << 8) | payload[1];
        var dataStart = 2 + headerLen;
        if (dataStart > payload.Length) return;
        audio.Write(payload, dataStart, payload.Length - dataStart);
    }

    private static IEnumerable<string> BuildUrls(string connectionId)
    {
        var gec = GenerateSecMsGec();
        yield return
            "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
            $"?TrustedClientToken={TrustedClientToken}" +
            $"&Sec-MS-GEC={gec}" +
            $"&Sec-MS-GEC-Version={SecMsGecVersion}" +
            $"&ConnectionId={connectionId}";

        yield return
            "wss://api.msedgeservices.com/tts/cognitiveservices/websocket/v1" +
            $"?Ocp-Apim-Subscription-Key={TrustedClientToken}" +
            $"&Sec-MS-GEC={gec}" +
            $"&Sec-MS-GEC-Version={SecMsGecVersion}" +
            $"&ConnectionId={connectionId}";
    }

    internal static string GenerateSecMsGec()
    {
        // See: https://github.com/rany2/edge-tts/issues/290
        var ticks = DateTime.UtcNow.ToFileTimeUtc();
        ticks -= ticks % 3_000_000_000L;
        var str = ticks.ToString() + TrustedClientToken;
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(str));
        return Convert.ToHexString(hash);
    }

    private static void TrySetHeader(ClientWebSocket ws, string name, string value)
    {
        try { ws.Options.SetRequestHeader(name, value); }
        catch { /* WASM / browser forbids some headers */ }
    }

    private static string BuildSsml(string text, string voice, string language, string ratePercent)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "en-US" : language;
        if (lang.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)) lang = "zh-CN";
        else if (lang.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)) lang = "zh-TW";
        else if (lang.Length == 2) lang = lang.ToLowerInvariant() switch
        {
            "pl" => "pl-PL",
            "en" => "en-US",
            "de" => "de-DE",
            "fr" => "fr-FR",
            "es" => "es-ES",
            "it" => "it-IT",
            "ja" => "ja-JP",
            "ko" => "ko-KR",
            "ru" => "ru-RU",
            "ar" => "ar-SA",
            "zh" => "zh-CN",
            _ => lang + "-" + lang.ToUpperInvariant()
        };

        var escaped = new XText(SanitizeSsmlText(text)).ToString();
        return
            "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='" + lang + "'>" +
            "<voice name='" + voice + "'>" +
            "<prosody pitch='+0Hz' rate='" + ratePercent + "' volume='+0%'>" +
            escaped +
            "</prosody></voice></speak>";
    }

    private static string SanitizeSsmlText(string text)
    {
        // Keep SSML plain (no <break> tags — those broke Edge after a recent change).
        var s = (text ?? string.Empty).Replace("*", string.Empty);
        s = Regex.Replace(s, @"\r\n?", "\n");
        s = Regex.Replace(s, @"\n\s*\n+", " … "); // paragraph pause via ellipsis, not SSML
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return Regex.Replace(s, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", " ");
    }

    private static string RateToPercent(double rate)
    {
        // 1.0 → +0%, 1.4 → +40%, 0.7 → -30%
        var pct = (int)Math.Round((rate - 1.0) * 100);
        pct = Math.Clamp(pct, -50, 100);
        return pct >= 0 ? $"+{pct}%" : $"{pct}%";
    }

    internal static string ResolveVoice(string? language)
    {
        var lang = (language ?? "en").Trim();
        if (lang.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
            || lang.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
            || lang.Equals("zh", StringComparison.OrdinalIgnoreCase))
            return "zh-CN-XiaoxiaoNeural";
        if (lang.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
            || lang.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase))
            return "zh-TW-HsiaoChenNeural";
        if (lang.Equals("zh-HK", StringComparison.OrdinalIgnoreCase))
            return "zh-HK-HiuMaanNeural";

        var primary = lang.Split('-', 2)[0].ToLowerInvariant();
        return primary switch
        {
            "pl" => "pl-PL-ZofiaNeural",
            "en" => "en-US-JennyNeural",
            "de" => "de-DE-KatjaNeural",
            "fr" => "fr-FR-DeniseNeural",
            "es" => "es-ES-ElviraNeural",
            "it" => "it-IT-ElsaNeural",
            "ja" => "ja-JP-NanamiNeural",
            "ko" => "ko-KR-SunHiNeural",
            "ru" => "ru-RU-SvetlanaNeural",
            "ar" => "ar-SA-ZariyahNeural",
            "pt" => "pt-BR-FranciscaNeural",
            "nl" => "nl-NL-FennaNeural",
            "sv" => "sv-SE-SofieNeural",
            "cs" => "cs-CZ-VlastaNeural",
            "uk" => "uk-UA-PolinaNeural",
            _ => "en-US-JennyNeural"
        };
    }
}
