namespace EpubReader.Application.Interfaces;

public sealed record PiperVoiceInfo(
    string Id,
    string DisplayName,
    string Detail,
    bool IsReady);

public enum PiperPrepPhase
{
    Idle,
    Ready,
    DownloadingRuntime,
    DownloadingVoice,
    Extracting,
    Error
}

/// <summary>
/// MAUI-only Piper voice picker + download status. Not registered on Web.
/// </summary>
public interface IPiperVoiceService
{
    IReadOnlyList<PiperVoiceInfo> GetVoices();
    string SelectedVoiceId { get; }
    PiperPrepPhase Phase { get; }
    string? StatusMessage { get; }
    /// <summary>0–1 when known; null while indeterminate.</summary>
    double? Progress { get; }
    event Action? OnChange;

    Task SelectVoiceAsync(string voiceId, CancellationToken cancellationToken = default);
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);
    bool IsVoiceReady(string voiceId);
}
