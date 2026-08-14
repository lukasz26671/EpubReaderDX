namespace EpubReader.Application.Interfaces;

public interface ILanguageDetector
{
    /// <summary>
    /// Returns a BCP-47 language tag (e.g. pl, en, zh-CN, zh-TW).
    /// </summary>
    string Detect(string? metadataLanguage, string? plainText);
}
