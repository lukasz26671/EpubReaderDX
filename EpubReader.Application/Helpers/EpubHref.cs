using System.Text.RegularExpressions;

namespace EpubReader.Application.Helpers;

public static class EpubHref
{
    public static string StripFragment(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return string.Empty;
        var hash = href.IndexOf('#');
        return hash >= 0 ? href[..hash] : href;
    }

    public static string? GetFragment(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        var hash = href.IndexOf('#');
        if (hash < 0 || hash == href.Length - 1) return null;
        return Uri.UnescapeDataString(href[(hash + 1)..]);
    }

    public static string Normalize(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return string.Empty;
        var value = StripFragment(href).Replace('\\', '/').Trim();
        while (value.StartsWith("./", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        return value.TrimStart('/');
    }

    public static string Combine(string baseDirOrFile, string relative)
    {
        relative = relative.Replace('\\', '/');
        if (relative.StartsWith('/') || Regex.IsMatch(relative, @"^[a-zA-Z][a-zA-Z0-9+.-]*:"))
        {
            return Normalize(relative.TrimStart('/'));
        }

        var basePath = baseDirOrFile.Replace('\\', '/');
        if (!basePath.EndsWith('/') && basePath.Contains('.'))
        {
            var slash = basePath.LastIndexOf('/');
            basePath = slash >= 0 ? basePath[..(slash + 1)] : string.Empty;
        }
        else if (!string.IsNullOrEmpty(basePath) && !basePath.EndsWith('/'))
        {
            basePath += "/";
        }

        return Normalize(ResolveDots(basePath + relative));
    }

    public static bool PathsMatch(string left, string right)
    {
        left = Normalize(left);
        right = Normalize(right);
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return true;
        return left.EndsWith('/' + right, StringComparison.OrdinalIgnoreCase)
               || right.EndsWith('/' + left, StringComparison.OrdinalIgnoreCase)
               || string.Equals(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExternal(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return false;
        return Regex.IsMatch(href, @"^(https?|mailto|ftp):", RegexOptions.IgnoreCase);
    }

    private static string ResolveDots(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        var stack = new List<string>();
        foreach (var part in parts)
        {
            if (part is "" or ".") continue;
            if (part == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(part);
        }

        return string.Join('/', stack);
    }
}
