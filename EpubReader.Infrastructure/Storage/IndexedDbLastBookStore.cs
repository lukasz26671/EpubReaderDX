using System.Text.Json;
using System.Text.Json.Serialization;
using EpubReader.Application.Interfaces;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Storage;

/// <summary>
/// Last EPUB in IndexedDB (browser). localStorage is too small for full books.
/// </summary>
public sealed class IndexedDbLastBookStore : ILastBookStore
{
    private readonly IJSRuntime _js;

    public IndexedDbLastBookStore(IJSRuntime js) => _js = js;

    public async Task SaveAsync(string fileName, byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes.Length == 0) return;
        var name = string.IsNullOrWhiteSpace(fileName) ? "book.epub" : fileName.Trim();
        await _js.InvokeVoidAsync("epubReaderLastBook.save", cancellationToken, name, bytes);
    }

    public async Task<LastBookBlob?> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _js.InvokeAsync<string?>("epubReaderLastBook.loadJson", cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<LastBookDto>(json);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Base64)) return null;
            var bytes = Convert.FromBase64String(dto.Base64);
            if (bytes.Length == 0) return null;
            var name = string.IsNullOrWhiteSpace(dto.FileName) ? "book.epub" : dto.FileName!;
            return new LastBookBlob(name, bytes);
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _js.InvokeVoidAsync("epubReaderLastBook.clear", cancellationToken);
    }

    private sealed class LastBookDto
    {
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("base64")]
        public string? Base64 { get; set; }
    }
}
