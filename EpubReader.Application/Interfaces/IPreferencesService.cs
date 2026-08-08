using EpubReader.Domain.Entities;

namespace EpubReader.Application.Interfaces;

public interface IPreferencesService
{
    Task<ReaderSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(ReaderSettings settings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Bookmark>> LoadBookmarksAsync(string bookKey, CancellationToken cancellationToken = default);
    Task SaveBookmarksAsync(string bookKey, IEnumerable<Bookmark> bookmarks, CancellationToken cancellationToken = default);
    Task<ReadingPosition?> LoadPositionAsync(string bookKey, CancellationToken cancellationToken = default);
    Task SavePositionAsync(string bookKey, ReadingPosition position, CancellationToken cancellationToken = default);
}
