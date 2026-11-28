using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Maalca.Application.Creator;

/// <summary>Un libro listo para empaquetar. El texto de los capítulos ya viene en XHTML (ver XhtmlFragment).</summary>
public sealed record EpubBook(
    string Identifier,          // "urn:isbn:…" o "urn:uuid:…": el identificador único del paquete
    string? Isbn,
    string Title,
    string? Subtitle,
    string Language,            // BCP 47: "es", "en"
    string Author,
    string? Publisher,
    int? Year,
    string? Description,        // texto plano (la sinopsis sin etiquetas)
    string? TranslationCredit,  // "Traducción del autor" / "Translated by the author"
    string? OriginalTitle,      // en una traducción, el título original
    bool Poetry,                // poemario: sin sangría ni justificado, los versos se respetan
    EpubImage? Cover,
    IReadOnlyList<EpubChapter> Chapters,
    DateTime ModifiedUtc);

public sealed record EpubImage(byte[] Bytes, string MediaType, string Extension);

public sealed record EpubChapter(string Title, string BodyXhtml);

/// <summary>
/// Empaqueta un EPUB 3 (con toc.ncx para lectores viejos) usando solo la biblioteca estándar
/// (System.IO.Compression). Estructura, en el orden del spine:
///   portada (si hay) → portadilla → créditos → índice → capítulos.
/// Lo que exigen las tiendas y epubcheck, y que esta clase cuida:
///   - "mimetype" es la primera entrada, sin comprimir, con "application/epub+zip".
///   - Cada XHTML es XML válido con su &lt;title&gt; y el idioma en xml:lang y lang.
///   - dcterms:modified en UTC sin fracciones; un dc:identifier único que es el unique-identifier.
///   - La portada declarada con properties="cover-image" (EPUB 3) y meta name="cover" (Kindle / EPUB 2).
/// La portadilla y los créditos son texto editorial fijo (no del manuscrito), traducido según Language.
/// </summary>
public static class EpubBuilder
{
    public const string MediaType = "application/epub+zip";

    public static byte[] Build(EpubBook book)
    {
        var lang = book.Language;
        var t = Texts.For(lang);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1) mimetype: primera entrada y sin comprimir (los lectores la buscan en el byte 38).
            AddText(zip, "mimetype", MediaType, CompressionLevel.NoCompression);
            AddText(zip, "META-INF/container.xml", ContainerXml);
            AddText(zip, "OEBPS/css/book.css", Css(book.Poetry));

            var manifest = new StringBuilder();
            var spine = new StringBuilder();
            void Item(string id, string href, string type, string? props = null)
            {
                manifest.Append("    <item id=\"").Append(id).Append("\" href=\"").Append(href)
                    .Append("\" media-type=\"").Append(type).Append('"');
                if (props is not null) manifest.Append(" properties=\"").Append(props).Append('"');
                manifest.Append("/>\n");
            }
            void Spine(string id, bool linear = true)
                => spine.Append("    <itemref idref=\"").Append(id).Append('"').Append(linear ? "" : " linear=\"no\"").Append("/>\n");

            Item("css", "css/book.css", "text/css");
            Item("nav", "nav.xhtml", "application/xhtml+xml", "nav");
            Item("ncx", "toc.ncx", "application/x-dtbncx+xml");

            string? firstPage = null;
            if (book.Cover is { } cover)
            {
                var coverHref = "images/cover." + cover.Extension;
                AddBytes(zip, "OEBPS/" + coverHref, cover.Bytes);
                Item("cover-image", coverHref, cover.MediaType, "cover-image");
                AddText(zip, "OEBPS/cover.xhtml", Page(lang, t.Cover, "cover",
                    $"<div class=\"cover\"><img src=\"{coverHref}\" alt=\"{Esc(book.Title)}\" /></div>"));
                Item("cover", "cover.xhtml", "application/xhtml+xml");
                Spine("cover");
                firstPage = "cover.xhtml";
            }

            AddText(zip, "OEBPS/title.xhtml", Page(lang, book.Title, "titlepage", TitlePage(book)));
            Item("titlepage", "title.xhtml", "application/xhtml+xml");
            Spine("titlepage");

            AddText(zip, "OEBPS/copyright.xhtml", Page(lang, t.Copyright, "copyright", CopyrightPage(book, t)));
            Item("copyright", "copyright.xhtml", "application/xhtml+xml");
            Spine("copyright");

            var chapterFiles = new List<(string File, string Title)>();
            for (var i = 0; i < book.Chapters.Count; i++)
            {
                var ch = book.Chapters[i];
                var file = $"ch{(i + 1).ToString("D3", CultureInfo.InvariantCulture)}.xhtml";
                var title = string.IsNullOrWhiteSpace(ch.Title) ? $"{t.Chapter} {i + 1}" : ch.Title.Trim();
                var body = $"<section class=\"chapter\" epub:type=\"chapter\" role=\"doc-chapter\" aria-labelledby=\"h{i + 1}\">\n" +
                           $"<h1 id=\"h{i + 1}\">{Esc(title)}</h1>\n{ch.BodyXhtml}\n</section>";
                AddText(zip, "OEBPS/" + file, Page(lang, title, "bodymatter", body));
                var id = "ch" + (i + 1).ToString("D3", CultureInfo.InvariantCulture);
                Item(id, file, "application/xhtml+xml");
                chapterFiles.Add((file, title));
            }

            // El índice va después de los créditos y antes del texto (como en un libro impreso).
            AddText(zip, "OEBPS/nav.xhtml", Nav(book, t, chapterFiles, firstPage));
            Spine("nav");
            for (var i = 0; i < chapterFiles.Count; i++) Spine("ch" + (i + 1).ToString("D3", CultureInfo.InvariantCulture));

            AddText(zip, "OEBPS/toc.ncx", Ncx(book, chapterFiles));
            AddText(zip, "OEBPS/content.opf", Opf(book, manifest.ToString(), spine.ToString()));
        }
        return ms.ToArray();
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────

    private const string ContainerXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\n" +
        "  <rootfiles>\n" +
        "    <rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/>\n" +
        "  </rootfiles>\n" +
        "</container>\n";

    private static string Opf(EpubBook b, string manifest, string spine)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"bookid\" xml:lang=\"")
          .Append(b.Language).Append("\">\n");
        sb.Append("  <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n");
        sb.Append("    <dc:identifier id=\"bookid\">").Append(Esc(b.Identifier)).Append("</dc:identifier>\n");
        sb.Append("    <dc:title id=\"title\">").Append(Esc(b.Title)).Append("</dc:title>\n");
        sb.Append("    <meta refines=\"#title\" property=\"title-type\">main</meta>\n");
        if (!string.IsNullOrWhiteSpace(b.Subtitle))
        {
            sb.Append("    <dc:title id=\"subtitle\">").Append(Esc(b.Subtitle)).Append("</dc:title>\n");
            sb.Append("    <meta refines=\"#subtitle\" property=\"title-type\">subtitle</meta>\n");
        }
        sb.Append("    <dc:language>").Append(Esc(b.Language)).Append("</dc:language>\n");
        sb.Append("    <dc:creator id=\"author\">").Append(Esc(b.Author)).Append("</dc:creator>\n");
        sb.Append("    <meta refines=\"#author\" property=\"role\" scheme=\"marc:relators\">aut</meta>\n");
        if (!string.IsNullOrWhiteSpace(b.Publisher))
            sb.Append("    <dc:publisher>").Append(Esc(b.Publisher)).Append("</dc:publisher>\n");
        if (b.Year is int y)
            sb.Append("    <dc:date>").Append(y.ToString(CultureInfo.InvariantCulture)).Append("</dc:date>\n");
        if (!string.IsNullOrWhiteSpace(b.Description))
            sb.Append("    <dc:description>").Append(Esc(b.Description)).Append("</dc:description>\n");
        sb.Append("    <dc:rights>").Append(Esc(Texts.For(b.Language).RightsLine(b.Year, b.Author))).Append("</dc:rights>\n");
        sb.Append("    <meta property=\"dcterms:modified\">")
          .Append(b.ModifiedUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
          .Append("</meta>\n");
        if (b.Cover is not null) sb.Append("    <meta name=\"cover\" content=\"cover-image\"/>\n");
        sb.Append("  </metadata>\n");
        sb.Append("  <manifest>\n").Append(manifest).Append("  </manifest>\n");
        sb.Append("  <spine toc=\"ncx\">\n").Append(spine).Append("  </spine>\n");
        sb.Append("</package>\n");
        return sb.ToString();
    }

    private static string Nav(EpubBook b, Texts t, List<(string File, string Title)> chapters, string? coverPage)
    {
        var sb = new StringBuilder();
        sb.Append("<nav epub:type=\"toc\" id=\"toc\" role=\"doc-toc\">\n<h1>").Append(Esc(t.Contents)).Append("</h1>\n<ol>\n");
        foreach (var (file, title) in chapters)
            sb.Append("<li><a href=\"").Append(file).Append("\">").Append(Esc(title)).Append("</a></li>\n");
        sb.Append("</ol>\n</nav>\n");
        sb.Append("<nav epub:type=\"landmarks\" id=\"landmarks\" hidden=\"hidden\">\n<ol>\n");
        if (coverPage is not null)
            sb.Append("<li><a epub:type=\"cover\" href=\"").Append(coverPage).Append("\">").Append(Esc(t.Cover)).Append("</a></li>\n");
        sb.Append("<li><a epub:type=\"toc\" href=\"nav.xhtml#toc\">").Append(Esc(t.Contents)).Append("</a></li>\n");
        if (chapters.Count > 0)
            sb.Append("<li><a epub:type=\"bodymatter\" href=\"").Append(chapters[0].File).Append("\">").Append(Esc(t.Start)).Append("</a></li>\n");
        sb.Append("</ol>\n</nav>");
        return Page(b.Language, t.Contents, "toc", sb.ToString());
    }

    private static string Ncx(EpubBook b, List<(string File, string Title)> chapters)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\" xml:lang=\"").Append(b.Language).Append("\">\n");
        sb.Append("  <head>\n    <meta name=\"dtb:uid\" content=\"").Append(Esc(b.Identifier)).Append("\"/>\n");
        sb.Append("    <meta name=\"dtb:depth\" content=\"1\"/>\n    <meta name=\"dtb:totalPageCount\" content=\"0\"/>\n");
        sb.Append("    <meta name=\"dtb:maxPageNumber\" content=\"0\"/>\n  </head>\n");
        sb.Append("  <docTitle><text>").Append(Esc(b.Title)).Append("</text></docTitle>\n");
        sb.Append("  <docAuthor><text>").Append(Esc(b.Author)).Append("</text></docAuthor>\n  <navMap>\n");
        for (var i = 0; i < chapters.Count; i++)
        {
            var n = (i + 1).ToString(CultureInfo.InvariantCulture);
            sb.Append("    <navPoint id=\"np").Append(n).Append("\" playOrder=\"").Append(n).Append("\">\n");
            sb.Append("      <navLabel><text>").Append(Esc(chapters[i].Title)).Append("</text></navLabel>\n");
            sb.Append("      <content src=\"").Append(chapters[i].File).Append("\"/>\n    </navPoint>\n");
        }
        sb.Append("  </navMap>\n</ncx>\n");
        return sb.ToString();
    }

    private static string TitlePage(EpubBook b)
    {
        var sb = new StringBuilder("<section class=\"titlepage\" epub:type=\"titlepage\">\n");
        sb.Append("<p class=\"tp-author\">").Append(Esc(b.Author)).Append("</p>\n");
        sb.Append("<h1 class=\"tp-title\">").Append(Esc(b.Title)).Append("</h1>\n");
        if (!string.IsNullOrWhiteSpace(b.Subtitle)) sb.Append("<p class=\"tp-subtitle\">").Append(Esc(b.Subtitle)).Append("</p>\n");
        if (!string.IsNullOrWhiteSpace(b.Publisher)) sb.Append("<p class=\"tp-publisher\">").Append(Esc(b.Publisher)).Append("</p>\n");
        sb.Append("</section>");
        return sb.ToString();
    }

    private static string CopyrightPage(EpubBook b, Texts t)
    {
        var sb = new StringBuilder("<section class=\"copyright\" epub:type=\"copyright-page\">\n");
        if (!string.IsNullOrWhiteSpace(b.OriginalTitle))
            sb.Append("<p>").Append(Esc(t.OriginalTitle)).Append(": <em>").Append(Esc(b.OriginalTitle)).Append("</em></p>\n");
        if (!string.IsNullOrWhiteSpace(b.TranslationCredit))
            sb.Append("<p>").Append(Esc(b.TranslationCredit)).Append("</p>\n");
        sb.Append("<p>").Append(Esc(t.RightsLine(b.Year, b.Author))).Append("</p>\n");
        sb.Append("<p>").Append(Esc(t.Reproduction)).Append("</p>\n");
        if (!string.IsNullOrWhiteSpace(b.Publisher))
            sb.Append("<p>").Append(Esc(t.PublishedBy)).Append(' ').Append(Esc(b.Publisher)).Append("</p>\n");
        if (!string.IsNullOrWhiteSpace(b.Isbn))
            sb.Append("<p>ISBN ").Append(Esc(b.Isbn)).Append(" (").Append(Esc(t.Ebook)).Append(")</p>\n");
        sb.Append("<p>").Append(Esc(t.Edition)).Append(' ').Append(b.ModifiedUtc.Year.ToString(CultureInfo.InvariantCulture)).Append("</p>\n");
        sb.Append("</section>");
        return sb.ToString();
    }

    private static string Page(string lang, string title, string bodyClass, string body)
        => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE html>\n" +
           "<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" " +
           $"xml:lang=\"{lang}\" lang=\"{lang}\">\n<head>\n<meta charset=\"utf-8\" />\n<title>{Esc(title)}</title>\n" +
           "<link rel=\"stylesheet\" type=\"text/css\" href=\"css/book.css\" />\n</head>\n" +
           $"<body class=\"{bodyClass}\">\n{body}\n</body>\n</html>\n";

    /// <summary>Estilo de la casa: sobrio, legible en cualquier lector (sin fuentes incrustadas; respeta las del lector).</summary>
    private static string Css(bool poetry) =>
        "@charset \"utf-8\";\n" +
        "body { margin: 0 5%; font-family: serif; line-height: 1.45; }\n" +
        "p { margin: 0; text-indent: " + (poetry ? "0" : "1.3em") + "; text-align: " + (poetry ? "left" : "justify") + "; " +
        "-webkit-hyphens: auto; hyphens: auto; widows: 2; orphans: 2; }\n" +
        (poetry ? "p { margin-bottom: 0.15em; }\n" : "") +
        "h1 + p, h2 + p, h3 + p, hr + p, blockquote + p, .chapter > p:first-of-type { text-indent: 0; }\n" +
        "h1 { font-size: 1.5em; font-weight: normal; text-align: center; margin: 3em 0 2em; page-break-before: always; break-before: page; }\n" +
        "h2, h3, h4, h5, h6 { font-weight: normal; text-align: center; margin: 1.6em 0 1em; }\n" +
        "h2 { font-size: 1.25em; } h3 { font-size: 1.1em; } h4, h5, h6 { font-size: 1em; font-style: italic; }\n" +
        "blockquote { margin: 1em 1.5em; font-style: italic; }\n" +
        "blockquote p { text-indent: 0; }\n" +
        "hr { border: 0; text-align: center; margin: 1.5em 0; }\n" +
        "hr::after { content: \"* * *\"; }\n" +
        "ul, ol { margin: 0.8em 0 0.8em 1.5em; }\n" +
        ".ta-center { text-align: center; text-indent: 0; }\n" +
        ".ta-right { text-align: right; text-indent: 0; }\n" +
        ".ta-justify { text-align: justify; }\n" +
        ".cover { margin: 0; padding: 0; text-align: center; height: 100%; }\n" +
        ".cover img { max-width: 100%; max-height: 100%; }\n" +
        "body.cover { margin: 0; }\n" +
        ".titlepage { text-align: center; margin-top: 25%; }\n" +
        ".titlepage p { text-align: center; text-indent: 0; }\n" +
        ".tp-author { font-size: 1.1em; letter-spacing: 0.08em; margin-bottom: 2em; }\n" +
        ".tp-title { font-size: 2em; margin: 0 0 0.5em; page-break-before: avoid; break-before: avoid; }\n" +
        ".tp-subtitle { font-style: italic; margin-bottom: 4em; }\n" +
        ".tp-publisher { font-variant: small-caps; letter-spacing: 0.1em; margin-top: 6em; }\n" +
        ".copyright { margin-top: 40%; font-size: 0.85em; }\n" +
        ".copyright p { text-indent: 0; text-align: left; margin-bottom: 0.6em; }\n" +
        "nav ol { list-style: none; margin-left: 0; }\n" +
        "nav li { margin: 0.4em 0; }\n" +
        "nav a { text-decoration: none; }\n";

    private static string Esc(string? s) => XhtmlFragment.Escape(s);

    private static void AddText(ZipArchive zip, string path, string content, CompressionLevel level = CompressionLevel.Optimal)
    {
        var entry = zip.CreateEntry(path, level);
        using var s = entry.Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void AddBytes(ZipArchive zip, string path, byte[] bytes)
    {
        // Las imágenes ya vienen comprimidas: guardarlas tal cual.
        var entry = zip.CreateEntry(path, CompressionLevel.NoCompression);
        using var s = entry.Open();
        s.Write(bytes, 0, bytes.Length);
    }

    // ── Texto editorial por idioma ──────────────────────────────────────────────

    private sealed record Texts(
        string Cover, string Copyright, string Contents, string Start, string Chapter, string OriginalTitle,
        string Reproduction, string PublishedBy, string Ebook, string Edition, string RightsHolderFormat)
    {
        public string RightsLine(int? year, string author)
            => string.Format(CultureInfo.InvariantCulture, RightsHolderFormat, year?.ToString(CultureInfo.InvariantCulture) ?? DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture), author);

        public static Texts For(string lang) => lang == "en" ? En : Es;

        private static readonly Texts Es = new(
            "Portada", "Créditos", "Índice", "Comienzo", "Capítulo", "Título original",
            "Queda prohibida la reproducción total o parcial de esta obra por cualquier medio sin la autorización escrita del titular de los derechos.",
            "Publicado por", "libro electrónico", "Edición digital,", "© {0} {1}. Todos los derechos reservados.");

        private static readonly Texts En = new(
            "Cover", "Copyright", "Contents", "Start", "Chapter", "Original title",
            "No part of this book may be reproduced in any form or by any means without written permission from the rights holder.",
            "Published by", "ebook", "Digital edition,", "© {0} {1}. All rights reserved.");
    }
}
