namespace EpubReader.Domain.Entities;

public class EpubTocNode
{
    public string Title { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public int? ChapterIndex { get; set; }
    public List<EpubTocNode> Children { get; set; } = [];
}
