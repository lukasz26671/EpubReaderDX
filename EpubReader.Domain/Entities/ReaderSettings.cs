using EpubReader.Domain.Enums;

namespace EpubReader.Domain.Entities;

public class ReaderSettings
{
    public ReaderTheme Theme { get; set; } = ReaderTheme.Light;
    public FontFamilyOption FontFamily { get; set; } = FontFamilyOption.Serif;
    public int FontSize { get; set; } = 18;
    public double LineHeight { get; set; } = 1.7;
    public double LetterSpacing { get; set; } = 0;
    public double ParagraphSpacing { get; set; } = 1.25;
    public TextAlignOption TextAlign { get; set; } = TextAlignOption.Justify;
    public int ContentWidth { get; set; } = 42; // rem-ish: max-width in ch units roughly via rem
    public int PageMargin { get; set; } = 16; // px padding multiplier-ish

    public TtsEngineKind TtsEngine { get; set; } = TtsEngineKind.Auto;
    public double TtsRate { get; set; } = 1.0;
    /// <summary>BCP-47 override; null/empty = auto-detect.</summary>
    public string? TtsLanguageOverride { get; set; }
    /// <summary>Piper voice id, e.g. en_US-lessac-high. Null = default.</summary>
    public string? PiperVoiceId { get; set; }

    public UiLanguageKind UiLanguage { get; set; } = UiLanguageKind.Auto;
}
