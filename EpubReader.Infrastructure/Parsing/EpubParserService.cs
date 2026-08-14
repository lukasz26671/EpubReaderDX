using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EpubReader.Application.Helpers;
using EpubReader.Application.Interfaces;
using EpubReader.Domain.Entities;

namespace EpubReader.Infrastructure.Parsing;

public sealed class EpubParserService : IEpubParserService
{
    public Task<EpubBook> ParseAsync(Stream stream, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        Stream archiveStream = stream;
        MemoryStream? owned = null;
        if (!stream.CanSeek)
        {
            owned = new MemoryStream();
            stream.CopyTo(owned);
            owned.Position = 0;
            archiveStream = owned;
        }

        try
        {
            using var zip = new System.IO.Compression.ZipArchive(
                archiveStream,
                System.IO.Compression.ZipArchiveMode.Read,
                leaveOpen: owned is null && stream.CanSeek);
            var book = ParseZip(zip, sourceName, cancellationToken);
            return Task.FromResult(book);
        }
        finally
        {
            owned?.Dispose();
        }
    }

    private static EpubBook ParseZip(System.IO.Compression.ZipArchive zip, string? sourceName, CancellationToken cancellationToken)
    {
        var book = new EpubBook { FilePath = sourceName ?? string.Empty };

        var opfPath = LocateOpfFilePath(zip);
        book.OpfDirectory = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') ?? string.Empty;
        if (!string.IsNullOrEmpty(book.OpfDirectory) && !book.OpfDirectory.EndsWith('/'))
        {
            book.OpfDirectory += "/";
        }

        var opfEntry = FindEntry(zip, opfPath) ?? throw new InvalidDataException($"OPF file missing at {opfPath}");
        XDocument opfDoc;
        using (var opfStream = opfEntry.Open())
        {
            opfDoc = XDocument.Load(opfStream);
        }

        ParseMetadata(opfDoc, book.Metadata);
        ParseManifest(opfDoc, book.Manifest, book.OpfDirectory);
        ParseSpine(opfDoc, book.Spine);
        LoadResources(zip, book, cancellationToken);
        ParseTableOfContents(zip, opfDoc, book);
        ParseChapters(zip, book, cancellationToken);
        BindTocChapterIndices(book);
        ResolveCover(book);
        return book;
    }

    private static string LocateOpfFilePath(System.IO.Compression.ZipArchive zip)
    {
        var containerEntry = FindEntry(zip, "META-INF/container.xml")
            ?? throw new InvalidDataException("Invalid EPUB: META-INF/container.xml not found.");

        using var stream = containerEntry.Open();
        var doc = XDocument.Load(stream);
        var rootfile = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "rootfile");
        var fullPathAttr = rootfile?.Attribute("full-path")?.Value;
        if (string.IsNullOrWhiteSpace(fullPathAttr))
        {
            throw new InvalidDataException("container.xml does not specify a full-path for rootfile.");
        }

        return fullPathAttr.Replace('\\', '/');
    }

    private static void ParseMetadata(XDocument opfDoc, EpubMetadata metadata)
    {
        var metadataElem = opfDoc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
        if (metadataElem is null) return;

        foreach (var elem in metadataElem.Elements())
        {
            switch (elem.Name.LocalName.ToLowerInvariant())
            {
                case "title":
                    if (string.IsNullOrWhiteSpace(metadata.Title) || metadata.Title == "Unknown Title")
                        metadata.Title = elem.Value.Trim();
                    break;
                case "creator":
                    if (!string.IsNullOrWhiteSpace(elem.Value))
                        metadata.Authors.Add(elem.Value.Trim());
                    break;
                case "language":
                    metadata.Language = elem.Value.Trim();
                    break;
                case "publisher":
                    metadata.Publisher = elem.Value.Trim();
                    break;
                case "description":
                    metadata.Description = elem.Value.Trim();
                    break;
                case "identifier":
                    if (string.IsNullOrWhiteSpace(metadata.Identifier))
                        metadata.Identifier = elem.Value.Trim();
                    break;
                case "rights":
                    metadata.Rights = elem.Value.Trim();
                    break;
                case "meta":
                    var nameAttr = elem.Attribute("name")?.Value;
                    var contentAttr = elem.Attribute("content")?.Value;
                    var property = elem.Attribute("property")?.Value;
                    if (nameAttr == "cover" && !string.IsNullOrEmpty(contentAttr))
                    {
                        metadata.CoverHref = contentAttr;
                    }
                    else if (property == "cover-image" && !string.IsNullOrEmpty(elem.Value))
                    {
                        metadata.CoverHref = elem.Value.Trim();
                    }
                    break;
            }
        }
    }

    private static void ParseManifest(XDocument opfDoc, Dictionary<string, EpubManifestItem> manifest, string opfDir)
    {
        var manifestElem = opfDoc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "manifest");
        if (manifestElem is null) return;

        foreach (var item in manifestElem.Elements().Where(e => e.Name.LocalName == "item"))
        {
            var id = item.Attribute("id")?.Value;
            var href = item.Attribute("href")?.Value;
            var mediaType = item.Attribute("media-type")?.Value ?? string.Empty;
            var properties = item.Attribute("properties")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(href)) continue;

            manifest[id] = new EpubManifestItem
            {
                Id = id,
                Href = href,
                MediaType = mediaType,
                Properties = properties,
                FullPath = CombinePaths(opfDir, href)
            };
        }
    }

    private static void ParseSpine(XDocument opfDoc, List<string> spine)
    {
        var spineElem = opfDoc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "spine");
        if (spineElem is null) return;

        foreach (var itemref in spineElem.Elements().Where(e => e.Name.LocalName == "itemref"))
        {
            var idref = itemref.Attribute("idref")?.Value;
            if (!string.IsNullOrEmpty(idref))
            {
                spine.Add(idref);
            }
        }
    }

    private static void LoadResources(System.IO.Compression.ZipArchive zip, EpubBook book, CancellationToken cancellationToken)
    {
        foreach (var item in book.Manifest.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsDocument(item) || IsNcx(item)) continue;

            var entry = FindEntry(zip, item.FullPath);
            if (entry is null) continue;

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            book.Resources[item.FullPath] = new EpubResource
            {
                FullPath = item.FullPath,
                MediaType = item.MediaType,
                Data = ms.ToArray()
            };
        }

        var cssBuilder = new StringBuilder();
        foreach (var item in book.Manifest.Values.Where(i =>
                     i.MediaType.Contains("css", StringComparison.OrdinalIgnoreCase) ||
                     i.Href.EndsWith(".css", StringComparison.OrdinalIgnoreCase)))
        {
            if (!book.Resources.TryGetValue(item.FullPath, out var resource)) continue;
            var css = Encoding.UTF8.GetString(resource.Data);
            cssBuilder.AppendLine(StripAuthorFonts(RewriteCssUrls(css, item.FullPath, book)));
        }

        book.BundledCss = cssBuilder.ToString();
    }

    /// <summary>
    /// Remove author font-family / color so reader theme + typography settings apply.
    /// </summary>
    private static string StripAuthorFonts(string css)
    {
        css = Regex.Replace(css, @"font-family\s*:\s*[^;{}]+;?", string.Empty, RegexOptions.IgnoreCase);
        css = Regex.Replace(css, @"\bcolor\s*:\s*[^;{}]+;?", string.Empty, RegexOptions.IgnoreCase);
        css = Regex.Replace(css, @"background(?:-color)?\s*:\s*[^;{}]+;?", string.Empty, RegexOptions.IgnoreCase);
        return css;
    }

    private static string RewriteCssUrls(string css, string cssFullPath, EpubBook book)
    {
        return Regex.Replace(css, @"url\(\s*(['""]?)([^)'""]+)\1\s*\)", match =>
        {
            var raw = match.Groups[2].Value.Trim();
            if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || EpubHref.IsExternal(raw))
            {
                return match.Value;
            }

            var resolved = EpubHref.Combine(cssFullPath, raw);
            if (!book.Resources.TryGetValue(resolved, out var resource))
            {
                var file = Path.GetFileName(resolved);
                resource = book.Resources.Values.FirstOrDefault(r =>
                    string.Equals(Path.GetFileName(r.FullPath), file, StringComparison.OrdinalIgnoreCase));
            }

            if (resource is null || resource.Data.Length == 0)
            {
                return match.Value;
            }

            var mime = string.IsNullOrWhiteSpace(resource.MediaType) ? "application/octet-stream" : resource.MediaType;
            return $"url(data:{mime};base64,{Convert.ToBase64String(resource.Data)})";
        }, RegexOptions.IgnoreCase);
    }

    private static void ParseTableOfContents(System.IO.Compression.ZipArchive zip, XDocument opfDoc, EpubBook book)
    {
        if (TryParseNavDocument(zip, book))
        {
            return;
        }

        var spineElem = opfDoc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "spine");
        var ncxId = spineElem?.Attribute("toc")?.Value;
        if (string.IsNullOrEmpty(ncxId) || !book.Manifest.TryGetValue(ncxId, out var ncxItem))
        {
            ncxItem = book.Manifest.Values.FirstOrDefault(IsNcx);
            if (ncxItem is null) return;
        }

        var ncxEntry = FindEntry(zip, ncxItem.FullPath);
        if (ncxEntry is null) return;

        using var stream = ncxEntry.Open();
        var ncxDoc = XDocument.Load(stream);
        var navMap = ncxDoc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "navMap");
        if (navMap is null) return;

        foreach (var navPoint in navMap.Elements().Where(e => e.Name.LocalName == "navPoint"))
        {
            book.TableOfContents.Add(ParseNcxNavPoint(navPoint));
        }
    }

    private static bool TryParseNavDocument(System.IO.Compression.ZipArchive zip, EpubBook book)
    {
        var navItem = book.Manifest.Values.FirstOrDefault(i =>
            i.Properties.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains("nav", StringComparer.OrdinalIgnoreCase));
        if (navItem is null) return false;

        var entry = FindEntry(zip, navItem.FullPath);
        if (entry is null) return false;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);

        // Prefer epub:type=toc when multiple navs exist
        var tocNav = doc.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "nav" &&
            e.Attributes().Any(a => a.Name.LocalName == "type" &&
                                    a.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                        .Contains("toc", StringComparer.OrdinalIgnoreCase)))
            ?? doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "nav");

        if (tocNav is null) return false;

        var ol = tocNav.Elements().FirstOrDefault(e => e.Name.LocalName == "ol");
        if (ol is null) return false;

        foreach (var li in ol.Elements().Where(e => e.Name.LocalName == "li"))
        {
            var node = ParseNavLi(li);
            if (node is not null) book.TableOfContents.Add(node);
        }

        return book.TableOfContents.Count > 0;
    }

    private static EpubTocNode? ParseNavLi(XElement li)
    {
        var anchor = li.Elements().FirstOrDefault(e => e.Name.LocalName == "a");
        var span = li.Elements().FirstOrDefault(e => e.Name.LocalName == "span");
        var title = (anchor?.Value ?? span?.Value ?? "Untitled").Trim();
        if (string.IsNullOrWhiteSpace(title)) title = "Untitled";

        var href = anchor?.Attribute("href")?.Value ?? string.Empty;
        var node = new EpubTocNode { Title = title, Href = href };

        var childOl = li.Elements().FirstOrDefault(e => e.Name.LocalName == "ol");
        if (childOl is not null)
        {
            foreach (var childLi in childOl.Elements().Where(e => e.Name.LocalName == "li"))
            {
                var child = ParseNavLi(childLi);
                if (child is not null) node.Children.Add(child);
            }
        }

        return node;
    }

    private static EpubTocNode ParseNcxNavPoint(XElement navPoint)
    {
        var label = navPoint.Elements().FirstOrDefault(e => e.Name.LocalName == "navLabel")
            ?.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value ?? "Untitled Section";
        var contentSrc = navPoint.Elements().FirstOrDefault(e => e.Name.LocalName == "content")
            ?.Attribute("src")?.Value ?? string.Empty;

        var node = new EpubTocNode { Title = label.Trim(), Href = contentSrc };
        foreach (var childPoint in navPoint.Elements().Where(e => e.Name.LocalName == "navPoint"))
        {
            node.Children.Add(ParseNcxNavPoint(childPoint));
        }

        return node;
    }

    private static void ParseChapters(System.IO.Compression.ZipArchive zip, EpubBook book, CancellationToken cancellationToken)
    {
        var chapterIndex = 1;
        foreach (var idref in book.Spine)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!book.Manifest.TryGetValue(idref, out var manifestItem)) continue;
            if (IsNcx(manifestItem) || IsNav(manifestItem)) continue;
            if (!IsDocument(manifestItem)) continue;

            var entry = FindEntry(zip, manifestItem.FullPath);
            if (entry is null) continue;

            string rawHtml;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                rawHtml = reader.ReadToEnd();
            }

            var chapterTitle = FindTitleForChapter(manifestItem.Href, manifestItem.FullPath, book.TableOfContents)
                ?? ExtractHtmlTitle(rawHtml)
                ?? $"Chapter {chapterIndex}";

            var chapter = new EpubChapter
            {
                Index = chapterIndex++,
                Id = idref,
                Title = chapterTitle,
                Href = manifestItem.Href,
                FullPath = manifestItem.FullPath,
                RawHtml = rawHtml,
                PlainText = HtmlToPlainTextConverter.Convert(rawHtml),
                Language = ExtractHtmlLang(rawHtml)
            };
            chapter.FormattedLines = HtmlToPlainTextConverter.WrapLines(chapter.PlainText, 100);
            chapter.DisplayHtml = ChapterHtmlProcessor.BuildDisplayHtml(chapter, book);
            book.Chapters.Add(chapter);
        }

        if (book.TableOfContents.Count == 0)
        {
            foreach (var chapter in book.Chapters)
            {
                book.TableOfContents.Add(new EpubTocNode
                {
                    Title = chapter.Title,
                    Href = chapter.Href,
                    ChapterIndex = chapter.Index - 1
                });
            }
        }
    }

    private static void BindTocChapterIndices(EpubBook book)
    {
        void Walk(List<EpubTocNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.ChapterIndex = ResolveChapterIndex(book, node.Href);
                if (node.Children.Count > 0) Walk(node.Children);
            }
        }

        Walk(book.TableOfContents);
    }

    private static int? ResolveChapterIndex(EpubBook book, string? href)
    {
        var path = EpubHref.Normalize(EpubHref.StripFragment(href));
        if (string.IsNullOrEmpty(path)) return null;

        for (var i = 0; i < book.Chapters.Count; i++)
        {
            var chapter = book.Chapters[i];
            if (EpubHref.PathsMatch(chapter.Href, path) || EpubHref.PathsMatch(chapter.FullPath, path)
                || EpubHref.PathsMatch(EpubHref.Combine(book.OpfDirectory, path), chapter.FullPath))
            {
                return i;
            }
        }

        return null;
    }

    private static void ResolveCover(EpubBook book)
    {
        EpubManifestItem? coverItem = null;
        if (!string.IsNullOrWhiteSpace(book.Metadata.CoverHref))
        {
            if (book.Manifest.TryGetValue(book.Metadata.CoverHref, out var byId))
            {
                coverItem = byId;
            }
            else
            {
                coverItem = book.Manifest.Values.FirstOrDefault(i =>
                    EpubHref.PathsMatch(i.Href, book.Metadata.CoverHref) ||
                    EpubHref.PathsMatch(i.Id, book.Metadata.CoverHref));
            }
        }

        coverItem ??= book.Manifest.Values.FirstOrDefault(i =>
            i.Properties.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains("cover-image", StringComparer.OrdinalIgnoreCase));

        coverItem ??= book.Manifest.Values.FirstOrDefault(i =>
            i.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            i.Id.Contains("cover", StringComparison.OrdinalIgnoreCase));

        if (coverItem is null) return;

        book.Metadata.CoverPath = coverItem.FullPath;
        if (book.Resources.TryGetValue(coverItem.FullPath, out var resource) && resource.Data.Length > 0)
        {
            var mime = string.IsNullOrWhiteSpace(resource.MediaType) ? "image/jpeg" : resource.MediaType;
            book.CoverDataUri = $"data:{mime};base64,{Convert.ToBase64String(resource.Data)}";
        }
    }

    private static string? FindTitleForChapter(string href, string fullPath, List<EpubTocNode> tocNodes)
    {
        string? best = null;
        var bestDepth = -1;

        void Walk(List<EpubTocNode> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.Href))
                {
                    var nodePath = EpubHref.Normalize(node.Href);
                    if (EpubHref.PathsMatch(nodePath, href) || EpubHref.PathsMatch(nodePath, fullPath))
                    {
                        if (depth >= bestDepth)
                        {
                            best = node.Title;
                            bestDepth = depth;
                        }
                    }
                }

                if (node.Children.Count > 0) Walk(node.Children, depth + 1);
            }
        }

        Walk(tocNodes, 0);
        return best;
    }

    private static string? ExtractHtmlTitle(string html)
    {
        var titleMatch = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleMatch.Success && !string.IsNullOrWhiteSpace(titleMatch.Groups[1].Value))
        {
            return HtmlToPlainTextConverter.UnescapeHtml(titleMatch.Groups[1].Value.Trim());
        }

        var h1Match = Regex.Match(html, @"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (h1Match.Success && !string.IsNullOrWhiteSpace(h1Match.Groups[1].Value))
        {
            return HtmlToPlainTextConverter.Convert(h1Match.Groups[1].Value).Trim();
        }

        return null;
    }

    private static string? ExtractHtmlLang(string html)
    {
        var m = Regex.Match(
            html,
            @"<html\b[^>]*?(?:xml:lang|lang)\s*=\s*[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return null;
        var lang = m.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(lang) ? null : lang;
    }

    private static bool IsDocument(EpubManifestItem item) =>
        item.MediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
        || item.Href.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)
        || item.Href.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
        || item.Href.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

    private static bool IsNcx(EpubManifestItem item) =>
        item.MediaType.Contains("ncx", StringComparison.OrdinalIgnoreCase)
        || item.Href.EndsWith(".ncx", StringComparison.OrdinalIgnoreCase);

    private static bool IsNav(EpubManifestItem item) =>
        item.Properties.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("nav", StringComparer.OrdinalIgnoreCase);

    private static System.IO.Compression.ZipArchiveEntry? FindEntry(System.IO.Compression.ZipArchive zip, string path)
    {
        var normalized = path.Replace('\\', '/');
        var exact = zip.GetEntry(normalized);
        if (exact is not null) return exact;

        return zip.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string CombinePaths(string dir, string relativePath) => EpubHref.Combine(dir, relativePath);
}
