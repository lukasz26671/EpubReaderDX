namespace EpubReader.Application.Interfaces;

public interface IUriLauncher
{
    Task OpenAsync(string url, CancellationToken cancellationToken = default);
}
