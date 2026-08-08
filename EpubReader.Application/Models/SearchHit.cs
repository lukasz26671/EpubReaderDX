namespace EpubReader.Application.Models;

public sealed class SearchHit
{
    public int ChapterIndex { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public int Offset { get; init; }
}
