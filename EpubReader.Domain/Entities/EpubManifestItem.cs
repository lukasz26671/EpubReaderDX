namespace EpubReader.Domain.Entities;

public class EpubManifestItem
{
    public string Id { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Properties { get; set; } = string.Empty;
}
