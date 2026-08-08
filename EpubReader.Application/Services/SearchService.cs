using EpubReader.Application.Helpers;
using EpubReader.Application.Interfaces;
using EpubReader.Application.Models;
using EpubReader.Domain.Entities;

namespace EpubReader.Application.Services;

public sealed class SearchService : ISearchService
{
    public IReadOnlyList<SearchHit> Search(EpubBook book, string query, int maxResults = 20)
    {
        var results = new List<SearchHit>();
        if (book is null || string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        for (var chapterIndex = 0; chapterIndex < book.Chapters.Count; chapterIndex++)
        {
            var chapter = book.Chapters[chapterIndex];
            if (chapter.FormattedLines.Count == 0 && !string.IsNullOrEmpty(chapter.PlainText))
            {
                chapter.FormattedLines = HtmlToPlainTextConverter.WrapLines(chapter.PlainText, 100);
            }

            for (var lineIndex = 0; lineIndex < chapter.FormattedLines.Count; lineIndex++)
            {
                var line = chapter.FormattedLines[lineIndex];
                var offset = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (offset < 0)
                {
                    continue;
                }

                results.Add(new SearchHit
                {
                    ChapterIndex = chapterIndex,
                    Title = chapter.Title,
                    Excerpt = line.Trim(),
                    Offset = offset
                });

                if (results.Count >= maxResults)
                {
                    return results;
                }
            }
        }

        return results;
    }
}
