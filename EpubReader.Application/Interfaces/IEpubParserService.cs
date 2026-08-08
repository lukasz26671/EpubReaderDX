using EpubReader.Domain.Entities;

namespace EpubReader.Application.Interfaces;

public interface IEpubParserService
{
    Task<EpubBook> ParseAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default);
}
