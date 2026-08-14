namespace EpubReader.Application.Interfaces;

public interface ILanguageDetector
{
    /// <summary>
    /// Returns a BCP-47 language tag (e.g. pl, en, zh-CN, zh-TW).
    /// </summary>
    string Detect(string? metadataLanguage, string? plainText);

    /// <summary>
    /// Language of a short snippet. Ambiguous tokens keep <paramref name="fallback"/> (usually the chapter language).
    /// </summary>
    string DetectLocal(string? text, string fallback);

    /// <summary>
    /// Splits chunks into speakable runs when Auto is on, so a lone English line in a Polish chapter gets an English voice.
    /// </summary>
    IReadOnlyList<(string Text, string Language)> LabelUtterances(IReadOnlyList<string> chunks, string fallback);
}
