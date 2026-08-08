using EpubReader.Application.Interfaces;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Platform;

public sealed class BrowserUriLauncher : IUriLauncher
{
    private readonly IJSRuntime _js;

    public BrowserUriLauncher(IJSRuntime js)
    {
        _js = js;
    }

    public async Task OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        await _js.InvokeVoidAsync("epubReaderUi.openExternal", cancellationToken, url);
    }
}
