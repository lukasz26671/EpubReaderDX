namespace EpubReader.Application.Models;

public sealed class PickedEpubFile : IAsyncDisposable, IDisposable
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }

    public void Dispose() => Stream.Dispose();

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}
