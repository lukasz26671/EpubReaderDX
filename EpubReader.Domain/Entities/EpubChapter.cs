namespace EpubReader.Domain.Entities;

public class EpubChapter
{
    public int Index { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string RawHtml { get; set; } = string.Empty;
    public string DisplayHtml { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    /// <summary>BCP-47 from the chapter html/xml:lang when present.</summary>
    public string? Language { get; set; }
    public List<string> FormattedLines { get; set; } = [];
}
