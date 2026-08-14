using EpubReader.Application.Interfaces;
using EpubReader.Domain.Enums;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Tts;

public sealed class SystemTtsEngine : ITtsEngine
{
    private readonly IJsRuntimeAccessor _js;

    public SystemTtsEngine(IJsRuntimeAccessor js) => _js = js;

    public TtsEngineKind Kind => TtsEngineKind.System;
    public string DisplayName => "System";

    public async Task<bool> IsAvailableAsync(string language, CancellationToken cancellationToken = default)
    {
        var runtime = _js.Current;
        if (runtime is null) return false;
        try
        {
            return await runtime.InvokeAsync<bool>("epubReaderTts.hasVoiceFor", cancellationToken, language ?? "en", false);
        }
        catch
        {
            return false;
        }
    }

    public async Task SpeakAsync(string text, string language, double rate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = JsRuntimeGuard.Require(_js);
        await using var reg = cancellationToken.Register(() => _ = StopAsync());
        await runtime.InvokeVoidAsync("epubReaderTts.speak", cancellationToken, text, language ?? "en", rate, false);
    }

    public async Task SpeakQueueAsync(
        IReadOnlyList<string> chunks,
        string language,
        double rate,
        Func<int, CancellationToken, Task>? onChunkStarted,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0) return;
        cancellationToken.ThrowIfCancellationRequested();
        var runtime = JsRuntimeGuard.Require(_js);

        using var bridge = DotNetObjectReference.Create(
            new TtsJsProgressBridge(index =>
                onChunkStarted?.Invoke(index, cancellationToken) ?? Task.CompletedTask));

        await using var reg = cancellationToken.Register(() => _ = StopAsync());
        try
        {
            await runtime.InvokeVoidAsync(
                "epubReaderTts.speakQueue",
                cancellationToken,
                chunks.ToArray(),
                language ?? "en",
                rate,
                false,
                bridge);
        }
        catch (OperationCanceledException)
        {
            try { await StopAsync(); } catch { /* ignore */ }
            throw;
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
