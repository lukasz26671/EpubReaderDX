using EpubReader.Application.Interfaces;
using EpubReader.Domain.Enums;
using EpubReader.Infrastructure.Tts;
using Microsoft.JSInterop;

namespace EpubReaderDX.Services.Tts;

/// <summary>
/// Offline Piper neural TTS (English). Windows (CLI) + Android (sherpa-onnx).
/// </summary>
public sealed class PiperTtsEngine : ITtsEngine, IPiperVoiceService
{
    private readonly IJsRuntimeAccessor _js;
    private readonly IReaderStateService _state;
    private readonly PiperRuntime _runtime = new();
    private Task? _ensureTask;

    public PiperTtsEngine(IJsRuntimeAccessor js, IReaderStateService state)
    {
        _js = js;
        _state = state;
        _runtime.OnChange += () => OnChange?.Invoke();
        SyncVoiceFromSettings();
        _state.OnChange += SyncVoiceFromSettings;
    }

    public TtsEngineKind Kind => TtsEngineKind.Piper;
    public string DisplayName => "Piper (EN)";

    public string SelectedVoiceId => _runtime.ActiveVoiceId;
    public PiperPrepPhase Phase => _runtime.Phase;
    public string? StatusMessage => _runtime.StatusMessage;
    public double? Progress => _runtime.Progress;
    public event Action? OnChange;

    public IReadOnlyList<PiperVoiceInfo> GetVoices() =>
        PiperRuntime.Catalog
            .Select(v => new PiperVoiceInfo(
                v.Id,
                v.DisplayName,
                $"{v.Detail} · {v.SizeHint}",
                _runtime.IsVoiceReady(v.Id)))
            .ToList();

    public async Task SelectVoiceAsync(string voiceId, CancellationToken cancellationToken = default)
    {
        var voice = PiperRuntime.ResolveVoice(voiceId);
        _runtime.SetActiveVoice(voice.Id);

        var s = CloneSettings();
        s.PiperVoiceId = voice.Id;
        await _state.UpdateSettingsAsync(s, cancellationToken);

        // Kick off download in background so the settings panel shows progress.
        _ = EnsureReadyAsync(cancellationToken);
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        SyncVoiceFromSettings();
        try
        {
            await _runtime.EnsureReadyAsync(cancellationToken);
        }
        finally
        {
            OnChange?.Invoke();
        }
    }

    public bool IsVoiceReady(string voiceId) => _runtime.IsVoiceReady(voiceId);

    public async Task<bool> IsAvailableAsync(string language, CancellationToken cancellationToken = default)
    {
        if (_js.Current is null) return false;
        if (!PiperRuntime.IsSupportedPlatform()) return false;

        SyncVoiceFromSettings();
        if (_runtime.LooksReady()) return true;

        _ensureTask ??= Task.Run(async () =>
        {
            try { await _runtime.EnsureReadyAsync(CancellationToken.None); }
            catch { /* surfaced on Speak / status */ }
            finally { OnChange?.Invoke(); }
        }, CancellationToken.None);

        return _runtime.LooksReady();
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

        SyncVoiceFromSettings();
        await _runtime.EnsureReadyAsync(cancellationToken);

        var currentTask = _runtime.SynthesizeWavAsync(chunks[0], rate, cancellationToken);
        Task<byte[]>? nextTask = chunks.Count > 1
            ? _runtime.SynthesizeWavAsync(chunks[1], rate, cancellationToken)
            : null;

        for (var i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (onChunkStarted is not null)
                await onChunkStarted(i, cancellationToken);

            byte[] wav;
            try
            {
                wav = await currentTask;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Piper: {ex.Message}", ex);
            }

            if (wav.Length == 0)
                throw new InvalidOperationException("Piper: empty audio");

            currentTask = nextTask ?? Task.FromResult(Array.Empty<byte>());
            nextTask = i + 2 < chunks.Count
                ? _runtime.SynthesizeWavAsync(chunks[i + 2], rate, cancellationToken)
                : null;

            var runtime = JsRuntimeGuard.Require(_js);
            var b64 = Convert.ToBase64String(wav);
            await using var reg = cancellationToken.Register(() => _ = StopAsync());
            try
            {
                await runtime.InvokeVoidAsync("epubReaderTts.playWavBase64", cancellationToken, b64);
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

    private void SyncVoiceFromSettings()
    {
        var id = string.IsNullOrWhiteSpace(_state.Settings.PiperVoiceId)
            ? PiperRuntime.DefaultVoiceId
            : _state.Settings.PiperVoiceId;
        _runtime.SetActiveVoice(id);
    }

    private EpubReader.Domain.Entities.ReaderSettings CloneSettings()
    {
        var cur = _state.Settings;
        return new()
        {
            Theme = cur.Theme,
            FontFamily = cur.FontFamily,
            FontSize = cur.FontSize,
            LineHeight = cur.LineHeight,
            LetterSpacing = cur.LetterSpacing,
            ParagraphSpacing = cur.ParagraphSpacing,
            TextAlign = cur.TextAlign,
            ContentWidth = cur.ContentWidth,
            PageMargin = cur.PageMargin,
            TtsEngine = cur.TtsEngine,
            TtsRate = cur.TtsRate,
            TtsLanguageOverride = cur.TtsLanguageOverride,
            PiperVoiceId = cur.PiperVoiceId,
            UiLanguage = cur.UiLanguage
        };
    }
}
