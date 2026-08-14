using EpubReader.Application.Interfaces;
using Microsoft.JSInterop;

namespace EpubReader.Application.Services;

public sealed class JsRuntimeAccessor : IJsRuntimeAccessor
{
    private IJSRuntime? _js;

    public IJSRuntime? Current => _js;

    public void Attach(IJSRuntime js) => _js = js;

    public void Detach(IJSRuntime js)
    {
        if (ReferenceEquals(_js, js))
            _js = null;
    }
}
