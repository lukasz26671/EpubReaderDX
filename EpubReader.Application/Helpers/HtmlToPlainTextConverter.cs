using System.Text;
using System.Text.RegularExpressions;

namespace EpubReader.Application.Helpers;

public static class HtmlToPlainTextConverter
{
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        html = Regex.Replace(html, @"<(script|style)[^>]*?>.*?</\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<(p|div|h1|h2|h3|h4|h5|h6|li|tr|blockquote)[^>]*>", "\n\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<hr\s*/?>", "\n--------------------------------------------------\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", string.Empty);
        html = UnescapeHtml(html);
        html = Regex.Replace(html, @"\n\s*\n\s*\n+", "\n\n");
        return html.Trim();
    }

    public static string UnescapeHtml(string str)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        return str.Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&mdash;", "—")
            .Replace("&ndash;", "–")
            .Replace("&hellip;", "…");
    }

    public static List<string> WrapLines(string text, int width)
    {
        var result = new List<string>();
        if (width <= 10) width = 80;

        string[] rawParagraphs = text.Split(["\n\n"], StringSplitOptions.None);

        foreach (var para in rawParagraphs)
        {
            string cleanPara = Regex.Replace(para.Replace("\n", " "), @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(cleanPara))
            {
                result.Add(string.Empty);
                continue;
            }

            string[] words = cleanPara.Split(' ');
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                if (currentLine.Length + word.Length + 1 > width)
                {
                    if (currentLine.Length > 0)
                    {
                        result.Add(currentLine.ToString());
                        currentLine.Clear();
                    }

                    if (word.Length > width)
                    {
                        result.Add(word[..width]);
                        continue;
                    }
                }

                if (currentLine.Length > 0) currentLine.Append(' ');
                currentLine.Append(word);
            }

            if (currentLine.Length > 0)
            {
                result.Add(currentLine.ToString());
            }

            result.Add(string.Empty);
        }

        return result;
    }

    public static string StripOuterDocument(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var cleaned = Regex.Replace(html, @"</?(html|body)[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }
}
