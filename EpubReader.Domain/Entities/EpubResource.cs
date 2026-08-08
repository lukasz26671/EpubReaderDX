namespace EpubReader.Domain.Entities;

public sealed class EpubResource
{
    public string FullPath { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
}
