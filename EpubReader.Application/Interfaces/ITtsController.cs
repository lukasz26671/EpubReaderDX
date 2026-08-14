using EpubReader.Domain.Enums;

namespace EpubReader.Application.Interfaces;

public interface ITtsController
{
    bool IsSpeaking { get; }
    bool IsPaused { get; }
    string? DetectedLanguage { get; }
    TtsEngineKind? ActiveEngine { get; }
    string? StatusMessage { get; }
    int CurrentChunkIndex { get; }
    int ChunkCount { get; }
    string? CurrentHighlightText { get; }
    double Rate { get; }
    event Action? OnChange;

    Task PlayChapterAsync(
        string plainText,
        string? metadataLanguage,
        TtsEngineKind preferredEngine,
        double rate,
        string? languageOverride,
        int startChunkIndex = 0,
        CancellationToken cancellationToken = default);

    Task TogglePlayPauseAsync();
    Task StopAsync();
    Task SkipForwardAsync();
    Task SkipBackAsync();
    /// <summary>Updates playback rate; reapplies from the current chunk if speaking.</summary>
    Task SetRateAsync(double rate);
}
