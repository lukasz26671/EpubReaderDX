using System.Text.Json;
using EpubReader.Application.Interfaces;
using EpubReader.Application.Models;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Platform;

public sealed class BrowserFilePickerService : IFilePickerService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IJSRuntime _js;

    public BrowserFilePickerService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<PickedEpubFile?> PickEpubAsync(CancellationToken cancellationToken = default)
    {
        var json = await _js.InvokeAsync<string?>("epubReaderFile.pickEpubBase64Json", cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;

        var picked = JsonSerializer.Deserialize<PickedFileDto>(json, JsonOptions);
        if (picked is null || string.IsNullOrWhiteSpace(picked.Base64)) return null;

        return new PickedEpubFile
        {
            Stream = new MemoryStream(Convert.FromBase64String(picked.Base64)),
            FileName = string.IsNullOrWhiteSpace(picked.Name) ? "book.epub" : picked.Name
        };
    }

    private sealed class PickedFileDto
    {
        public string? Base64 { get; set; }
        public string? Name { get; set; }
    }
}
