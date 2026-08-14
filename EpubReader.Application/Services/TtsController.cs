using EpubReader.Application.Helpers;
using EpubReader.Application.Interfaces;
using EpubReader.Domain.Enums;
using Microsoft.JSInterop;

namespace EpubReader.Application.Services;

public sealed class TtsController : ITtsController
{
    private readonly ILanguageDetector _languageDetector;
    private readonly IAppLocalizer _l;
    private readonly IJsRuntimeAccessor _js;
    private readonly IReadOnlyDictionary<TtsEngineKind, ITtsEngine> _engines;
    private readonly IPiperVoiceService? _piper;
    private CancellationTokenSource? _playCts;
    private CancellationTokenSource? _queueCts;
    private readonly object _gate = new();
    private int _nav; // 0 = none, 1 = skip forward, -1 = skip back, 2 = restart current (rate change)
    private double _rate = 1.0;

    private bool _edgeFellBack;

    public TtsController(
        ILanguageDetector languageDetector,
        IAppLocalizer localizer,
        IJsRuntimeAccessor js,
        IEnumerable<ITtsEngine> engines,
        IEnumerable<IPiperVoiceService> piperVoices)
    {
        _languageDetector = languageDetector;
        _l = localizer;
        _js = js;
        _engines = engines.ToDictionary(e => e.Kind);
        _piper = piperVoices.FirstOrDefault();
        if (_piper is not null)
            _piper.OnChange += HandlePiperStatus;
    }

    private void HandlePiperStatus()
    {
        if (!IsSpeaking && string.IsNullOrEmpty(StatusMessage)) return;
        if (_piper is null) return;
        if (_piper.Phase is PiperPrepPhase.DownloadingRuntime or PiperPrepPhase.DownloadingVoice or PiperPrepPhase.Extracting)
        {
            StatusMessage = _piper.StatusMessage ?? _l.T("tts.piperPreparing");
            Notify();
        }
    }

    public bool IsSpeaking { get; private set; }
    public bool IsPaused { get; private set; }
    public string? DetectedLanguage { get; private set; }
    public TtsEngineKind? ActiveEngine { get; private set; }
    public string? StatusMessage { get; private set; }
    public int CurrentChunkIndex { get; private set; }
    public int ChunkCount { get; private set; }
    public string? CurrentHighlightText { get; private set; }
    public double Rate => _rate;

    public event Action? OnChange;

    public async Task PlayChapterAsync(
        string plainText,
        string? metadataLanguage,
        TtsEngineKind preferredEngine,
        double rate,
        string? languageOverride,
        int startChunkIndex = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            StatusMessage = _l.T("tts.noText");
            Notify();
            return;
        }

        await StopAsync();

        var lang = string.IsNullOrWhiteSpace(languageOverride)
            ? _languageDetector.Detect(metadataLanguage, plainText)
            : languageOverride.Trim();

        // Piper is English-only — do not overwrite the saved language preference.
        if (preferredEngine == TtsEngineKind.Piper)
            lang = "en";

        DetectedLanguage = lang;

        _rate = Math.Clamp(rate, 0.5, 2.0);
        // Same packs as the GitHub build: Edge ~900, Piper ~700, System ~240; flush on paragraphs.
        var (chunkSize, breakOnParagraph) = ChunkParams(preferredEngine);
        var chunks = TtsTextChunker.Chunk(plainText, chunkSize, breakOnParagraph);
        if (chunks.Count == 0)
        {
            StatusMessage = _l.T("tts.noText");
            Notify();
            return;
        }

        var (engine, note) = await ResolveEngineAsync(preferredEngine, lang, cancellationToken);
        if (engine is null)
        {
            StatusMessage = _l.T("tts.noEngine");
            Notify();
            return;
        }

        if (engine.Kind != preferredEngine)
        {
            (chunkSize, breakOnParagraph) = ChunkParams(engine.Kind);
            chunks = TtsTextChunker.Chunk(plainText, chunkSize, breakOnParagraph);
            if (chunks.Count == 0)
            {
                StatusMessage = _l.T("tts.noText");
                Notify();
                return;
            }
        }

        ActiveEngine = engine.Kind;
        if (engine.Kind == TtsEngineKind.Piper)
        {
            lang = "en";
            DetectedLanguage = lang;
        }
        _edgeFellBack = false;
        ChunkCount = chunks.Count;
        CurrentChunkIndex = Math.Clamp(startChunkIndex, 0, chunks.Count - 1);
        StatusMessage = string.IsNullOrWhiteSpace(note)
            ? _l.T("tts.playing", engine.DisplayName, lang)
            : note;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_gate) { _playCts = cts; }

        IsSpeaking = true;
        IsPaused = false;
        Interlocked.Exchange(ref _nav, 0);
        Notify();

        try
        {
            await PlayChunksAsync(engine, chunks, lang, cts.Token, CurrentChunkIndex);
            if (!cts.IsCancellationRequested)
                StatusMessage = _l.T("tts.done");
        }
        catch (OperationCanceledException)
        {
            // stopped by user or chapter change
        }
        catch (Exception ex)
        {
            // Edge failed → Piper (offline EN) → System
            if (engine.Kind == TtsEngineKind.EdgeNeural
                && preferredEngine != TtsEngineKind.System)
            {
                if (preferredEngine != TtsEngineKind.Piper
                    && _engines.TryGetValue(TtsEngineKind.Piper, out var piper))
                {
                    try
                    {
                        StatusMessage = _l.T("tts.piperFallback") + " · " + ex.Message;
                        ActiveEngine = TtsEngineKind.Piper;
                        lang = "en";
                        DetectedLanguage = lang;
                        _edgeFellBack = false;
                        Notify();
                        await Task.Delay(600, CancellationToken.None);
                        var piperChunks = TtsTextChunker.Chunk(plainText, 700);
                        ChunkCount = piperChunks.Count;
                        CurrentChunkIndex = Math.Clamp(startChunkIndex, 0, Math.Max(0, piperChunks.Count - 1));
                        await PlayChunksAsync(piper, piperChunks, lang, cts.Token, CurrentChunkIndex);
                        if (!cts.IsCancellationRequested)
                            StatusMessage = _l.T("tts.done");
                        return;
                    }
                    catch (OperationCanceledException) { return; }
                    catch
                    {
                        // fall through to system
                    }
                }

                if (preferredEngine != TtsEngineKind.System
                    && _engines.TryGetValue(TtsEngineKind.System, out var system))
                {
                    _edgeFellBack = true;
                    StatusMessage = _l.T("tts.edgeFallback") + " · " + ex.Message;
                    ActiveEngine = TtsEngineKind.System;
                    Notify();
                    await Task.Delay(900, CancellationToken.None);
                    try
                    {
                        var systemChunks = TtsTextChunker.Chunk(plainText, 240);
                        ChunkCount = systemChunks.Count;
                        CurrentChunkIndex = Math.Clamp(startChunkIndex, 0, Math.Max(0, systemChunks.Count - 1));
                        await PlayChunksAsync(system, systemChunks, lang, cts.Token, CurrentChunkIndex);
                        if (!cts.IsCancellationRequested)
                            StatusMessage = _l.T("tts.done");
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception fallbackEx)
                    {
                        StatusMessage = _l.T("tts.error", fallbackEx.Message);
                    }
                }
                else
                {
                    StatusMessage = _l.T("tts.error", ex.Message);
                }
            }
            else
            {
                StatusMessage = _l.T("tts.error", ex.Message);
            }
        }
        finally
        {
            IsSpeaking = false;
            IsPaused = false;
            ActiveEngine = null;
            CurrentChunkIndex = 0;
            ChunkCount = 0;
            CurrentHighlightText = null;
            await ClearHighlightAsync();
            lock (_gate)
            {
                if (ReferenceEquals(_playCts, cts))
                    _playCts = null;
            }
            cts.Dispose();
            Notify();
        }
    }

    private static (int MaxChars, bool BreakOnParagraph) ChunkParams(TtsEngineKind kind) => kind switch
    {
        TtsEngineKind.System => (240, true),
        TtsEngineKind.Piper => (700, true),
        _ => (900, true)
    };

    private async Task PlayChunksAsync(
        ITtsEngine engine,
        IReadOnlyList<string> chunks,
        string lang,
        CancellationToken ct,
        int startIndex = 0)
    {
        var i = Math.Clamp(startIndex, 0, Math.Max(0, chunks.Count - 1));
        while (i < chunks.Count)
        {
            ct.ThrowIfCancellationRequested();
            Interlocked.Exchange(ref _nav, 0);

            var start = i;
            var slice = chunks.Skip(start).ToList();

            using var queueCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            lock (_gate) { _queueCts = queueCts; }

            try
            {
                await engine.SpeakQueueAsync(
                    slice,
                    lang,
                    _rate,
                    async (local, token) =>
                    {
                        var global = start + local;
                        CurrentChunkIndex = global;
                        CurrentHighlightText = chunks[global];
                        var engineLabel = _edgeFellBack
                            ? _l.T("tts.engineFallback")
                            : ActiveEngine == TtsEngineKind.EdgeNeural
                                ? _l.T("tts.engineEdge")
                                : ActiveEngine == TtsEngineKind.Piper
                                    ? _l.T("tts.enginePiper")
                                    : _l.T("tts.engineSystem");
                        StatusMessage = _l.T("tts.chunkProgress", engineLabel, global + 1, chunks.Count);
                        Notify();
                        await HighlightAsync(chunks[global]);
                    },
                    queueCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Skip / rate change aborted the current queue — continue via _nav.
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_queueCts, queueCts))
                        _queueCts = null;
                }
            }

            ct.ThrowIfCancellationRequested();
            var nav = Interlocked.Exchange(ref _nav, 0);
            if (nav == 1)
                i = Math.Min(chunks.Count, CurrentChunkIndex + 1);
            else if (nav < 0)
                i = Math.Max(0, CurrentChunkIndex - 1);
            else if (nav == 2)
                i = CurrentChunkIndex; // rate change — replay current
            else
                break; // natural completion of remaining queue
        }
    }

    private void CancelQueue()
    {
        lock (_gate)
        {
            try { _queueCts?.Cancel(); }
            catch { /* ignore */ }
        }
    }

    public async Task SetRateAsync(double rate)
    {
        _rate = Math.Clamp(rate, 0.5, 2.0);
        Notify();

        if (!IsSpeaking || ActiveEngine is null) return;
        if (!_engines.TryGetValue(ActiveEngine.Value, out var engine)) return;

        Interlocked.Exchange(ref _nav, 2);
        if (IsPaused)
        {
            IsPaused = false;
            try { await engine.ResumeAsync(); } catch { /* ignore */ }
        }

        CancelQueue();
        try { await engine.StopAsync(); } catch { /* ignore */ }
    }

    public async Task TogglePlayPauseAsync()
    {
        if (!IsSpeaking || ActiveEngine is null) return;
        if (!_engines.TryGetValue(ActiveEngine.Value, out var engine)) return;

        if (IsPaused)
        {
            await engine.ResumeAsync();
            IsPaused = false;
            StatusMessage = _l.T("tts.resumed");
        }
        else
        {
            await engine.PauseAsync();
            IsPaused = true;
            StatusMessage = _l.T("tts.paused");
        }

        Notify();
    }

    public async Task SkipForwardAsync()
    {
        if (!IsSpeaking || ActiveEngine is null) return;
        if (!_engines.TryGetValue(ActiveEngine.Value, out var engine)) return;

        Interlocked.Exchange(ref _nav, 1);
        if (IsPaused)
        {
            IsPaused = false;
            try { await engine.ResumeAsync(); } catch { /* ignore */ }
        }

        CancelQueue();
        try { await engine.StopAsync(); } catch { /* ignore */ }
        Notify();
    }

    public async Task SkipBackAsync()
    {
        if (!IsSpeaking || ActiveEngine is null) return;
        if (!_engines.TryGetValue(ActiveEngine.Value, out var engine)) return;

        Interlocked.Exchange(ref _nav, -1);
        if (IsPaused)
        {
            IsPaused = false;
            try { await engine.ResumeAsync(); } catch { /* ignore */ }
        }

        CancelQueue();
        try { await engine.StopAsync(); } catch { /* ignore */ }
        Notify();
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _playCts;
            _playCts = null;
            try { _queueCts?.Cancel(); } catch { /* ignore */ }
        }

        Interlocked.Exchange(ref _nav, 0);
        try { cts?.Cancel(); } catch { /* ignore */ }
        cts?.Dispose();

        foreach (var engine in _engines.Values)
        {
            try { await engine.StopAsync(); } catch { /* ignore */ }
        }

        await ClearHighlightAsync();
        CurrentHighlightText = null;
        CurrentChunkIndex = 0;
        ChunkCount = 0;

        if (IsSpeaking || IsPaused)
        {
            IsSpeaking = false;
            IsPaused = false;
            ActiveEngine = null;
            StatusMessage = null;
            Notify();
        }
    }

    private async Task HighlightAsync(string text)
    {
        var runtime = _js.Current;
        if (runtime is null || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var ok = await runtime.InvokeAsync<bool>("epubReaderTts.highlight", text);
            if (!ok)
            {
                // Retry with a short prefix — more resilient when HTML differs from PlainText.
                var prefix = text.Length > 96 ? text[..96] : text;
                await runtime.InvokeAsync<bool>("epubReaderTts.highlight", prefix);
            }
        }
        catch { /* ignore highlight failures */ }
    }

    private async Task ClearHighlightAsync()
    {
        var runtime = _js.Current;
        if (runtime is null) return;
        try
        {
            await runtime.InvokeVoidAsync("epubReaderTts.clearHighlight");
        }
        catch { /* ignore */ }
    }

    private async Task<(ITtsEngine? Engine, string? Note)> ResolveEngineAsync(
        TtsEngineKind preferred,
        string language,
        CancellationToken cancellationToken)
    {
        if (preferred == TtsEngineKind.System)
            return (_engines.GetValueOrDefault(TtsEngineKind.System), null);

        if (preferred == TtsEngineKind.EdgeNeural)
        {
            if (_engines.TryGetValue(TtsEngineKind.EdgeNeural, out var edge)
                && await edge.IsAvailableAsync(language, cancellationToken))
                return (edge, null);

            var sys = _engines.GetValueOrDefault(TtsEngineKind.System);
            return (sys, sys is null ? null : _l.T("tts.edgeUnavailableNote"));
        }

        if (preferred == TtsEngineKind.Piper)
        {
            if (_engines.TryGetValue(TtsEngineKind.Piper, out var piper)
                && await piper.IsAvailableAsync(language, cancellationToken))
                return (piper, null);

            // Not downloaded yet — still return Piper so Speak triggers EnsureReady.
            if (_engines.TryGetValue(TtsEngineKind.Piper, out var piperEngine))
                return (piperEngine, _piper?.StatusMessage ?? _l.T("tts.piperPreparing"));

            var sys = _engines.GetValueOrDefault(TtsEngineKind.System);
            return (sys, sys is null ? null : _l.T("tts.piperUnavailableNote"));
        }

        // Auto: Edge → Piper → System
        if (_engines.TryGetValue(TtsEngineKind.EdgeNeural, out var autoEdge)
            && await autoEdge.IsAvailableAsync(language, cancellationToken))
            return (autoEdge, null);

        if (_engines.TryGetValue(TtsEngineKind.Piper, out var autoPiper)
            && await autoPiper.IsAvailableAsync(language, cancellationToken))
            return (autoPiper, null);

        return (_engines.GetValueOrDefault(TtsEngineKind.System), null);
    }

    private void Notify() => OnChange?.Invoke();
}
