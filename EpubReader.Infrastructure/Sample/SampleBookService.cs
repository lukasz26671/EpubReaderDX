using System.IO.Compression;
using System.Text;
using EpubReader.Application.Interfaces;

namespace EpubReader.Infrastructure.Sample;

public sealed class SampleBookService : ISampleBookService
{
    public Task<Stream> CreateSampleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "mimetype", "application/epub+zip", Encoding.ASCII, CompressionLevel.NoCompression);
            WriteEntry(archive, "META-INF/container.xml", """
                <?xml version="1.0"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                   <rootfiles>
                      <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                   </rootfiles>
                </container>
                """);

            WriteEntry(archive, "OEBPS/content.opf", """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" unique-identifier="BookId" version="2.0">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>Przygody z Blazor &amp; Web EPUB</dc:title>
                    <dc:creator>Programista C#</dc:creator>
                    <dc:language>pl</dc:language>
                    <dc:publisher>Web Studio Press</dc:publisher>
                    <dc:description>Przykładowa książka demonstracyjna wygenerowana dynamicznie dla EPUB Reader Studio.</dc:description>
                    <dc:identifier id="BookId">urn:uuid:epub-reader-studio-demo</dc:identifier>
                  </metadata>
                  <manifest>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="chap1" href="chap1.xhtml" media-type="application/xhtml+xml"/>
                    <item id="chap2" href="chap2.xhtml" media-type="application/xhtml+xml"/>
                    <item id="chap3" href="chap3.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine toc="ncx">
                    <itemref idref="chap1"/>
                    <itemref idref="chap2"/>
                    <itemref idref="chap3"/>
                  </spine>
                </package>
                """);

            WriteEntry(archive, "OEBPS/toc.ncx", """
                <?xml version="1.0" encoding="UTF-8"?>
                <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
                  <head><meta name="dtb:uid" content="BookId"/></head>
                  <docTitle><text>Przygody z Blazor &amp; Web EPUB</text></docTitle>
                  <navMap>
                    <navPoint id="navPoint-1" playOrder="1">
                      <navLabel><text>Rozdział 1: Wstęp do Czytnika Web</text></navLabel>
                      <content src="chap1.xhtml"/>
                    </navPoint>
                    <navPoint id="navPoint-2" playOrder="2">
                      <navLabel><text>Rozdział 2: Funkcje i Motywy</text></navLabel>
                      <content src="chap2.xhtml"/>
                    </navPoint>
                    <navPoint id="navPoint-3" playOrder="3">
                      <navLabel><text>Rozdział 3: Podsumowanie</text></navLabel>
                      <content src="chap3.xhtml"/>
                    </navPoint>
                  </navMap>
                </ncx>
                """);

            WriteEntry(archive, "OEBPS/chap1.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml">
                <head><title>Rozdział 1: Wstęp do Czytnika Web</title></head>
                <body>
                  <h1 id="top">Rozdział 1: Wstęp do Czytnika Web</h1>
                  <p>Witaj w demonstracyjnej książce EPUB Reader Studio. Cały plik EPUB to archiwum ZIP z dokumentami XHTML i manifestem OPF.</p>
                  <p>Przejdź do <a href="chap2.xhtml">rozdziału 2</a> albo do <a href="chap3.xhtml#summary">podsumowania</a>.</p>
                  <p>Łącze zewnętrzne: <a href="https://dotnet.microsoft.com/">dotnet.microsoft.com</a></p>
                  <blockquote>Clean Architecture pozwala współdzielić Domain i Application między hostami.</blockquote>
                </body>
                </html>
                """);

            WriteEntry(archive, "OEBPS/chap2.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml">
                <head><title>Rozdział 2: Funkcje i Motywy</title></head>
                <body>
                  <h1>Rozdział 2: Funkcje i Motywy</h1>
                  <p>Możesz zmieniać motyw (Jasny, Ciemny, Sepia, Nord), czcionkę, rozmiar tekstu oraz interlinię.</p>
                  <p>Wyszukiwarka przeszukuje tekst rozdziałów, a zakładki zapamiętują wybrane miejsca lektury.</p>
                  <p>Wróć do <a href="chap1.xhtml#top">wstępu</a> lub idź do <a href="chap3.xhtml">rozdziału 3</a>.</p>
                </body>
                </html>
                """);

            WriteEntry(archive, "OEBPS/chap3.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml">
                <head><title>Rozdział 3: Podsumowanie</title></head>
                <body>
                  <h1 id="summary">Rozdział 3: Podsumowanie</h1>
                  <p>Otwórz własny plik .epub albo wróć do tego demo, aby przetestować UI.</p>
                  <p>Skróty: ←/→ lub J/K — rozdział, / — szukaj, B — zakładka, T — spis treści, Esc — zamknij panele.</p>
                  <p>Dziękujemy za wypróbowanie EPUB Reader Studio.</p>
                </body>
                </html>
                """);
        }

        ms.Position = 0;
        return Task.FromResult<Stream>(ms);
    }

    private static void WriteEntry(
        ZipArchive archive,
        string entryName,
        string content,
        Encoding? encoding = null,
        CompressionLevel compression = CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(entryName, compression);
        using var writer = new StreamWriter(entry.Open(), encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content.TrimStart());
    }
}
