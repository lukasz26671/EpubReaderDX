using EpubReader.Application.Interfaces;
using EpubReader.Domain.Enums;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Tts;

public sealed class EdgeNeuralTtsEngine : ITtsEngine
{
    private readonly IJsRuntimeAccessor _js;
    private readonly EdgeTtsSynthesizer _direct = new();
    private readonly EdgeTtsProxyClient _proxy = new();
    private bool? _useProxy;

    public EdgeNeuralTtsEngine(IJsRuntimeAccessor js) => _js = js;

    public TtsEngineKind Kind => TtsEngineKind.EdgeNeural;
    public string DisplayName => "Edge neural";

    public async Task<bool> IsAvailableAsync(string language, CancellationToken cancellationToken = default)
    {
        if (_js.Current is null) return false;
        if (!await ShouldUseProxyAsync(cancellationToken))
            return true; // direct Edge (MAUI / non-GH-Pages web)

        return await _proxy.IsReachableAsync(cancellationToken);
    }

    public Task SpeakAsync(string text, string language, double rate, CancellationToken cancellationToken = default) =>
        SpeakQueueAsync([text], language, rate, null, cancellationToken);

    public async Task SpeakQueueAsync(
        IReadOnlyList<string> chunks,
        string language,
        double rate,
        Func<int, CancellationToken, Task>? onChunkStarted,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0) return;
        cancellationToken.ThrowIfCancellationRequested();

        await SpeakEdgePrefetchAsync(chunks, language, rate, onChunkStarted, cancellationToken);
    }

    private async Task SpeakEdgePrefetchAsync(
        IReadOnlyList<string> chunks,
        string language,
        double rate,
        Func<int, CancellationToken, Task>? onChunkStarted,
        CancellationToken cancellationToken)
    {
        var useProxy = await ShouldUseProxyAsync(cancellationToken);

        var currentTask = SynthAsync(useProxy, chunks[0], language, rate, cancellationToken);
        Task<byte[]>? nextTask = chunks.Count > 1
            ? SynthAsync(useProxy, chunks[1], language, rate, cancellationToken)
            : null;

        for (var i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (onChunkStarted is not null)
                await onChunkStarted(i, cancellationToken);

            byte[] mp3;
            try
            {
                mp3 = await currentTask;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Edge neural: {ex.Message}", ex);
            }

            if (mp3.Length == 0)
                throw new InvalidOperationException("Edge neural: empty audio");

            currentTask = nextTask ?? Task.FromResult(Array.Empty<byte>());
            nextTask = i + 2 < chunks.Count
                ? SynthAsync(useProxy, chunks[i + 2], language, rate, cancellationToken)
                : null;

            cancellationToken.ThrowIfCancellationRequested();

            var runtime = JsRuntimeGuard.Require(_js);
            var b64 = Convert.ToBase64String(mp3);
            await using var reg = cancellationToken.Register(() => _ = StopAsync());
            try
            {
                await runtime.InvokeVoidAsync("epubReaderTts.playMp3Base64", cancellationToken, b64);
            }
            catch (OperationCanceledException)
            {
                try { await StopAsync(); } catch { /* ignore */ }
                throw;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                try { await StopAsync(); } catch { /* ignore */ }
                cancellationToken.ThrowIfCancellationRequested();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private Task<byte[]> SynthAsync(bool useProxy, string text, string language, double rate, CancellationToken ct) =>
        useProxy
            ? _proxy.SynthesizeAsync(text, language, rate, ct)
            : SynthDirectAsync(text, language, rate, ct);

    private async Task<byte[]> SynthDirectAsync(string text, string language, double rate, CancellationToken ct)
    {
        var bytes = await _direct.SynthesizeAsync(text, language, rate, ct);
        return bytes ?? [];
    }

    /// <summary>
    /// Proxy only on lukasz26671.github.io. Elsewhere on web → direct Edge; MAUI → direct Edge.
    /// </summary>
    private async Task<bool> ShouldUseProxyAsync(CancellationToken cancellationToken)
    {
        if (_useProxy is bool cached)
            return cached;

        if (!OperatingSystem.IsBrowser())
        {
            _useProxy = false;
            return false;
        }

        var runtime = _js.Current;
        if (runtime is null)
        {
            _useProxy = false;
            return false;
        }

        try
        {
            var host = await runtime.InvokeAsync<string>(
                "epubReaderTts.getHostname",
                cancellationToken);
            _useProxy = string.Equals(
                host?.Trim(),
                EdgeTtsProxyConfig.GitHubPagesHost,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            _useProxy = false;
        }

        return _useProxy.Value;
    }

    public Task PauseAsync()
    {
        var runtime = _js.Current;
        return runtime is null ? Task.CompletedTask : runtime.InvokeVoidAsync("epubReaderTts.pause").AsTask();
    }

    public Task ResumeAsync()
    {
        var runtime = _js.Current;
        return runtime is null ? Task.CompletedTask : runtime.InvokeVoidAsync("epubReaderTts.resume").AsTask();
    }

    public Task StopAsync()
    {
        var runtime = _js.Current;
        return runtime is null ? Task.CompletedTask : runtime.InvokeVoidAsync("epubReaderTts.stop").AsTask();
    }
}
