using EpubReader.Application.Interfaces;
using EpubReader.Application.Models;

namespace EpubReaderDX.Services;

public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<PickedEpubFile?> PickEpubAsync(CancellationToken cancellationToken = default)
    {
        var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, ["com.adobe.epub", "org.idpf.epub-container", "public.data"] },
            { DevicePlatform.Android, ["application/epub+zip", "application/octet-stream", "*/*"] },
            { DevicePlatform.WinUI, [".epub"] },
            { DevicePlatform.MacCatalyst, ["epub", "public.data"] }
        });

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Wybierz plik EPUB",
            FileTypes = customFileType
        });

        if (result is null)
        {
            return null;
        }

        await using var source = await result.OpenReadAsync();
        var buffered = new MemoryStream();
        await source.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        return new PickedEpubFile
        {
            Stream = buffered,
            FileName = string.IsNullOrWhiteSpace(result.FileName) ? "book.epub" : result.FileName
        };
    }
}
