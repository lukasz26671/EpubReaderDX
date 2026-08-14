using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EpubReader.Application.Interfaces;

namespace EpubReader.Application.Services;

public sealed class LanguageDetector : ILanguageDetector
{
    // Frequent Simplified-only vs Traditional-only characters for Hans/Hant split.
    private static readonly HashSet<char> SimplifiedMarkers =
    [
        '国', '对', '会', '这', '来', '时', '为', '现', '说', '过', '发', '经', '还', '进', '从',
        '开', '关', '问', '里', '后', '没', '让', '应', '该', '样', '点', '种', '头', '体',
        '书', '长', '门', '间', '东', '车', '电', '话', '马', '风', '鸟', '龙', '齐', '飞', '习',
        '与', '专', '业', '两', '严', '丧', '个', '临', '丽', '举', '义', '乌', '乐', '乔',
        '买', '乱', '争', '于', '亏', '云', '亚', '产', '亩', '亲', '亿', '仅', '仓', '仪',
        '们', '价', '众', '优', '传', '伤', '伦', '伟', '伪', '余', '侠', '侣', '侥', '侦',
        '侧', '侨', '侩', '侪', '侬', '俦', '俨', '俩', '俭', '债', '倾', '偿', '傥', '储',
        '傩', '伫', '帅', '师', '帐', '帘', '帜', '带', '帧', '帮', '尝', '哑', '哟', '唤',
        '啧', '啬', '啭', '啮', '啸', '喷', '喽', '喾', '嗫', '嗳', '响', '哗', '员', '呗',
        '唠', '唢', '啰', '广', '庆', '应', '庐', '库', '厨', '厢', '厦', '寿', '将', '尔',
        '尘', '尝', '尧', '尴', '尸', '层', '屿', '岁', '岂', '岗', '崭', '嵘', '岭', '岳',
        '峡', '峰', '岛', '岚', '岩', '岿', '峦', '崭', '嵘', '岭', '条', '柜', '柠', '查',
        '标', '栈', '栋', '栏', '树', '栖', '样', '根', '格', '桃', '框', '案', '桌', '桐',
        '桑', '桥', '桨', '桩', '梦', '梧', '梨', '梯', '械', '梳', '检', '棂', '弃', '辇'
    ];

    private static readonly HashSet<char> TraditionalMarkers =
    [
        '國', '對', '會', '這', '來', '時', '為', '現', '說', '過', '發', '經', '還', '進', '從',
        '開', '關', '問', '裡', '後', '沒', '讓', '應', '該', '樣', '點', '種', '頭', '體',
        '書', '長', '門', '間', '東', '車', '電', '話', '馬', '風', '鳥', '龍', '齊', '飛', '習',
        '與', '專', '業', '兩', '嚴', '喪', '個', '臨', '麗', '舉', '義', '烏', '樂', '喬',
        '買', '亂', '爭', '於', '虧', '雲', '亞', '產', '畝', '親', '億', '僅', '倉', '儀',
        '們', '價', '眾', '優', '傳', '傷', '倫', '偉', '偽', '餘', '俠', '侶', '僥', '偵',
        '側', '僑', '儈', '儕', '儂', '儔', '儼', '倆', '儉', '債', '傾', '償', '儻', '儲',
        '儺', '佇', '帥', '師', '帳', '簾', '幟', '帶', '幀', '幫', '嘗', '啞', '喲', '喚',
        '嘖', '嗇', '囀', '齧', '嘯', '噴', '嘍', '嚳', '囁', '噯', '響', '嘩', '員', '唄',
        '嘮', '嗩', '囉', '廣', '慶', '廬', '庫', '廚', '廂', '廈', '壽', '將', '爾',
        '塵', '堯', '尷', '屍', '層', '嶼', '歲', '豈', '崗', '嶄', '嶸', '嶺', '嶽',
        '峽', '島', '嵐', '巖', '巋', '巒', '條', '櫃', '檸', '標', '棧', '棟', '欄', '樹',
        '棲', '橋', '槳', '樁', '夢', '梯', '械', '梳', '檢', '櫺', '棄', '輦', '臺', '灣',
        '麼', '著', '纔', '隻', '衝', '禦', '範', '註', '週', '萬', '與', '學', '覺', '觀'
    ];
    public string Detect(string? metadataLanguage, string? plainText)
    {
        var sample = TakeSample(plainText, 4000);
        var fromText = DetectFromText(sample);
        var fromMeta = NormalizeMetadata(metadataLanguage);

        if (fromText is not null)
        {
            // Prefer script-based zh-CN/zh-TW when metadata only says "zh".
            if (fromText.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                && (fromMeta is null || fromMeta.Equals("zh", StringComparison.OrdinalIgnoreCase)
                    || fromMeta.StartsWith("zh", StringComparison.OrdinalIgnoreCase)))
            {
                return fromText;
            }

            // Strong CJK/script signal wins over mismatched metadata.
            if (IsStrongScript(fromText))
                return fromText;
        }

        if (fromMeta is not null)
            return RefineChineseVariant(fromMeta, sample);

        return fromText ?? "en";
    }

    private static readonly HashSet<string> EnCue =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "why", "what", "who", "where", "when", "how", "the", "this", "these", "those",
            "is", "are", "was", "were", "have", "has", "will", "would", "could", "should",
            "hello", "thanks", "please", "because", "they", "them", "their", "yeah", "wow",
            "don't", "can't", "it's", "i'm", "you're", "we're", "isn't", "aren't", "that's",
            "what's", "with", "from", "your", "his", "her", "our", "english", "okay", "yes",
            "hello", "hey", "please", "thanks"
        };

    private static readonly HashSet<string> PlCue =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "tak", "nie", "się", "jest", "są", "był", "była", "było", "byli", "że", "czy",
            "jak", "ale", "tego", "tej", "tym", "będzie", "mogę", "może", "mogą", "już",
            "jeszcze", "bardzo", "tylko", "też", "przez", "również", "więc", "ponieważ",
            "które", "który", "która", "których", "proszę", "dziękuję", "cześć", "dzień",
            "polski", "polska", "lubię", "chcę", "gdzie", "kiedy", "dlaczego"
        };

    private static readonly HashSet<string> DeCue =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "und", "der", "die", "das", "ich", "nicht", "ist", "sind", "warum", "bitte",
            "danke", "ein", "eine", "nicht", "sie", "wir", "nicht"
        };

    private static readonly HashSet<string> EsCue =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "que", "los", "las", "una", "por", "para", "está", "son", "porque", "gracias",
            "hola", "está", "están", "también"
        };

    private static readonly HashSet<string> FrCue =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "les", "une", "des", "est", "sont", "pas", "pour", "dans", "que", "qui",
            "merci", "pourquoi", "bonjour", "avec", "mais"
        };

    public string DetectLocal(string? text, string fallback)
    {
        var sample = (text ?? string.Empty).Trim();
        if (sample.Length == 0) return fallback;

        var script = DetectSnippetScript(sample);
        if (script is not null && IsStrongScript(script))
            return script;

        if (script == "pl")
            return "pl";

        var (best, bestScore, secondScore) = ScoreCueWords(sample);
        var words = CountWords(sample);
        var letters = CountLetters(sample);

        // Whole-snippet cues: "Why" / "tak" are obvious; "no" is PL filler and EN negation → keep fallback.
        if (words <= 3 || letters <= 16)
        {
            if (IsAmbiguousShort(sample))
                return fallback;
            if (best is not null && bestScore >= 1 && bestScore > secondScore)
                return best;
            return fallback;
        }

        if (best is not null && bestScore >= 1 && bestScore > secondScore)
            return best;

        return script ?? fallback;
    }

    public IReadOnlyList<(string Text, string Language)> LabelUtterances(
        IReadOnlyList<string> chunks,
        string fallback)
    {
        if (chunks.Count == 0) return [];
        var fb = string.IsNullOrWhiteSpace(fallback) ? "en" : fallback;
        var result = new List<(string, string)>();

        foreach (var chunk in chunks)
        {
            var pieces = SplitSpeakable(chunk);
            string? runLang = null;
            var run = new List<string>();

            void Flush()
            {
                if (run.Count == 0 || runLang is null) return;
                result.Add((string.Join(" ", run).Trim(), runLang));
                run.Clear();
            }

            foreach (var piece in pieces)
            {
                var lang = DetectLocal(piece, fb);
                if (runLang is null)
                {
                    runLang = lang;
                    run.Add(piece);
                    continue;
                }

                if (lang == runLang)
                {
                    run.Add(piece);
                    continue;
                }

                Flush();
                runLang = lang;
                run.Add(piece);
            }

            Flush();
        }

        return result;
    }

    private static bool IsAmbiguousShort(string sample)
    {
        var t = sample.Trim().TrimEnd('.', '!', '?', '…', '"', '\'', '”', '“').Trim();
        return t.Equals("no", StringComparison.OrdinalIgnoreCase)
            || t.Equals("ok", StringComparison.OrdinalIgnoreCase)
            || t.Equals("a", StringComparison.OrdinalIgnoreCase)
            || t.Equals("i", StringComparison.OrdinalIgnoreCase)
            || t.Equals("to", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitSpeakable(string text)
    {
        var t = text.Trim();
        if (t.Length == 0) yield break;

        var paras = Regex.Split(t, @"\n\s*\n+");
        foreach (var para in paras)
        {
            var p = para.Trim();
            if (p.Length == 0) continue;
            var sentences = SentenceCueSplit.Split(p)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            if (sentences.Count == 0)
            {
                yield return p;
                continue;
            }

            foreach (var s in sentences)
                yield return s;
        }
    }

    private static readonly Regex SentenceCueSplit = new(
        @"(?<=[.!?…。！？])\s+",
        RegexOptions.Compiled);

    private static readonly Regex WordSplit = new(
        @"[^\p{L}]+",
        RegexOptions.Compiled);

    private static (string? Best, int BestScore, int SecondScore) ScoreCueWords(string sample)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in WordSplit.Split(sample))
        {
            if (raw.Length < 2) continue;
            var w = raw;
            if (EnCue.Contains(w)) scores["en"] = scores.GetValueOrDefault("en") + 1;
            if (PlCue.Contains(w)) scores["pl"] = scores.GetValueOrDefault("pl") + 1;
            if (DeCue.Contains(w)) scores["de"] = scores.GetValueOrDefault("de") + 1;
            if (EsCue.Contains(w)) scores["es"] = scores.GetValueOrDefault("es") + 1;
            if (FrCue.Contains(w)) scores["fr"] = scores.GetValueOrDefault("fr") + 1;
        }

        string? best = null;
        var bestScore = 0;
        var second = 0;
        foreach (var (lang, n) in scores)
        {
            if (n > bestScore)
            {
                second = bestScore;
                bestScore = n;
                best = lang;
            }
            else if (n > second)
            {
                second = n;
            }
        }

        return (best, bestScore, second);
    }

    private static int CountWords(string sample) =>
        WordSplit.Split(sample).Count(w => w.Length > 0);

    private static int CountLetters(string sample)
    {
        var n = 0;
        foreach (var r in sample.EnumerateRunes())
            if (Rune.IsLetter(r)) n++;
        return n;
    }

    /// <summary>Relaxed script detect for short snippets (1 CJK char is enough).</summary>
    private static string? DetectSnippetScript(string sample)
    {
        var han = 0;
        var hiraKata = 0;
        var hangul = 0;
        var cyrillic = 0;
        var arabic = 0;
        var latin = 0;
        var polish = 0;
        var letters = 0;

        foreach (var rune in sample.EnumerateRunes())
        {
            var v = rune.Value;
            if (IsHan(v)) { han++; letters++; continue; }
            if (IsHiraganaOrKatakana(v)) { hiraKata++; letters++; continue; }
            if (IsHangul(v)) { hangul++; letters++; continue; }
            if (IsCyrillic(v)) { cyrillic++; letters++; continue; }
            if (IsArabic(v)) { arabic++; letters++; continue; }
            if (Rune.IsLetter(rune))
            {
                letters++;
                latin++;
                var ch = rune.ToString();
                if ("ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(ch, StringComparison.Ordinal))
                    polish++;
            }
        }

        if (letters == 0) return null;
        if (hiraKata > 0) return "ja";
        if (hangul > 0) return "ko";
        if (han > 0 && hiraKata == 0 && hangul == 0) return DetectChineseVariant(sample);
        if (cyrillic > 0 && cyrillic >= latin) return "ru";
        if (arabic > 0 && arabic >= latin) return "ar";
        if (polish >= 1) return "pl";
        return null;
    }

    private static string? DetectFromText(string sample)
    {
        if (string.IsNullOrWhiteSpace(sample)) return null;

        var han = 0;
        var hiraKata = 0;
        var hangul = 0;
        var cyrillic = 0;
        var arabic = 0;
        var latin = 0;
        var polish = 0;
        var letters = 0;

        foreach (var rune in sample.EnumerateRunes())
        {
            var v = rune.Value;
            if (IsHan(v)) { han++; letters++; continue; }
            if (IsHiraganaOrKatakana(v)) { hiraKata++; letters++; continue; }
            if (IsHangul(v)) { hangul++; letters++; continue; }
            if (IsCyrillic(v)) { cyrillic++; letters++; continue; }
            if (IsArabic(v)) { arabic++; letters++; continue; }
            if (Rune.IsLetter(rune))
            {
                letters++;
                latin++;
                var ch = rune.ToString();
                if ("ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(ch, StringComparison.Ordinal))
                    polish++;
            }
        }

        if (letters == 0) return null;

        if (hiraKata > 8 || hiraKata > han * 0.15) return "ja";
        if (hangul > 8) return "ko";
        if (han > 12 || han > letters * 0.2) return DetectChineseVariant(sample);
        if (cyrillic > letters * 0.25) return "ru";
        if (arabic > letters * 0.25) return "ar";
        if (polish >= 3 || polish > latin * 0.02) return "pl";
        if (latin > 0) return "en";
        return null;
    }

    private static string DetectChineseVariant(string sample)
    {
        var simp = 0;
        var trad = 0;
        foreach (var ch in sample)
        {
            if (SimplifiedMarkers.Contains(ch)) simp++;
            if (TraditionalMarkers.Contains(ch)) trad++;
        }

        if (trad > simp * 1.15) return "zh-TW";
        if (simp > trad * 1.05) return "zh-CN";
        // Default mainland when ambiguous but clearly Chinese.
        return simp >= trad ? "zh-CN" : "zh-TW";
    }

    private static string RefineChineseVariant(string lang, string sample)
    {
        if (!lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return lang;

        if (lang.Contains("TW", StringComparison.OrdinalIgnoreCase)
            || lang.Contains("HK", StringComparison.OrdinalIgnoreCase)
            || lang.Contains("Hant", StringComparison.OrdinalIgnoreCase)
            || lang.Contains("MO", StringComparison.OrdinalIgnoreCase))
            return lang.Contains("HK", StringComparison.OrdinalIgnoreCase) ? "zh-HK" : "zh-TW";

        if (lang.Contains("CN", StringComparison.OrdinalIgnoreCase)
            || lang.Contains("Hans", StringComparison.OrdinalIgnoreCase)
            || lang.Contains("SG", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";

        if (!string.IsNullOrWhiteSpace(sample))
            return DetectChineseVariant(sample);

        return "zh-CN";
    }

    private static string? NormalizeMetadata(string? metadataLanguage)
    {
        if (string.IsNullOrWhiteSpace(metadataLanguage)) return null;
        var raw = metadataLanguage.Trim().Replace('_', '-');
        if (raw.Equals("und", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("zxx", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var culture = CultureInfo.GetCultureInfo(raw);
            var name = culture.Name;
            if (string.IsNullOrWhiteSpace(name)) return raw.ToLowerInvariant();

            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return RefineChineseVariant(name, string.Empty);

            // pl-PL → pl, en-US → en (keep region only for zh)
            var dash = name.IndexOf('-');
            return dash > 0 ? name[..dash].ToLowerInvariant() : name.ToLowerInvariant();
        }
        catch (CultureNotFoundException)
        {
            var dash = raw.IndexOf('-');
            var primary = (dash > 0 ? raw[..dash] : raw).ToLowerInvariant();
            return string.IsNullOrWhiteSpace(primary) ? null : primary;
        }
    }

    private static string TakeSample(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var t = text.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static bool IsStrongScript(string lang) =>
        lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
        || lang is "ja" or "ko" or "ar" or "ru";

    private static bool IsHan(int v) =>
        (v is >= 0x4E00 and <= 0x9FFF)
        || (v is >= 0x3400 and <= 0x4DBF)
        || (v is >= 0xF900 and <= 0xFAFF)
        || (v is >= 0x20000 and <= 0x2A6DF);

    private static bool IsHiraganaOrKatakana(int v) =>
        (v is >= 0x3040 and <= 0x309F) || (v is >= 0x30A0 and <= 0x30FF);

    private static bool IsHangul(int v) =>
        (v is >= 0xAC00 and <= 0xD7AF) || (v is >= 0x1100 and <= 0x11FF);

    private static bool IsCyrillic(int v) => v is >= 0x0400 and <= 0x04FF;
    private static bool IsArabic(int v) => v is >= 0x0600 and <= 0x06FF;
}
