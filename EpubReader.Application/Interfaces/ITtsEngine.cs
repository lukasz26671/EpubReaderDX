using EpubReader.Domain.Enums;

namespace EpubReader.Application.Interfaces;

public interface ITtsEngine
{
    TtsEngineKind Kind { get; }
    string DisplayName { get; }
    Task<bool> IsAvailableAsync(string language, CancellationToken cancellationToken = default);
    Task SpeakAsync(string text, string language, double rate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Speaks all chunks with prefetch/queue to minimize gaps.
    /// <paramref name="onChunkStarted"/> is invoked when a chunk actually begins (0-based).
    /// </summary>
    Task SpeakQueueAsync(
        IReadOnlyList<string> chunks,
        string language,
        double rate,
        Func<int, CancellationToken, Task>? onChunkStarted,
        CancellationToken cancellationToken = default);

    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
}
