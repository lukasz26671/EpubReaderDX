using EpubReader.Application.Interfaces;
using EpubReader.Domain.Enums;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Tts;

public sealed class EdgeNeuralTtsEngine : ITtsEngine
{
    private readonly IJsRuntimeAccessor _js;
    private readonly EdgeTtsSynthesizer _synthesizer = new();

    public EdgeNeuralTtsEngine(IJsRuntimeAccessor js) => _js = js;

    public TtsEngineKind Kind => TtsEngineKind.EdgeNeural;
    public string DisplayName => "Edge neural";

    public Task<bool> IsAvailableAsync(string language, CancellationToken cancellationToken = default)
    {
        // Optimistic: real connectivity is probed on Speak. Never permanently lock out Edge
        // after a single failure (that was forcing silent system TTS).
        return Task.FromResult(_js.Current is not null);
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

        // Real Edge neural only — do NOT fall back to speechSynthesis here.
        // Silent fallback made users hear system voices while thinking Edge was active.
        await SpeakEdgePrefetchAsync(chunks, language, rate, onChunkStarted, cancellationToken);
    }

    private async Task SpeakEdgePrefetchAsync(
        IReadOnlyList<string> chunks,
        string language,
        double rate,
        Func<int, CancellationToken, Task>? onChunkStarted,
        CancellationToken cancellationToken)
    {
        // Speculative: always synthesize one chunk ahead while current audio plays.
        var currentTask = _synthesizer.SynthesizeAsync(chunks[0], language, rate, cancellationToken);
        Task<byte[]?>? nextTask = chunks.Count > 1
            ? _synthesizer.SynthesizeAsync(chunks[1], language, rate, cancellationToken)
            : null;

        for (var i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (onChunkStarted is not null)
                await onChunkStarted(i, cancellationToken);

            byte[]? mp3;
            try
            {
                mp3 = await currentTask;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Edge neural: {ex.Message}", ex);
            }

            if (mp3 is not { Length: > 0 })
                throw new InvalidOperationException("Edge neural: empty audio");

            currentTask = nextTask ?? Task.FromResult<byte[]?>(null);
            nextTask = i + 2 < chunks.Count
                ? _synthesizer.SynthesizeAsync(chunks[i + 2], language, rate, cancellationToken)
                : null;

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
        }
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
