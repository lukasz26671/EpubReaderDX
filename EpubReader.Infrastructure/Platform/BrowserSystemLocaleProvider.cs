using System.Globalization;
using EpubReader.Application.Interfaces;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Platform;

public sealed class BrowserSystemLocaleProvider : ISystemLocaleProvider
{
    private readonly IJSRuntime _js;

    public BrowserSystemLocaleProvider(IJSRuntime js) => _js = js;

    public async Task<string> GetSystemLanguageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tag = await _js.InvokeAsync<string?>("epubReaderLocale.get", cancellationToken);
            if (!string.IsNullOrWhiteSpace(tag)) return tag;
        }
        catch { /* JS not ready */ }

        return CultureInfo.CurrentUICulture.Name;
    }
}
