using System.Globalization;
using EpubReader.Application.Interfaces;

namespace EpubReaderDX.Services;

public sealed class MauiSystemLocaleProvider : ISystemLocaleProvider
{
    public Task<string> GetSystemLanguageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CultureInfo.CurrentUICulture.Name);
}
