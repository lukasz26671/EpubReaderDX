using EpubReader.Application.Models;

namespace EpubReader.Application.Interfaces;

public interface IFilePickerService
{
    Task<PickedEpubFile?> PickEpubAsync(CancellationToken cancellationToken = default);
}
