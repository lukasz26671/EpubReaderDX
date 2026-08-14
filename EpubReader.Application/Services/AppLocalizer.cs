using EpubReader.Application.Interfaces;
using EpubReader.Domain.Enums;

namespace EpubReader.Application.Services;

public sealed class AppLocalizer : IAppLocalizer
{
    private readonly ISystemLocaleProvider _systemLocale;
    private string _systemTag = "en";

    public AppLocalizer(ISystemLocaleProvider systemLocale) => _systemLocale = systemLocale;

    public string Culture { get; private set; } = "en";
    public UiLanguageKind Preference { get; private set; } = UiLanguageKind.Auto;
    public event Action? OnChange;

    public async Task InitializeAsync(UiLanguageKind preference, CancellationToken cancellationToken = default)
    {
        _systemTag = await _systemLocale.GetSystemLanguageAsync(cancellationToken);
        Preference = preference;
        Culture = Resolve(preference, _systemTag);
        OnChange?.Invoke();
    }

    public Task SetPreferenceAsync(UiLanguageKind preference, CancellationToken cancellationToken = default)
    {
        Preference = preference;
        Culture = Resolve(preference, _systemTag);
        OnChange?.Invoke();
        return Task.CompletedTask;
    }

    public string T(string key) =>
        Catalog.TryGet(Culture, key) ?? Catalog.TryGet("en", key) ?? key;

    public string T(string key, params object[] args)
    {
        var format = T(key);
        try { return string.Format(format, args); }
        catch { return format; }
    }

    internal static string Resolve(UiLanguageKind preference, string systemTag)
    {
        if (preference == UiLanguageKind.Polish) return "pl";
        if (preference == UiLanguageKind.English) return "en";

        var tag = (systemTag ?? "en").Trim().Replace('_', '-');
        if (tag.StartsWith("pl", StringComparison.OrdinalIgnoreCase))
            return "pl";
        return "en";
    }
}
