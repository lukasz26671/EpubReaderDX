using EpubReader.Application;
using EpubReader.Application.Interfaces;
using EpubReader.Infrastructure;
using EpubReaderDX.Services;
using Microsoft.Extensions.Logging;

namespace EpubReaderDX;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddEpubReaderCore();
        builder.Services.AddEpubReaderApplication();
        // Must match IReaderStateService lifetime (singleton) — scoped platform services
        // become separate instances per Blazor component in MAUI WebView.
        builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();
        builder.Services.AddSingleton<IPreferencesService, LocalPreferencesService>();
        builder.Services.AddSingleton<ILastBookStore, MauiLastBookStore>();
        builder.Services.AddSingleton<IUriLauncher, MauiUriLauncher>();
        builder.Services.AddSingleton<ISystemLocaleProvider, MauiSystemLocaleProvider>();
        builder.Services.AddSingleton<ITtsEngine, EpubReader.Infrastructure.Tts.SystemTtsEngine>();
        builder.Services.AddSingleton<ITtsEngine, EpubReader.Infrastructure.Tts.EdgeNeuralTtsEngine>();
        builder.Services.AddSingleton<EpubReaderDX.Services.Tts.PiperTtsEngine>();
        builder.Services.AddSingleton<ITtsEngine>(sp => sp.GetRequiredService<EpubReaderDX.Services.Tts.PiperTtsEngine>());
        builder.Services.AddSingleton<IPiperVoiceService>(sp => sp.GetRequiredService<EpubReaderDX.Services.Tts.PiperTtsEngine>());

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
