using EpubReader.Domain.Enums;

namespace EpubReader.Application.Interfaces;

public interface IAppLocalizer
{
    /// <summary>Resolved UI culture: "en" or "pl".</summary>
    string Culture { get; }
    UiLanguageKind Preference { get; }
    event Action? OnChange;

    Task InitializeAsync(UiLanguageKind preference, CancellationToken cancellationToken = default);
    Task SetPreferenceAsync(UiLanguageKind preference, CancellationToken cancellationToken = default);
    string T(string key);
    string T(string key, params object[] args);
}
