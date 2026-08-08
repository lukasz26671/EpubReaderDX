namespace EpubReader.Domain.Entities;

public class EpubBook
{
    public string FilePath { get; set; } = string.Empty;
    public EpubMetadata Metadata { get; set; } = new();
    public Dictionary<string, EpubManifestItem> Manifest { get; set; } = new();
    public List<string> Spine { get; set; } = [];
    public List<EpubTocNode> TableOfContents { get; set; } = [];
    public List<EpubChapter> Chapters { get; set; } = [];
    public Dictionary<string, EpubResource> Resources { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string OpfDirectory { get; set; } = string.Empty;
    public string? CoverDataUri { get; set; }
    public string BundledCss { get; set; } = string.Empty;
}
