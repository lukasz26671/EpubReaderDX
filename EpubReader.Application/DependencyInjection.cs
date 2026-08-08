using EpubReader.Application.Interfaces;
using EpubReader.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EpubReader.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddEpubReaderApplication(this IServiceCollection services)
    {
        // Singleton is required for MAUI BlazorWebView: scoped services are NOT shared
        // across components, which left the header with a loaded book while TOC/viewport stayed empty.
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IBookmarkService, BookmarkService>();
        services.AddSingleton<IReaderStateService, ReaderStateService>();
        return services;
    }
}
