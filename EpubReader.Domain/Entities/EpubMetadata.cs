namespace EpubReader.Domain.Entities;

public class EpubMetadata
{
    public string Title { get; set; } = "Unknown Title";
    public List<string> Authors { get; set; } = [];
    public string Language { get; set; } = "en";
    public string Publisher { get; set; } = "Unknown Publisher";
    public string Description { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string Rights { get; set; } = string.Empty;
    public string CoverHref { get; set; } = string.Empty;
    public string CoverPath { get; set; } = string.Empty;
}
