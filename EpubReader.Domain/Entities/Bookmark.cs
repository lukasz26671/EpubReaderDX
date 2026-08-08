namespace EpubReader.Domain.Entities;

public class Bookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ChapterIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
