using EpubReader.Application.Interfaces;
using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Tts;

public static class JsRuntimeGuard
{
    public static IJSRuntime Require(IJsRuntimeAccessor accessor) =>
        accessor.Current
        ?? throw new InvalidOperationException(
            "JavaScript runtime is not ready. Open the reader UI before using TTS.");
}
