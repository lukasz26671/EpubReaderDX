using System.Text.RegularExpressions;

namespace EpubReader.Application.Helpers;

/// <summary>
/// Cleans chapter plain text for TTS: drop decorative markers, keep paragraph structure.
/// </summary>
public static class TtsSpeechNormalizer
{
    private static readonly Regex Asterisks = new(@"\*+", RegexOptions.Compiled);
    private static readonly Regex MultiNewlines = new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>
    /// Removes asterisks (so voices don't say "asterisk") and normalizes newlines.
    /// Keeps blank lines between paragraphs for pause handling downstream.
    /// </summary>
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var s = text.Replace("\r\n", "\n").Replace('\r', '\n');
        s = Asterisks.Replace(s, string.Empty);
        s = MultiNewlines.Replace(s, "\n\n");
        return s.Trim();
    }
}
