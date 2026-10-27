using System.Text.RegularExpressions;

namespace Maalca.Application.Common;

/// <summary>
/// Saneo mínimo de HTML enriquecido (la descripción que el dueño escribe con el RichTextEditor)
/// al GUARDAR, como defensa de fondo. El display ya sanea con una allowlist estricta en la web
/// (src/lib/sanitize-html.ts), pero no queremos guardar vectores de XSS: una cuenta comprometida
/// o un POST directo a la API saltándose el editor no deberían dejar script/estilos/handlers
/// almacenados que otro consumidor pudiera renderizar sin sanear.
///
/// Sin dependencias nuevas (regex conservador, siguiendo el principio "ligero y veloz" de
/// CLAUDE.md): neutraliza bloques peligrosos, atributos de evento y esquemas de URL peligrosos.
/// No reescribe el resto del HTML — el formato normal (p, strong, em, encabezados, listas) pasa
/// intacto. No reemplaza al saneo del display; lo complementa.
/// </summary>
public static class HtmlScrub
{
    // Bloques con contenido: <script>…</script>, <style>…</style>, etc. (\1 = mismo tag de cierre).
    private static readonly Regex DangerousBlocks = new(
        @"<\s*(script|style|iframe|object|embed|svg|math|form|link|meta|base)\b[^>]*>[\s\S]*?<\s*/\s*\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Las mismas etiquetas sin cierre / autocerradas que pudieran quedar sueltas.
    private static readonly Regex DangerousTags = new(
        @"<\s*/?\s*(script|style|iframe|object|embed|svg|math|form|link|meta|base)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Atributos de evento: onclick, onerror, onload, etc.
    private static readonly Regex EventAttributes = new(
        @"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // href/src con esquemas peligrosos → se neutralizan a "#".
    private static readonly Regex DangerousUrls = new(
        @"(href|src)\s*=\s*(?:""\s*(?:javascript|vbscript|data)\s*:[^""]*""|'\s*(?:javascript|vbscript|data)\s*:[^']*'|(?:javascript|vbscript|data)\s*:[^\s>]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? SanitizeRichText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var s = DangerousBlocks.Replace(html, string.Empty);
        s = DangerousTags.Replace(s, string.Empty);
        s = EventAttributes.Replace(s, string.Empty);
        s = DangerousUrls.Replace(s, "$1=\"#\"");
        return s.Trim();
    }
}
