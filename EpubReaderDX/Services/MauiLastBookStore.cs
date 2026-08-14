using EpubReader.Application.Interfaces;

namespace EpubReaderDX.Services;

/// <summary>
/// Last EPUB on disk under AppData (Windows + Android). Metadata in Preferences.
/// </summary>
public sealed class MauiLastBookStore : ILastBookStore
{
    private const string MetaFileNameKey = "epubreader.lastBook.fileName";
    private const string MetaSizeKey = "epubreader.lastBook.size";
    private static readonly string BooksDir = Path.Combine(FileSystem.AppDataDirectory, "books");
    private static readonly string LastPath = Path.Combine(BooksDir, "last.epub");

    public async Task SaveAsync(string fileName, byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes.Length == 0) return;
        Directory.CreateDirectory(BooksDir);
        var temp = LastPath + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
        if (File.Exists(LastPath)) File.Delete(LastPath);
        File.Move(temp, LastPath);
        Preferences.Default.Set(MetaFileNameKey, string.IsNullOrWhiteSpace(fileName) ? "book.epub" : fileName.Trim());
        Preferences.Default.Set(MetaSizeKey, bytes.Length);
    }

    public async Task<LastBookBlob?> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(LastPath)) return null;
        try
        {
            var bytes = await File.ReadAllBytesAsync(LastPath, cancellationToken);
            if (bytes.Length == 0) return null;
            var name = Preferences.Default.Get(MetaFileNameKey, "book.epub");
            if (string.IsNullOrWhiteSpace(name)) name = "book.epub";
            return new LastBookBlob(name, bytes);
        }
        catch
        {
            return null;
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(LastPath)) File.Delete(LastPath);
            var tmp = LastPath + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        catch { /* ignore */ }

        Preferences.Default.Remove(MetaFileNameKey);
        Preferences.Default.Remove(MetaSizeKey);
        return Task.CompletedTask;
    }
}
