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
    /// <summary>Restore scroll ratio when reopening a book/chapter.</summary>
    public bool RestoreScrollPosition { get; set; } = true;
    /// <summary>Resume TTS from the last saved chunk in the chapter.</summary>
    public bool RestoreTtsPosition { get; set; } = true;
    /// <summary>After TTS finishes a chapter, advance to the next one.</summary>
    public bool TtsAutoNextChapter { get; set; }
    /// <summary>After auto-next (or when AutoPlay alone finishes), start reading the next chapter.</summary>
    public bool TtsAutoPlay { get; set; }
    /// <summary>Overscroll a large pad at chapter end/start to go next/previous.</summary>
    public bool InfiniteScroll { get; set; } = true;

    public UiLanguageKind UiLanguage { get; set; } = UiLanguageKind.Auto;
}
