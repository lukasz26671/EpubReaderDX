namespace EpubReader.Domain.Entities;

public sealed class ReadingPosition
{
    public int ChapterIndex { get; set; }
    public double ScrollRatio { get; set; }
}
