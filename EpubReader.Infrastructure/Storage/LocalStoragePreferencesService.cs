using System.Text.Json;
using EpubReader.Application.Interfaces;
using EpubReader.Domain.Entities;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Storage;

public sealed class LocalStoragePreferencesService : IPreferencesService
{
    private const string SettingsKey = "epubreader.settings";
    private const string BookmarksPrefix = "epubreader.bookmarks.";
    private const string PositionPrefix = "epubreader.position.";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IJSRuntime _js;

    public LocalStoragePreferencesService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<ReaderSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        var json = await _js.InvokeAsync<string?>("epubReaderPrefs.get", cancellationToken, SettingsKey);
        if (string.IsNullOrWhiteSpace(json)) return new ReaderSettings();
        try { return JsonSerializer.Deserialize<ReaderSettings>(json, JsonOptions) ?? new ReaderSettings(); }
        catch { return new ReaderSettings(); }
    }

    public async Task SaveSettingsAsync(ReaderSettings settings, CancellationToken cancellationToken = default)
    {
        await _js.InvokeVoidAsync("epubReaderPrefs.set", cancellationToken, SettingsKey, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public async Task<IReadOnlyList<Bookmark>> LoadBookmarksAsync(string bookKey, CancellationToken cancellationToken = default)
    {
        var json = await _js.InvokeAsync<string?>("epubReaderPrefs.get", cancellationToken, BookmarksPrefix + bookKey);
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<Bookmark>>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    public async Task SaveBookmarksAsync(string bookKey, IEnumerable<Bookmark> bookmarks, CancellationToken cancellationToken = default)
    {
        await _js.InvokeVoidAsync("epubReaderPrefs.set", cancellationToken, BookmarksPrefix + bookKey,
            JsonSerializer.Serialize(bookmarks.ToList(), JsonOptions));
    }

    public async Task<ReadingPosition?> LoadPositionAsync(string bookKey, CancellationToken cancellationToken = default)
    {
        var json = await _js.InvokeAsync<string?>("epubReaderPrefs.get", cancellationToken, PositionPrefix + bookKey);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ReadingPosition>(json, JsonOptions); }
        catch { return null; }
    }

    public async Task SavePositionAsync(string bookKey, ReadingPosition position, CancellationToken cancellationToken = default)
    {
        await _js.InvokeVoidAsync("epubReaderPrefs.set", cancellationToken, PositionPrefix + bookKey,
            JsonSerializer.Serialize(position, JsonOptions));
    }
}
