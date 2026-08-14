namespace EpubReader.Domain.Entities;

public sealed class ReadingPosition
{
    public int ChapterIndex { get; set; }
    public double ScrollRatio { get; set; }
    /// <summary>Last TTS chunk within the chapter (0-based).</summary>
    public int TtsChunkIndex { get; set; }
}
