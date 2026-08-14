using Microsoft.JSInterop;

namespace EpubReader.Infrastructure.Tts;

/// <summary>Bridge so JS speakQueue can report chunk starts into .NET.</summary>
internal sealed class TtsJsProgressBridge
{
    private readonly Func<int, Task> _onChunk;

    public TtsJsProgressBridge(Func<int, Task> onChunk) => _onChunk = onChunk;

    [JSInvokable]
    public async Task OnChunk(int index)
    {
        // Yield so we leave the JS→.NET call stack before any .NET→JS (highlight) work.
        await Task.Yield();
        await _onChunk(index);
    }
}
