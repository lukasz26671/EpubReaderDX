using EpubReader.Application.Interfaces;
using EpubReader.Domain.Entities;

namespace EpubReader.Application.Services;

public sealed class BookmarkService : IBookmarkService
{
    private readonly IPreferencesService _preferences;
    private readonly List<Bookmark> _bookmarks = [];
    private string _bookKey = string.Empty;
    private Func<int>? _currentChapterIndex;
    private Func<string?>? _currentChapterTitle;

    public BookmarkService(IPreferencesService preferences)
    {
        _preferences = preferences;
    }

    public IReadOnlyList<Bookmark> Bookmarks => _bookmarks;

    public bool IsCurrentChapterBookmarked
    {
        get
        {
            var index = _currentChapterIndex?.Invoke() ?? -1;
            return index >= 0 && _bookmarks.Any(b => b.ChapterIndex == index);
        }
    }

    public void Bind(Func<int> currentChapterIndex, Func<string?> currentChapterTitle)
    {
        _currentChapterIndex = currentChapterIndex;
        _currentChapterTitle = currentChapterTitle;
    }

    public async Task LoadForBookAsync(string bookKey, CancellationToken cancellationToken = default)
    {
        _bookKey = bookKey;
        _bookmarks.Clear();
        var loaded = await _preferences.LoadBookmarksAsync(bookKey, cancellationToken);
        _bookmarks.AddRange(loaded);
    }

    public async Task ToggleCurrentAsync(CancellationToken cancellationToken = default)
    {
        var index = _currentChapterIndex?.Invoke() ?? -1;
        if (index < 0 || string.IsNullOrEmpty(_bookKey))
        {
            return;
        }

        var existing = _bookmarks.FirstOrDefault(b => b.ChapterIndex == index);
        if (existing is not null)
        {
            _bookmarks.Remove(existing);
        }
        else
        {
            _bookmarks.Add(new Bookmark
            {
                ChapterIndex = index,
                Title = _currentChapterTitle?.Invoke() ?? $"Rozdział {index + 1}"
            });
        }

        await PersistAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
    {
        var existing = _bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
        if (existing is null)
        {
            return;
        }

        _bookmarks.Remove(existing);
        await PersistAsync(cancellationToken);
    }

    private Task PersistAsync(CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(_bookKey)
            ? Task.CompletedTask
            : _preferences.SaveBookmarksAsync(_bookKey, _bookmarks, cancellationToken);
}
