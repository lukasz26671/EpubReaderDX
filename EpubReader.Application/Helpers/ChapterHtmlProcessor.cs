using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using EpubReader.Domain.Entities;

namespace EpubReader.Application.Helpers;

public static class ChapterHtmlProcessor
{
    private static readonly Regex ScriptRegex = new(@"<script\b[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StyleBlockRegex = new(@"<style\b[^>]*>[\s\S]*?</style>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EventAttrRegex = new(@"\son[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BodyRegex = new(@"<body\b[^>]*>([\s\S]*)</body>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ImgRegex = new(@"<img\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SvgImageRegex = new(@"<(?:image|svg:image)\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AnchorRegex = new(@"<a\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AttrRegex = new(@"([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.Compiled);
    private static readonly Regex LinkStylesheetRegex = new(@"<link\b[^>]*rel\s*=\s*[""']?stylesheet[""']?[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string BuildDisplayHtml(EpubChapter chapter, EpubBook book)
    {
        var html = chapter.RawHtml ?? string.Empty;
        html = ScriptRegex.Replace(html, string.Empty);
        html = LinkStylesheetRegex.Replace(html, string.Empty);
        html = EventAttrRegex.Replace(html, string.Empty);

        var bodyMatch = BodyRegex.Match(html);
        var content = bodyMatch.Success ? bodyMatch.Groups[1].Value : StripOuter(html);

        var localCss = new StringBuilder();
        foreach (Match style in StyleBlockRegex.Matches(html))
        {
            localCss.AppendLine(style.Value);
        }

        content = StyleBlockRegex.Replace(content, string.Empty);
        content = RewriteImages(content, chapter.FullPath, book);
        content = RewriteAnchors(content, chapter.FullPath);

        if (localCss.Length == 0)
        {
            return content;
        }

        return "<style>" + localCss + "</style>" + content;
    }

    private static string StripOuter(string html)
    {
        html = Regex.Replace(html, @"</?(html|head|body)[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<head\b[^>]*>[\s\S]*?</head>", string.Empty, RegexOptions.IgnoreCase);
        return html.Trim();
    }

    private static string RewriteImages(string html, string chapterFullPath, EpubBook book)
    {
        return ImgRegex.Replace(html, m => RewriteMediaTag("img", m.Groups[1].Value, chapterFullPath, book));
    }

    private static string RewriteMediaTag(string tag, string attrs, string chapterFullPath, EpubBook book)
    {
        var map = ParseAttributes(attrs);
        if (!map.TryGetValue("src", out var src) || string.IsNullOrWhiteSpace(src))
        {
            map.TryGetValue("xlink:href", out src);
        }

        if (string.IsNullOrWhiteSpace(src) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return $"<{tag}{RebuildAttributes(map)}>";
        }

        if (EpubHref.IsExternal(src))
        {
            return $"<{tag}{RebuildAttributes(map)}>";
        }

        var resolved = EpubHref.Combine(chapterFullPath, src);
        if (TryGetDataUri(book, resolved, out var dataUri))
        {
            map["src"] = dataUri;
            map.Remove("xlink:href");
        }

        return $"<{tag}{RebuildAttributes(map)}>";
    }

    private static string RewriteAnchors(string html, string chapterFullPath)
    {
        return AnchorRegex.Replace(html, m =>
        {
            var map = ParseAttributes(m.Groups[1].Value);
            if (!map.TryGetValue("href", out var href) || string.IsNullOrWhiteSpace(href))
            {
                return $"<a{RebuildAttributes(map)}>";
            }

            map["data-epub-href"] = href;
            map["data-epub-base"] = chapterFullPath;
            map["href"] = "#";
            map["draggable"] = "false";
            return $"<a{RebuildAttributes(map)}>";
        });
    }

    private static bool TryGetDataUri(EpubBook book, string fullPath, out string dataUri)
    {
        dataUri = string.Empty;
        if (!TryFindResource(book, fullPath, out var resource) || resource.Data.Length == 0)
        {
            return false;
        }

        var mime = string.IsNullOrWhiteSpace(resource.MediaType) ? GuessMime(fullPath) : resource.MediaType;
        dataUri = $"data:{mime};base64,{Convert.ToBase64String(resource.Data)}";
        return true;
    }

    private static bool TryFindResource(EpubBook book, string fullPath, out EpubResource resource)
    {
        if (book.Resources.TryGetValue(fullPath, out resource!))
        {
            return true;
        }

        var fileName = Path.GetFileName(fullPath);
        foreach (var pair in book.Resources)
        {
            if (string.Equals(Path.GetFileName(pair.Key), fileName, StringComparison.OrdinalIgnoreCase))
            {
                resource = pair.Value;
                return true;
            }
        }

        resource = null!;
        return false;
    }

    private static string GuessMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".css" => "text/css",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream"
    };

    private static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttrRegex.Matches(attrs))
        {
            var name = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim();
            if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            map[name] = WebUtility.HtmlDecode(value);
        }

        return map;
    }

    private static string RebuildAttributes(Dictionary<string, string> map)
    {
        if (map.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        foreach (var (key, value) in map)
        {
            sb.Append(' ')
                .Append(key)
                .Append("=\"")
                .Append(WebUtility.HtmlEncode(value))
                .Append('"');
        }

        return sb.ToString();
    }
}
