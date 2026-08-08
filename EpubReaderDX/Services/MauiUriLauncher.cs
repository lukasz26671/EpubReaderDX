using EpubReader.Application.Interfaces;

namespace EpubReaderDX.Services;

public sealed class MauiUriLauncher : IUriLauncher
{
    public async Task OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            await Launcher.Default.OpenAsync(url);
        }
    }
}
