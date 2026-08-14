using System.Text.RegularExpressions;

namespace EpubReader.Application.Helpers;

public static class TtsTextChunker
{
    private static readonly Regex SentenceSplit = new(
        @"(?<=[.!?…。！？])\s+",
        RegexOptions.Compiled);

    /// <summary>
    /// Packs whole sentences into utterances so highlight == what is being read.
    /// When <paramref name="breakOnParagraph"/> is false (Edge/Piper), paragraphs are packed
    /// together until <paramref name="maxChars"/> — fewer synth round-trips.
    /// </summary>
    public static IReadOnlyList<string> Chunk(string text, int maxChars = 280, bool breakOnParagraph = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var normalized = TtsSpeechNormalizer.Sanitize(text);
        if (normalized.Length == 0) return [];

        var sentences = SplitSentences(normalized);
        if (sentences.Count == 0) return [];

        var chunks = new List<string>();
        var current = new List<string>();
        var len = 0;

        void Flush(bool paragraphTail)
        {
            if (current.Count == 0) return;
            var body = string.Join(" ", current).Trim();
            current.Clear();
            len = 0;
            if (body.Length == 0) return;
            if (paragraphTail) body += "\n\n";
            chunks.Add(body);
        }

        foreach (var (sentence, afterParagraph) in sentences)
        {
            var s = sentence.Trim();
            if (s.Length == 0)
            {
                if (afterParagraph && breakOnParagraph) Flush(paragraphTail: true);
                continue;
            }

            if (s.Length > maxChars)
            {
                Flush(paragraphTail: false);
                foreach (var piece in SplitLong(s, maxChars))
                    chunks.Add(piece);
                if (afterParagraph && breakOnParagraph && chunks.Count > 0 && !chunks[^1].EndsWith("\n\n", StringComparison.Ordinal))
                    chunks[^1] += "\n\n";
                continue;
            }

            var joinLen = afterParagraph && !breakOnParagraph ? 2 : 1; // "\n\n" vs " "
            if (len > 0 && len + joinLen + s.Length > maxChars)
                Flush(paragraphTail: false);

            if (len > 0 && afterParagraph && !breakOnParagraph)
            {
                // Soft paragraph break inside the same synth request.
                current[^1] = current[^1].TrimEnd() + "\n\n" + s;
                len += 2 + s.Length;
            }
            else
            {
                current.Add(s);
                len += (len == 0 ? 0 : 1) + s.Length;
            }

            if (afterParagraph && breakOnParagraph)
                Flush(paragraphTail: true);
        }

        Flush(paragraphTail: false);
        return chunks;
    }

    private static List<(string Sentence, bool AfterParagraph)> SplitSentences(string text)
    {
        var result = new List<(string, bool)>();
        var blocks = Regex.Split(text.Trim(), @"\n\s*\n+");
        for (var b = 0; b < blocks.Length; b++)
        {
            var block = blocks[b].Trim();
            if (block.Length == 0) continue;

            var sentences = SentenceSplit.Split(block)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            for (var i = 0; i < sentences.Count; i++)
            {
                var afterPara = i == sentences.Count - 1 && b < blocks.Length - 1;
                result.Add((sentences[i], afterPara));
            }
        }

        return result;
    }

    private static IEnumerable<string> SplitLong(string sentence, int maxChars)
    {
        var start = 0;
        while (start < sentence.Length)
        {
            while (start < sentence.Length && char.IsWhiteSpace(sentence[start]))
                start++;
            if (start >= sentence.Length) yield break;

            var end = Math.Min(start + maxChars, sentence.Length);
            if (end < sentence.Length)
            {
                var slice = sentence.AsSpan(start, end - start);
                var breakAt = LastSoftBreak(slice);
                if (breakAt > maxChars / 3)
                    end = start + breakAt;
            }

            var piece = sentence[start..end].Trim();
            if (piece.Length > 0) yield return piece;
            start = end;
        }
    }

    private static int LastSoftBreak(ReadOnlySpan<char> slice)
    {
        for (var i = slice.Length - 1; i >= 0; i--)
        {
            var c = slice[i];
            if (c is ',' or ';' or ':' or '，' or '；' or '：' or '、' or ' ' or '\n')
                return i + 1;
        }

        return slice.Length;
    }
}
