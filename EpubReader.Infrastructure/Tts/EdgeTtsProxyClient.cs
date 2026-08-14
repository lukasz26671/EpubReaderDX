using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace EpubReader.Infrastructure.Tts;

/// <summary>
/// Calls the Python Edge TTS proxy (browser-safe HTTPS). No API token —
/// the proxy accepts speak only from lukasz26671.github.io (Origin).
/// </summary>
internal sealed class EdgeTtsProxyClient
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "audio/mpeg");
        return http;
    }

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var res = await Http.GetAsync(
                EdgeTtsProxyConfig.BaseUrl.TrimEnd('/') + EdgeTtsProxyConfig.HealthPath,
                cancellationToken);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<byte[]> SynthesizeAsync(
        string text,
        string language,
        double rate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var url = EdgeTtsProxyConfig.BaseUrl.TrimEnd('/') + EdgeTtsProxyConfig.SpeakPath;
        using var res = await Http.PostAsJsonAsync(
            url,
            new SpeakBody(text, language, rate),
            cancellationToken);

        if (!res.IsSuccessStatusCode)
        {
            var detail = await res.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Edge proxy HTTP {(int)res.StatusCode}: {Truncate(detail, 240)}");
        }

        var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
            throw new InvalidOperationException("Edge proxy returned empty audio");
        return bytes;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

    private sealed record SpeakBody(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("rate")] double Rate);
}
