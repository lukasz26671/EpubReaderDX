using EpubReader.Application.Models;
using EpubReader.Domain.Entities;

namespace EpubReader.Application.Interfaces;

public interface IReaderStateService
{
    EpubBook? Book { get; }
    EpubChapter? CurrentChapter { get; }
    int CurrentChapterIndex { get; }
    ReaderSettings Settings { get; }
    IReadOnlyList<Bookmark> Bookmarks { get; }
    IReadOnlyList<SearchHit> SearchResults { get; }
    string SearchQuery { get; }
    string TocFilter { get; }
    string? PendingScrollFragment { get; }
    bool HasBook { get; }
    bool IsSidebarOpen { get; }
    bool IsSettingsOpen { get; }
    bool IsSearchOpen { get; }
    bool IsBookmarksOpen { get; }
    bool IsMetadataOpen { get; }
    bool IsDropZoneVisible { get; }
    bool IsLoading { get; }
    bool IsFullscreenChromeHidden { get; }
    string? ErrorMessage { get; }
    string? StatusMessage { get; }
    int ProgressPercent { get; }
    bool IsCurrentChapterBookmarked { get; }
    event Action? OnChange;

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task OpenFromStreamAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default);
    Task OpenFromBase64Async(string base64, string? sourceName = null, CancellationToken cancellationToken = default);
    Task LoadSampleAsync(CancellationToken cancellationToken = default);
    Task PickAndOpenAsync(CancellationToken cancellationToken = default);
    void GoToChapter(int index, string? fragment = null);
    void NextChapter();
    void PreviousChapter();
    Task HandleContentLinkAsync(string? href, string? basePath = null, CancellationToken cancellationToken = default);
    Task ToggleCurrentBookmarkAsync(CancellationToken cancellationToken = default);
    Task RemoveBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default);
    void Search(string query);
    void SetTocFilter(string filter);
    Task UpdateSettingsAsync(ReaderSettings settings, CancellationToken cancellationToken = default);
    Task AdjustFontSizeAsync(int delta, CancellationToken cancellationToken = default);
    void ToggleSidebar();
    void SetSidebarOpen(bool open);
    void ToggleSettings();
    void ToggleSearch();
    void ToggleBookmarks();
    void ToggleMetadata();
    void ToggleChrome();
    void SetDropZoneVisible(bool visible);
    void CloseDrawers();
    Task PersistPositionAsync(double scrollRatio = 0, CancellationToken cancellationToken = default);
    void ClearPendingScrollFragment();
    void ClearStatus();
}
