using EpubReader.Application.Interfaces;
using EpubReader.Infrastructure.Parsing;
using EpubReader.Infrastructure.Platform;
using EpubReader.Infrastructure.Sample;
using EpubReader.Infrastructure.Storage;
using EpubReader.Infrastructure.Tts;
using Microsoft.Extensions.DependencyInjection;

namespace EpubReader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEpubReaderCore(this IServiceCollection services)
    {
        services.AddSingleton<IEpubParserService, EpubParserService>();
        services.AddSingleton<ISampleBookService, SampleBookService>();
        return services;
    }

    public static IServiceCollection AddEpubReaderWebPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IFilePickerService, BrowserFilePickerService>();
        services.AddSingleton<IPreferencesService, LocalStoragePreferencesService>();
        services.AddSingleton<ILastBookStore, IndexedDbLastBookStore>();
        services.AddSingleton<IUriLauncher, BrowserUriLauncher>();
        services.AddSingleton<ISystemLocaleProvider, BrowserSystemLocaleProvider>();
        services.AddSingleton<ITtsEngine, SystemTtsEngine>();
        // Browser WASM cannot open Edge WebSockets (Origin locked). Uses HTTP proxy — see EdgeTtsProxyConfig.
        services.AddSingleton<ITtsEngine, EdgeNeuralTtsEngine>();
        return services;
    }
}
