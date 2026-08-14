namespace EpubReader.Application.Interfaces;

public sealed record LastBookBlob(string FileName, byte[] Bytes);

/// <summary>
/// Persists the last opened EPUB in full so it can be restored on next launch.
/// Web: IndexedDB · MAUI (Windows/Android): AppData file.
/// </summary>
public interface ILastBookStore
{
    Task SaveAsync(string fileName, byte[] bytes, CancellationToken cancellationToken = default);
    Task<LastBookBlob?> TryLoadAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
