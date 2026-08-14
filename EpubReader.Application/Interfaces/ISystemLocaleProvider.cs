namespace EpubReader.Application.Interfaces;

public interface ISystemLocaleProvider
{
    /// <summary>BCP-47 tag from OS / browser (e.g. pl-PL, en-US).</summary>
    Task<string> GetSystemLanguageAsync(CancellationToken cancellationToken = default);
}
