using EpubReader.Domain.Entities;

namespace EpubReader.Application.Interfaces;

public interface IBookmarkService
{
    IReadOnlyList<Bookmark> Bookmarks { get; }
    bool IsCurrentChapterBookmarked { get; }
    Task ToggleCurrentAsync(CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid bookmarkId, CancellationToken cancellationToken = default);
    Task LoadForBookAsync(string bookKey, CancellationToken cancellationToken = default);
}
