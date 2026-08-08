using System.Text.Json;
using EpubReader.Application.Interfaces;
using EpubReader.Domain.Entities;

namespace EpubReaderDX.Services;

public sealed class LocalPreferencesService : IPreferencesService
{
    private const string SettingsKey = "epubreader.settings";
    private const string BookmarksPrefix = "epubreader.bookmarks.";
    private const string PositionPrefix = "epubreader.position.";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Task<ReaderSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        var json = Preferences.Default.Get(SettingsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return Task.FromResult(new ReaderSettings());
        try { return Task.FromResult(JsonSerializer.Deserialize<ReaderSettings>(json, JsonOptions) ?? new ReaderSettings()); }
        catch { return Task.FromResult(new ReaderSettings()); }
    }

    public Task SaveSettingsAsync(ReaderSettings settings, CancellationToken cancellationToken = default)
    {
        Preferences.Default.Set(SettingsKey, JsonSerializer.Serialize(settings, JsonOptions));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Bookmark>> LoadBookmarksAsync(string bookKey, CancellationToken cancellationToken = default)
    {
        var json = Preferences.Default.Get(BookmarksPrefix + bookKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return Task.FromResult<IReadOnlyList<Bookmark>>([]);
        try { return Task.FromResult<IReadOnlyList<Bookmark>>(JsonSerializer.Deserialize<List<Bookmark>>(json, JsonOptions) ?? []); }
        catch { return Task.FromResult<IReadOnlyList<Bookmark>>([]); }
    }

    public Task SaveBookmarksAsync(string bookKey, IEnumerable<Bookmark> bookmarks, CancellationToken cancellationToken = default)
    {
        Preferences.Default.Set(BookmarksPrefix + bookKey, JsonSerializer.Serialize(bookmarks.ToList(), JsonOptions));
        return Task.CompletedTask;
    }

    public Task<ReadingPosition?> LoadPositionAsync(string bookKey, CancellationToken cancellationToken = default)
    {
        var json = Preferences.Default.Get(PositionPrefix + bookKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return Task.FromResult<ReadingPosition?>(null);
        try { return Task.FromResult(JsonSerializer.Deserialize<ReadingPosition>(json, JsonOptions)); }
        catch { return Task.FromResult<ReadingPosition?>(null); }
    }

    public Task SavePositionAsync(string bookKey, ReadingPosition position, CancellationToken cancellationToken = default)
    {
        Preferences.Default.Set(PositionPrefix + bookKey, JsonSerializer.Serialize(position, JsonOptions));
        return Task.CompletedTask;
    }
}
