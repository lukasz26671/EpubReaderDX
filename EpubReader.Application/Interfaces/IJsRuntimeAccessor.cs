using Microsoft.JSInterop;

namespace EpubReader.Application.Interfaces;

/// <summary>
/// Holds the Blazor circuit / WebView <see cref="IJSRuntime"/> for singleton services.
/// Must be attached from a component (e.g. ReaderApp) before JS interop.
/// </summary>
public interface IJsRuntimeAccessor
{
    IJSRuntime? Current { get; }
    void Attach(IJSRuntime js);
    void Detach(IJSRuntime js);
}
