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
                    <dc:title>Mała latarnia / The Little Lantern</dc:title>
                    <dc:creator>Demo Storybook</dc:creator>
                    <dc:language>mul</dc:language>
                    <dc:publisher>WebEpub Demo</dc:publisher>
                    <dc:description>Krótka wielojęzyczna bajka do wypróbowania czytnika i TTS.</dc:description>
                    <dc:identifier id="BookId">urn:uuid:epub-little-lantern-demo</dc:identifier>
                  </metadata>
                  <manifest>
                    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                    <item id="css" href="style.css" media-type="text/css"/>
                    <item id="chap1" href="chap1.xhtml" media-type="application/xhtml+xml"/>
                    <item id="chap2" href="chap2.xhtml" media-type="application/xhtml+xml"/>
                    <item id="chap3" href="chap3.xhtml" media-type="application/xhtml+xml"/>
                    <item id="chap4" href="chap4.xhtml" media-type="application/xhtml+xml"/>
                    <item id="chap5" href="chap5.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine toc="ncx">
                    <itemref idref="chap1"/>
                    <itemref idref="chap2"/>
                    <itemref idref="chap3"/>
                    <itemref idref="chap4"/>
                    <itemref idref="chap5"/>
                  </spine>
                </package>
                """);

            WriteEntry(archive, "OEBPS/toc.ncx", """
                <?xml version="1.0" encoding="UTF-8"?>
                <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
                  <head><meta name="dtb:uid" content="BookId"/></head>
                  <docTitle><text>Mała latarnia</text></docTitle>
                  <navMap>
                    <navPoint id="navPoint-1" playOrder="1">
                      <navLabel><text>1 · Nad rzeką (PL)</text></navLabel>
                      <content src="chap1.xhtml"/>
                    </navPoint>
                    <navPoint id="navPoint-2" playOrder="2">
                      <navLabel><text>2 · The stranger (EN)</text></navLabel>
                      <content src="chap2.xhtml"/>
                    </navPoint>
                    <navPoint id="navPoint-3" playOrder="3">
                      <navLabel><text>3 · Der Weg (DE)</text></navLabel>
                      <content src="chap3.xhtml"/>
                    </navPoint>
                    <navPoint id="navPoint-4" playOrder="4">
                      <navLabel><text>4 · La orilla (ES)</text></navLabel>
                      <content src="chap4.xhtml"/>
                    </navPoint>
                    <navPoint id="navPoint-5" playOrder="5">
                      <navLabel><text>5 · Powrót / Home (PL·EN)</text></navLabel>
                      <content src="chap5.xhtml"/>
                    </navPoint>
                  </navMap>
                </ncx>
                """);

            WriteEntry(archive, "OEBPS/style.css", """
                body { line-height: 1.65; }
                h1 { font-size: 1.45em; margin-bottom: 0.4em; }
                .lang { font-size: 0.75em; letter-spacing: 0.04em; text-transform: uppercase; opacity: 0.65; margin: 0 0 1em; }
                .nav { margin-top: 1.75em; font-size: 0.92em; }
                blockquote { margin: 1.2em 0; padding-left: 0.9em; border-left: 3px solid #c4a574; opacity: 0.95; }
                """);

            WriteEntry(archive, "OEBPS/chap1.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="pl" lang="pl">
                <head>
                  <title>Nad rzeką</title>
                  <link rel="stylesheet" type="text/css" href="style.css"/>
                </head>
                <body>
                  <p class="lang">Polski</p>
                  <h1 id="top">Nad rzeką</h1>
                  <p>Gdy niebo zrobiło się fioletowe, Lea znalazła w trawie małą mosiężną latarnię. Szkiełko było ciepłe, jakby ktoś dopiero co zdmuchnął płomień.</p>
                  <p>— Jeśli mnie zapalicie — szepnął cichy głos — pokażę wam drogę do miejsca, gdzie kończy się mgła.</p>
                  <p>Lea spojrzała na ciemną wodę. Po drugiej stronie migotało coś jak złota nitka. Nie czekała długo. Zapaliła knot zapałką z kieszeni płaszcza i poszła wzdłuż brzegu.</p>
                  <blockquote>Latarnia nie ważyła prawie nic. Ważyła tylko obietnicę.</blockquote>
                  <p class="nav">Dalej: <a href="chap2.xhtml">The stranger →</a></p>
                </body>
                </html>
                """);

            WriteEntry(archive, "OEBPS/chap2.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="en" lang="en">
                <head>
                  <title>The stranger</title>
                  <link rel="stylesheet" type="text/css" href="style.css"/>
                </head>
                <body>
                  <p class="lang">English</p>
                  <h1>The stranger</h1>
                  <p>Halfway along the path, Lea met a traveler sitting on a fallen oak. His coat was patched with tickets from trains that no longer ran.</p>
                  <p>“Your light is polite,” he said. “It does not shout. Most lights shout.”</p>
                  <p>She held the lantern between them. In its glass she saw not her face, but a small harbor waiting under rain.</p>
                  <p>“If you keep walking,” the stranger continued, “do not ask the fog for permission. Ask your feet.”</p>
                  <p>He tipped an invisible hat and dissolved into the reeds, as if he had always been a shape the evening borrowed.</p>
                  <p class="nav"><a href="chap1.xhtml#top">← Nad rzeką</a> · <a href="chap3.xhtml">Der Weg →</a></p>
                </body>
                </html>
                """);

            WriteEntry(archive, "OEBPS/chap3.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="de" lang="de">
                <head>
                  <title>Der Weg</title>
                  <link rel="stylesheet" type="text/css" href="style.css"/>
                </head>
                <body>
                  <p class="lang">Deutsch</p>
                  <h1>Der Weg</h1>
                  <p>Der Pfad wurde schmal. Steine glänzten wie nasse Münzen. Lea zählte ihre Schritte, bis die Laterne stärker leuchtete als der Mond.</p>
                  <p>„Links liegt die Angst,“ flüsterte das Licht. „Rechts liegt nur Wasser. Geradeaus liegt das Haus mit der offenen Tür.“</p>
                  <p>Sie ging geradeaus. Wind rüttelte an den Weiden, doch die Flamme blieb ruhig — eine kleine Sonne in einer Messinghand.</p>
                  <blockquote>Manchmal ist Mut nur die Entscheidung, nicht umzukehren.</blockquote>
                  <p class="nav"><a href="chap2.xhtml">← The stranger</a> · <a href="chap4.xhtml">La orilla →</a></p>
                </body>
                </html>
                """);

            WriteEntry(archive, "OEBPS/chap4.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="es" lang="es">
                <head>
                  <title>La orilla</title>
                  <link rel="stylesheet" type="text/css" href="style.css"/>
                </head>
                <body>
                  <p class="lang">Español</p>
                  <h1>La orilla</h1>
                  <p>Al fin llegó a una orilla distinta: la arena olía a pan recién horneado y a lluvia antigua. Había un muelle corto y una puerta entreabierta en una casa de madera azul.</p>
                  <p>Dentro, una mesa esperaba con dos tazas. Nadie más estaba allí, pero alguien había dejado una nota:</p>
                  <blockquote>Quien trae luz también puede quedarse. O puede volver, más ligero.</blockquote>
                  <p>Lea sonrió. Bebió un sorbo de té que sabía a limón y a valentía pequeña. Luego miró la linterna.</p>
                  <p>— Gracias — dijo. — Ahora sé el camino de regreso.</p>
                  <p class="nav"><a href="chap3.xhtml">← Der Weg</a> · <a href="chap5.xhtml">Powrót →</a></p>
                </body>
                </html>
                """);

            WriteEntry(archive, "OEBPS/chap5.xhtml", """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="pl" lang="pl">
                <head>
                  <title>Powrót</title>
                  <link rel="stylesheet" type="text/css" href="style.css"/>
                </head>
                <body>
                  <p class="lang">Polski · English</p>
                  <h1 id="end">Powrót / Home</h1>
                  <p>Lea wróciła tą samą ścieżką. Mgła rozstąpiła się jak kotara. Latarnia zgasła sama, lekko, bez dramatu — jak ktoś, kto skończył swoją zmianę.</p>
                  <p>Schowała ją do kieszeni. Od tej nocy, gdy ktoś pytał, czy boi się ciemności, odpowiadała tylko:</p>
                  <blockquote>Nie. Znam drogę.</blockquote>
                  <p lang="en">And if you listen closely on quiet evenings, you may still hear a tiny brass lantern humming by the river — ready for the next walker who needs a polite light.</p>
                  <p class="nav"><a href="chap4.xhtml">← La orilla</a> · <a href="chap1.xhtml#top">Od początku</a></p>
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
