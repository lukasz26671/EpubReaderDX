using EpubReader.Domain.Entities;

namespace EpubReader.Application.Interfaces;

public interface ISampleBookService
{
    Task<Stream> CreateSampleAsync(CancellationToken cancellationToken = default);
}
