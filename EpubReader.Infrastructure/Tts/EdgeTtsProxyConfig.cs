namespace EpubReader.Infrastructure.Tts;

/// <summary>
/// Always-on Python Edge TTS proxy used only from the published GitHub Pages origin.
/// No API token in the browser — the proxy allows speak solely via CORS Origin check.
/// </summary>
internal static class EdgeTtsProxyConfig
{
    /// <summary>HTTPS base of tools/edge-tts-proxy on the DuckDNS host.</summary>
    public const string BaseUrl = "https://lukasz26671.duckdns.org:9443";

    public const string HealthPath = "/health";
    public const string SpeakPath = "/v1/speak";

    /// <summary>Only this host uses the proxy; other web hosts use direct Edge WebSocket.</summary>
    public const string GitHubPagesHost = "lukasz26671.github.io";

    public const string GitHubPagesOrigin = "https://lukasz26671.github.io";
}
