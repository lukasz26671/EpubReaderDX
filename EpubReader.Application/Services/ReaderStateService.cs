using EpubReader.Application.Helpers;
using EpubReader.Application.Interfaces;
using EpubReader.Application.Models;
using EpubReader.Domain.Entities;

namespace EpubReader.Application.Services;

public sealed class ReaderStateService : IReaderStateService
{
    private readonly IEpubParserService _parser;
    private readonly ISampleBookService _sampleBook;
    private readonly IFilePickerService _filePicker;
    private readonly IPreferencesService _preferences;
    private readonly ISearchService _search;
    private readonly IUriLauncher _uriLauncher;
    private readonly BookmarkService _bookmarks;

    private EpubBook? _book;
    private ReaderSettings _settings = new();
    private IReadOnlyList<SearchHit> _searchResults = [];
    private string _searchQuery = string.Empty;
    private string _tocFilter = string.Empty;
    private string _bookKey = string.Empty;

    public ReaderStateService(
        IEpubParserService parser,
        ISampleBookService sampleBook,
        IFilePickerService filePicker,
        IPreferencesService preferences,
        ISearchService search,
        IUriLauncher uriLauncher,
        IBookmarkService bookmarks)
    {
        _parser = parser;
        _sampleBook = sampleBook;
        _filePicker = filePicker;
        _preferences = preferences;
        _search = search;
        _uriLauncher = uriLauncher;
        _bookmarks = bookmarks as BookmarkService
            ?? throw new InvalidOperationException("IBookmarkService must be BookmarkService.");
        _bookmarks.Bind(() => CurrentChapterIndex, () => CurrentChapter?.Title);
    }

    public EpubBook? Book => _book;
    public EpubChapter? CurrentChapter =>
        _book is not null && CurrentChapterIndex >= 0 && CurrentChapterIndex < _book.Chapters.Count
            ? _book.Chapters[CurrentChapterIndex]
            : null;

    public int CurrentChapterIndex { get; private set; }
    public ReaderSettings Settings => _settings;
    public IReadOnlyList<Bookmark> Bookmarks => _bookmarks.Bookmarks;
    public IReadOnlyList<SearchHit> SearchResults => _searchResults;
    public string SearchQuery => _searchQuery;
    public string TocFilter => _tocFilter;
    public string? PendingScrollFragment { get; private set; }
    public bool HasBook => _book?.Chapters.Count > 0;
    public bool IsSidebarOpen { get; private set; } = true;
    public bool IsSettingsOpen { get; private set; }
    public bool IsSearchOpen { get; private set; }
    public bool IsBookmarksOpen { get; private set; }
    public bool IsMetadataOpen { get; private set; }
    public bool IsDropZoneVisible { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsFullscreenChromeHidden { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? StatusMessage { get; private set; }

    public int ProgressPercent =>
        !HasBook || _book is null
            ? 0
            : (int)Math.Round(((CurrentChapterIndex + 1) / (double)_book.Chapters.Count) * 100);

    public bool IsCurrentChapterBookmarked => _bookmarks.IsCurrentChapterBookmarked;

    public event Action? OnChange;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _preferences.LoadSettingsAsync(cancellationToken);
        Notify();
    }

    public async Task OpenFromBase64Async(string base64, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(base64)) return;
        var bytes = Convert.FromBase64String(base64);
        await using var stream = new MemoryStream(bytes);
        await OpenFromStreamAsync(stream, sourceName, cancellationToken);
    }

    public async Task OpenFromStreamAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = "Parsowanie EPUB…";
            Notify();

            await using var buffered = new MemoryStream();
            await stream.CopyToAsync(buffered, cancellationToken);
            buffered.Position = 0;

            var book = await _parser.ParseAsync(buffered, sourceName, cancellationToken);
            await ApplyBookAsync(book, cancellationToken);
            StatusMessage = $"Załadowano {book.Chapters.Count} rozdziałów";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
            Notify();
        }
        finally
        {
            IsLoading = false;
            Notify();
        }
    }

    public async Task LoadSampleAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await _sampleBook.CreateSampleAsync(cancellationToken);
        await OpenFromStreamAsync(stream, "SampleBook.epub", cancellationToken);
    }

    public async Task PickAndOpenAsync(CancellationToken cancellationToken = default)
    {
        await using var picked = await _filePicker.PickEpubAsync(cancellationToken);
        if (picked is null) return;
        await OpenFromStreamAsync(picked.Stream, picked.FileName, cancellationToken);
    }

    public void GoToChapter(int index, string? fragment = null)
    {
        if (_book is null || index < 0 || index >= _book.Chapters.Count) return;
        CurrentChapterIndex = index;
        PendingScrollFragment = string.IsNullOrWhiteSpace(fragment) ? null : fragment;
        _ = PersistPositionAsync();
        Notify();
    }

    public void NextChapter() => GoToChapter(CurrentChapterIndex + 1);

    public void PreviousChapter() => GoToChapter(CurrentChapterIndex - 1);

    public async Task HandleContentLinkAsync(string? href, string? basePath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(href) || href == "#") return;

        if (EpubHref.IsExternal(href))
        {
            await _uriLauncher.OpenAsync(href, cancellationToken);
            StatusMessage = "Otwarto łącze zewnętrzne";
            Notify();
            return;
        }

        if (href.StartsWith('#'))
        {
            PendingScrollFragment = href.TrimStart('#');
            Notify();
            return;
        }

        var baseForResolve = string.IsNullOrWhiteSpace(basePath)
            ? CurrentChapter?.FullPath ?? _book?.OpfDirectory ?? string.Empty
            : basePath;
        var resolved = EpubHref.Combine(baseForResolve, href);
        var fragment = EpubHref.GetFragment(href);

        if (_book is null) return;

        for (var i = 0; i < _book.Chapters.Count; i++)
        {
            var chapter = _book.Chapters[i];
            if (EpubHref.PathsMatch(chapter.FullPath, resolved)
                || EpubHref.PathsMatch(chapter.Href, resolved)
                || EpubHref.PathsMatch(chapter.FullPath, EpubHref.Combine(_book.OpfDirectory, resolved)))
            {
                GoToChapter(i, fragment);
                StatusMessage = $"Przejście: {chapter.Title}";
                Notify();
                return;
            }
        }

        // Same-document fragment already handled; unresolved internal path
        StatusMessage = "Nie znaleziono docelowego rozdziału";
        Notify();
    }

    public async Task ToggleCurrentBookmarkAsync(CancellationToken cancellationToken = default)
    {
        await _bookmarks.ToggleCurrentAsync(cancellationToken);
        StatusMessage = IsCurrentChapterBookmarked ? "Dodano zakładkę" : "Usunięto zakładkę";
        Notify();
    }

    public async Task RemoveBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
    {
        await _bookmarks.RemoveAsync(bookmarkId, cancellationToken);
        Notify();
    }

    public void Search(string query)
    {
        _searchQuery = query?.Trim() ?? string.Empty;
        _searchResults = _book is null || string.IsNullOrWhiteSpace(_searchQuery)
            ? []
            : _search.Search(_book, _searchQuery, 50);
        Notify();
    }

    public void SetTocFilter(string filter)
    {
        _tocFilter = filter ?? string.Empty;
        Notify();
    }

    public async Task UpdateSettingsAsync(ReaderSettings settings, CancellationToken cancellationToken = default)
    {
        _settings = new ReaderSettings
        {
            Theme = settings.Theme,
            FontFamily = settings.FontFamily,
            FontSize = Math.Clamp(settings.FontSize, 12, 40),
            LineHeight = Math.Clamp(settings.LineHeight, 1.1, 2.8),
            LetterSpacing = Math.Clamp(settings.LetterSpacing, -0.05, 0.2),
            ParagraphSpacing = Math.Clamp(settings.ParagraphSpacing, 0.4, 2.5),
            TextAlign = settings.TextAlign,
            ContentWidth = Math.Clamp(settings.ContentWidth, 28, 60),
            PageMargin = Math.Clamp(settings.PageMargin, 8, 48)
        };
        await _preferences.SaveSettingsAsync(_settings, cancellationToken);
        Notify();
    }

    public Task AdjustFontSizeAsync(int delta, CancellationToken cancellationToken = default)
    {
        var s = CloneCurrentSettings();
        s.FontSize += delta;
        return UpdateSettingsAsync(s, cancellationToken);
    }

    private ReaderSettings CloneCurrentSettings() => new()
    {
        Theme = _settings.Theme,
        FontFamily = _settings.FontFamily,
        FontSize = _settings.FontSize,
        LineHeight = _settings.LineHeight,
        LetterSpacing = _settings.LetterSpacing,
        ParagraphSpacing = _settings.ParagraphSpacing,
        TextAlign = _settings.TextAlign,
        ContentWidth = _settings.ContentWidth,
        PageMargin = _settings.PageMargin
    };

    public void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
        Notify();
    }

    public void SetSidebarOpen(bool open)
    {
        IsSidebarOpen = open;
        Notify();
    }

    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        if (IsSettingsOpen)
        {
            IsSearchOpen = false;
            IsBookmarksOpen = false;
        }

        Notify();
    }

    public void ToggleSearch()
    {
        IsSearchOpen = !IsSearchOpen;
        if (IsSearchOpen)
        {
            IsSettingsOpen = false;
            IsBookmarksOpen = false;
        }

        Notify();
    }

    public void ToggleBookmarks()
    {
        IsBookmarksOpen = !IsBookmarksOpen;
        if (IsBookmarksOpen)
        {
            IsSettingsOpen = false;
            IsSearchOpen = false;
        }

        Notify();
    }

    public void ToggleMetadata()
    {
        IsMetadataOpen = !IsMetadataOpen;
        Notify();
    }

    public void ToggleChrome()
    {
        IsFullscreenChromeHidden = !IsFullscreenChromeHidden;
        Notify();
    }

    public void SetDropZoneVisible(bool visible)
    {
        IsDropZoneVisible = visible;
        Notify();
    }

    public void CloseDrawers()
    {
        IsSettingsOpen = false;
        IsSearchOpen = false;
        IsBookmarksOpen = false;
        IsMetadataOpen = false;
        Notify();
    }

    public async Task PersistPositionAsync(double scrollRatio = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_bookKey) || !HasBook) return;
        await _preferences.SavePositionAsync(_bookKey, new ReadingPosition
        {
            ChapterIndex = CurrentChapterIndex,
            ScrollRatio = Math.Clamp(scrollRatio, 0, 1)
        }, cancellationToken);
    }

    public void ClearPendingScrollFragment() => PendingScrollFragment = null;

    public void ClearStatus()
    {
        StatusMessage = null;
        Notify();
    }

    private async Task ApplyBookAsync(EpubBook book, CancellationToken cancellationToken)
    {
        _book = book;
        _searchQuery = string.Empty;
        _searchResults = [];
        _tocFilter = string.Empty;
        _bookKey = BuildBookKey(book);
        await _bookmarks.LoadForBookAsync(_bookKey, cancellationToken);

        var position = await _preferences.LoadPositionAsync(_bookKey, cancellationToken);
        CurrentChapterIndex = position is not null && position.ChapterIndex >= 0 && position.ChapterIndex < book.Chapters.Count
            ? position.ChapterIndex
            : 0;
        PendingScrollFragment = null;
        IsDropZoneVisible = false;
        IsSidebarOpen = true;
        ErrorMessage = null;
        Notify();
    }

    private static string BuildBookKey(EpubBook book)
    {
        var id = !string.IsNullOrWhiteSpace(book.Metadata.Identifier)
            ? book.Metadata.Identifier
            : book.Metadata.Title;
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{id}|{book.Metadata.Title}|{book.Chapters.Count}"))).ToLowerInvariant();
    }

    private void Notify() => OnChange?.Invoke();
}
