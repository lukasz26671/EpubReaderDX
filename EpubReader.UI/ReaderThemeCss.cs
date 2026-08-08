using EpubReader.Domain.Enums;

namespace EpubReader.UI;

public static class ReaderThemeCss
{
    public static string BodyClasses(ReaderTheme theme)
    {
        var darkish = theme is ReaderTheme.Dark or ReaderTheme.Nord or ReaderTheme.Forest
            or ReaderTheme.Midnight or ReaderTheme.Oled or ReaderTheme.Solarized;
        return $"h-full flex flex-col font-sans overflow-hidden antialiased transition-colors duration-200 reader-shell {(darkish ? "dark" : null)}";
    }

    public static string ThemeAttribute(ReaderTheme theme) => theme.ToString().ToLowerInvariant();

    public static string FontStack(FontFamilyOption font) => font switch
    {
        FontFamilyOption.Sans => "Inter, system-ui, -apple-system, \"Segoe UI\", Roboto, sans-serif",
        FontFamilyOption.Mono => "\"Fira Code\", \"Cascadia Code\", Consolas, \"Courier New\", monospace",
        FontFamilyOption.Dyslexic => "\"Comic Sans MS\", \"Segoe Print\", Verdana, Arial, sans-serif",
        FontFamilyOption.Palatino => "Palatino, \"Palatino Linotype\", \"Book Antiqua\", Georgia, serif",
        FontFamilyOption.Merriweather => "Georgia, \"Times New Roman\", \"Liberation Serif\", serif",
        FontFamilyOption.Rounded => "\"Segoe UI\", Candara, \"Trebuchet MS\", sans-serif",
        FontFamilyOption.Condensed => "\"Arial Narrow\", \"Helvetica Condensed\", Impact, sans-serif",
        _ => "Georgia, Cambria, \"Times New Roman\", Times, serif"
    };

    public static string TextAlignCss(TextAlignOption align) => align switch
    {
        TextAlignOption.Left => "left",
        TextAlignOption.Center => "center",
        _ => "justify"
    };

    public static string ReaderContentStyle(Domain.Entities.ReaderSettings settings)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return string.Join(' ',
            $"--reader-font-family:{FontStack(settings.FontFamily)};",
            $"--reader-font-size:{settings.FontSize.ToString(inv)}px;",
            $"--reader-line-height:{settings.LineHeight.ToString(inv)};",
            $"--reader-letter-spacing:{settings.LetterSpacing.ToString(inv)}em;",
            $"--reader-paragraph-spacing:{settings.ParagraphSpacing.ToString(inv)}em;",
            $"--reader-text-align:{TextAlignCss(settings.TextAlign)};",
            $"--reader-content-width:{settings.ContentWidth.ToString(inv)}rem;",
            $"--reader-page-margin:{settings.PageMargin.ToString(inv)}px;",
            "font-family:var(--reader-font-family);",
            "font-size:var(--reader-font-size);",
            "line-height:var(--reader-line-height);",
            "letter-spacing:var(--reader-letter-spacing);",
            "text-align:var(--reader-text-align);");
    }

    public static (string Label, string Swatch, string Accent) ThemeMeta(ReaderTheme theme) => theme switch
    {
        ReaderTheme.Sepia => ("Sepia", "#edd298", "#a16207"),
        ReaderTheme.Paper => ("Papier", "#f3efe7", "#78716c"),
        ReaderTheme.Dark => ("Ciemny", "#0f172a", "#38bdf8"),
        ReaderTheme.Nord => ("Nord", "#3b4252", "#88c0d0"),
        ReaderTheme.Forest => ("Las", "#1a2e1a", "#86efac"),
        ReaderTheme.Rose => ("Róż", "#fff1f2", "#e11d48"),
        ReaderTheme.Midnight => ("Północ", "#111827", "#a78bfa"),
        ReaderTheme.Solarized => ("Solar", "#002b36", "#b58900"),
        ReaderTheme.Oled => ("OLED", "#000000", "#22d3ee"),
        _ => ("Jasny", "#ffffff", "#2563eb")
    };

    public static (string Label, string Sample) FontMeta(FontFamilyOption font) => font switch
    {
        FontFamilyOption.Sans => ("Sans", "Aa"),
        FontFamilyOption.Mono => ("Mono", "{}"),
        FontFamilyOption.Dyslexic => ("Dyslexic", "Dy"),
        FontFamilyOption.Palatino => ("Palatino", "Pa"),
        FontFamilyOption.Merriweather => ("Merriweather", "Me"),
        FontFamilyOption.Rounded => ("Rounded", "Ro"),
        FontFamilyOption.Condensed => ("Condensed", "Co"),
        _ => ("Serif", "Aa")
    };
}
