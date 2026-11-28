using System.Net;
using System.Text;
using System.Xml;

namespace Maalca.Application.Creator;

/// <summary>
/// Convierte el HTML de un capítulo (lo que guarda el editor del dashboard) en un fragmento XHTML
/// bien formado y válido para EPUB 3. Las tiendas (Amazon, Apple, Kobo) rechazan un EPUB con un solo
/// error de XML, y el HTML del editor es HTML, no XML: &amp;nbsp;, &lt;br&gt; sin cerrar, etiquetas
/// cruzadas, estilos en línea.
///
/// Por qué un tokenizador propio y no AngleSharp/HtmlAgilityPack: el principio "ligero y veloz"
/// (sin dependencias nuevas), y porque aquí además hace de lista blanca: lo que no está en
/// <see cref="Allowed"/> se descarta conservando su texto. Reglas:
///   - Texto: se decodifican las entidades HTML y se vuelve a escapar para XML.
///   - Etiquetas vacías (br, hr) se autocierran; img se quita (una imagen remota no puede ir dentro
///     del EPUB sin descargarla, y un recurso remoto lo invalida) y se cuenta en <see cref="DroppedImages"/>.
///   - Anidamiento: abrir un bloque cierra el párrafo y las marcas en línea abiertas (en HTML un
///     &lt;p&gt; no puede ir dentro de &lt;strong&gt; ni de otro &lt;p&gt;); un cierre sin apertura se ignora;
///     un cierre que salta niveles cierra los intermedios; al final se cierra todo.
///   - Alineación: style="text-align:center|right|justify" se vuelve class="ta-center" etc. (la hoja de
///     estilo del EPUB la define). Cualquier otro estilo en línea se quita.
///   - Enlaces: solo http(s) y mailto.
/// El resultado se comprueba con XmlReader; si no parsea, lanza (nunca se entrega un EPUB roto).
/// </summary>
public sealed class XhtmlFragment
{
    private const string Ns = "http://www.w3.org/1999/xhtml";

    private enum Kind { Inline, Block, Void, Container, ListItem, List, Transparent }

    // Etiquetas permitidas → (nombre de salida, tipo). Transparent = se quita la etiqueta y queda su contenido.
    private static readonly Dictionary<string, (string Out, Kind Kind)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["p"] = ("p", Kind.Block),
        ["h1"] = ("h2", Kind.Block), // el h1 es el título del capítulo; los del texto bajan un nivel
        ["h2"] = ("h3", Kind.Block),
        ["h3"] = ("h4", Kind.Block),
        ["h4"] = ("h5", Kind.Block),
        ["h5"] = ("h6", Kind.Block),
        ["h6"] = ("h6", Kind.Block),
        ["pre"] = ("pre", Kind.Block),
        ["blockquote"] = ("blockquote", Kind.Container),
        ["div"] = ("div", Kind.Container),
        ["section"] = ("div", Kind.Container),
        ["article"] = ("div", Kind.Container),
        ["figure"] = ("div", Kind.Container),
        ["figcaption"] = ("p", Kind.Block),
        ["ul"] = ("ul", Kind.List),
        ["ol"] = ("ol", Kind.List),
        ["li"] = ("li", Kind.ListItem),
        ["td"] = ("p", Kind.Block),
        ["th"] = ("p", Kind.Block),
        ["center"] = ("p", Kind.Block),
        ["table"] = ("", Kind.Transparent),
        ["thead"] = ("", Kind.Transparent),
        ["tbody"] = ("", Kind.Transparent),
        ["tfoot"] = ("", Kind.Transparent),
        ["tr"] = ("", Kind.Transparent),
        ["font"] = ("", Kind.Transparent),
        ["strong"] = ("strong", Kind.Inline),
        ["b"] = ("strong", Kind.Inline),
        ["em"] = ("em", Kind.Inline),
        ["i"] = ("em", Kind.Inline),
        ["u"] = ("u", Kind.Inline),
        ["s"] = ("s", Kind.Inline),
        ["strike"] = ("s", Kind.Inline),
        ["del"] = ("del", Kind.Inline),
        ["ins"] = ("ins", Kind.Inline),
        ["sub"] = ("sub", Kind.Inline),
        ["sup"] = ("sup", Kind.Inline),
        ["small"] = ("small", Kind.Inline),
        ["mark"] = ("mark", Kind.Inline),
        ["code"] = ("code", Kind.Inline),
        ["span"] = ("span", Kind.Inline),
        ["a"] = ("a", Kind.Inline),
        ["br"] = ("br", Kind.Void),
        ["hr"] = ("hr", Kind.Void),
    };

    // Contenido crudo que se descarta entero (no solo la etiqueta).
    private static readonly HashSet<string> SkipWithContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "head", "title", "iframe", "object", "embed", "svg", "math", "noscript", "template", "form", "select", "textarea",
    };

    private readonly StringBuilder _out = new();
    private readonly List<(string Name, Kind Kind)> _stack = new();

    /// <summary>Imágenes que traía el HTML y no entraron al EPUB.</summary>
    public int DroppedImages { get; private set; }

    public string Xhtml => _out.ToString();

    public static XhtmlFragment Convert(string? html)
    {
        var f = new XhtmlFragment();
        f.Run(html ?? string.Empty);
        f.Verify();
        return f;
    }

    private void Run(string html)
    {
        var i = 0;
        var text = new StringBuilder();
        while (i < html.Length)
        {
            var c = html[i];
            if (c != '<')
            {
                text.Append(c);
                i++;
                continue;
            }

            // Comentario, doctype, CDATA, instrucción de proceso → fuera.
            if (StartsWith(html, i, "<!--"))
            {
                FlushText(text);
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? html.Length : end + 3;
                continue;
            }
            if (StartsWith(html, i, "<!") || StartsWith(html, i, "<?"))
            {
                FlushText(text);
                var end = html.IndexOf('>', i + 2);
                i = end < 0 ? html.Length : end + 1;
                continue;
            }

            var close = i + 1 < html.Length && html[i + 1] == '/';
            var nameStart = close ? i + 2 : i + 1;
            var n = nameStart;
            while (n < html.Length && (char.IsLetterOrDigit(html[n]) || html[n] == '-' || html[n] == ':')) n++;
            if (n == nameStart)
            {
                // Un "<" suelto en el texto (ej. "a < b"): es texto.
                text.Append(c);
                i++;
                continue;
            }

            var tagEnd = FindTagEnd(html, n);
            var name = html[nameStart..n].ToLowerInvariant();
            var attrs = html[n..Math.Max(n, tagEnd)];
            i = tagEnd < html.Length ? tagEnd + 1 : html.Length;
            FlushText(text);

            if (close)
            {
                CloseTag(name);
                continue;
            }

            if (SkipWithContent.Contains(name))
            {
                var endTag = html.IndexOf("</" + name, i, StringComparison.OrdinalIgnoreCase);
                if (endTag < 0) { i = html.Length; continue; }
                var gt = html.IndexOf('>', endTag);
                i = gt < 0 ? html.Length : gt + 1;
                continue;
            }

            if (name == "img") { DroppedImages++; continue; }
            if (!Allowed.TryGetValue(name, out var spec)) continue; // etiqueta desconocida: fuera, su texto queda

            OpenTag(spec.Out, spec.Kind, attrs, selfClosed: attrs.TrimEnd().EndsWith('/'));
        }
        FlushText(text);
        while (_stack.Count > 0) Pop();
    }

    private static bool StartsWith(string s, int at, string prefix)
        => string.CompareOrdinal(s, at, prefix, 0, prefix.Length) == 0;

    // Fin de la etiqueta respetando comillas en los atributos (un ">" dentro de un title="a>b" no cierra).
    private static int FindTagEnd(string html, int from)
    {
        char? quote = null;
        for (var k = from; k < html.Length; k++)
        {
            var ch = html[k];
            if (quote is not null) { if (ch == quote) quote = null; continue; }
            if (ch == '"' || ch == '\'') { quote = ch; continue; }
            if (ch == '>') return k;
        }
        return html.Length;
    }

    private void OpenTag(string name, Kind kind, string attrs, bool selfClosed)
    {
        if (kind == Kind.Transparent) return;

        switch (kind)
        {
            case Kind.Void:
                if (name == "hr") CloseInlineAndParagraphs();
                EnsureListItemContext();
                _out.Append('<').Append(name).Append(" />");
                return;

            case Kind.Block:
                CloseInlineAndParagraphs();
                EnsureListItemContext();
                break;

            case Kind.Container:
                CloseInlineAndParagraphs();
                EnsureListItemContext();
                break;

            case Kind.List:
                CloseInlineAndParagraphs();
                EnsureListItemContext();
                break;

            case Kind.ListItem:
                // Un <li> nuevo cierra el anterior de la misma lista; fuera de una lista se vuelve párrafo.
                CloseInlineAndParagraphs();
                while (_stack.Count > 0 && _stack[^1].Kind == Kind.ListItem) Pop();
                if (_stack.Count == 0 || _stack[^1].Kind != Kind.List)
                {
                    name = "p";
                    kind = Kind.Block;
                }
                break;

            case Kind.Inline:
                // Un enlace no puede ir dentro de otro: el nuevo cierra el abierto (como hace el navegador).
                if (name == "a")
                {
                    var open = _stack.FindLastIndex(e => e.Name == "a");
                    if (open >= 0) while (_stack.Count > open) Pop();
                }
                EnsureListItemContext();
                break;
        }

        _out.Append('<').Append(name);
        AppendAttributes(name, attrs);
        _out.Append('>');
        _stack.Add((name, kind));
        if (selfClosed) Pop();
    }

    private void CloseTag(string rawName)
    {
        if (rawName == "img" || rawName == "br" || rawName == "hr") return;
        if (!Allowed.TryGetValue(rawName, out var spec) || spec.Kind == Kind.Transparent) return;
        var name = spec.Out;
        // li fuera de lista se abrió como p: su cierre debe cerrar ese p.
        var idx = _stack.FindLastIndex(e => e.Name == name || (spec.Kind == Kind.ListItem && e.Name == "p"));
        if (idx < 0) return;
        while (_stack.Count > idx) Pop();
    }

    // Antes de abrir un bloque: cerrar marcas en línea y párrafos/encabezados abiertos.
    private void CloseInlineAndParagraphs()
    {
        while (_stack.Count > 0 && (_stack[^1].Kind == Kind.Inline || _stack[^1].Kind == Kind.Block)) Pop();
    }

    // Dentro de <ul>/<ol> solo puede ir <li>: lo que llegue suelto se mete en uno.
    private void EnsureListItemContext()
    {
        if (_stack.Count > 0 && _stack[^1].Kind == Kind.List)
        {
            _out.Append("<li>");
            _stack.Add(("li", Kind.ListItem));
        }
    }

    private void Pop()
    {
        var top = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        _out.Append("</").Append(top.Name).Append('>');
    }

    private void FlushText(StringBuilder text)
    {
        if (text.Length == 0) return;
        var decoded = WebUtility.HtmlDecode(text.ToString());
        text.Clear();
        // Dentro de una lista solo puede haber <li>: los espacios entre etiquetas no cuentan.
        if (_stack.Count > 0 && _stack[^1].Kind == Kind.List)
        {
            if (string.IsNullOrWhiteSpace(decoded)) return;
            EnsureListItemContext();
        }
        AppendEscaped(_out, decoded);
    }

    private void AppendAttributes(string name, string attrs)
    {
        var map = ParseAttributes(attrs);
        if (map.TryGetValue("style", out var style))
        {
            var cls = AlignClass(style);
            if (cls is not null) _out.Append(" class=\"").Append(cls).Append('"');
        }
        if (name == "a" && map.TryGetValue("href", out var href))
        {
            href = href.Trim();
            if (href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                _out.Append(" href=\"");
                AppendEscaped(_out, href, attribute: true);
                _out.Append('"');
            }
        }
        if ((name == "ol") && map.TryGetValue("start", out var start) && int.TryParse(start, out var s) && s > 0)
            _out.Append(" start=\"").Append(s).Append('"');
    }

    private static string? AlignClass(string style)
    {
        foreach (var decl in style.Split(';'))
        {
            var parts = decl.Split(':', 2);
            if (parts.Length != 2 || !parts[0].Trim().Equals("text-align", StringComparison.OrdinalIgnoreCase)) continue;
            return parts[1].Trim().ToLowerInvariant() switch
            {
                "center" => "ta-center",
                "right" or "end" => "ta-right",
                "justify" => "ta-justify",
                _ => null,
            };
        }
        return null;
    }

    private static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < attrs.Length)
        {
            while (i < attrs.Length && (char.IsWhiteSpace(attrs[i]) || attrs[i] == '/')) i++;
            var ns = i;
            while (i < attrs.Length && !char.IsWhiteSpace(attrs[i]) && attrs[i] != '=' && attrs[i] != '/') i++;
            if (i == ns) { i++; continue; }
            var key = attrs[ns..i];
            while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;
            var value = string.Empty;
            if (i < attrs.Length && attrs[i] == '=')
            {
                i++;
                while (i < attrs.Length && char.IsWhiteSpace(attrs[i])) i++;
                if (i < attrs.Length && (attrs[i] == '"' || attrs[i] == '\''))
                {
                    var q = attrs[i++];
                    var vs = i;
                    while (i < attrs.Length && attrs[i] != q) i++;
                    value = attrs[vs..Math.Min(i, attrs.Length)];
                    i++;
                }
                else
                {
                    var vs = i;
                    while (i < attrs.Length && !char.IsWhiteSpace(attrs[i])) i++;
                    value = attrs[vs..i];
                }
            }
            map[key] = WebUtility.HtmlDecode(value);
        }
        return map;
    }

    /// <summary>Escapa para XML y quita los caracteres que XML 1.0 no admite (controles salvo tab/salto).</summary>
    public static void AppendEscaped(StringBuilder sb, string value, bool attribute = false)
    {
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"' when attribute: sb.Append("&quot;"); break;
                case '\t' or '\n' or '\r': sb.Append(ch); break;
                default:
                    if (ch < 0x20 || ch == 0xFFFE || ch == 0xFFFF) break;
                    sb.Append(ch);
                    break;
            }
        }
    }

    public static string Escape(string? value)
    {
        var sb = new StringBuilder();
        AppendEscaped(sb, value ?? string.Empty, attribute: true);
        return sb.ToString();
    }

    private void Verify()
    {
        var doc = "<div xmlns=\"" + Ns + "\">" + _out + "</div>";
        using var reader = XmlReader.Create(new StringReader(doc), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        while (reader.Read()) { }
    }
}
