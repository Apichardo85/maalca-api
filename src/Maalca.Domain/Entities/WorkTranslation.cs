using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Cómo se traduce una obra a un idioma: la guía del autor (voz, registro, qué palabras se quedan en
/// español), el glosario (término → traducción fija) y el crédito que se muestra al leer.
/// Una fila por obra e idioma. El texto traducido vive en WorkChapterTranslation.
/// </summary>
public class WorkTranslationSettings : AuditableEntity
{
    public Guid WorkId { get; set; }
    /// <summary>Código del idioma destino ("en"). Lista permitida: WorkTranslationService.Languages.</summary>
    public string Language { get; set; } = "en";
    public string? Brief { get; set; }
    /// <summary>Una entrada por línea: "hiedra = ivy".</summary>
    public string? Glossary { get; set; }
    /// <summary>Lo que sale al pie de la traducción ("Traducción del autor"). null = la web pone el texto por defecto.</summary>
    public string? Credit { get; set; }

    public Work? Work { get; set; }
}

/// <summary>
/// Traducción de un capítulo a un idioma. Es a la vez la cola (Status = queued) y el texto final.
///
/// Flujo: queued → generating → draft (borrador del modelo, sin revisar) → published (el autor la revisó y la
/// publicó). failed guarda el motivo en Error. El modelo propone; el autor firma: nada pasa a published
/// sin que el autor lo pida.
///
/// SourceHash es la huella del capítulo original (título + texto) con la que se hizo o revisó esta
/// traducción. Si el autor cambia el original, ya no coincide y el panel la marca "desactualizada".
/// </summary>
public class WorkChapterTranslation : AuditableEntity
{
    public Guid WorkId { get; set; }
    public Guid ChapterId { get; set; }
    public string Language { get; set; } = "en";

    public string? Title { get; set; }
    /// <summary>HTML enriquecido, saneado con HtmlScrub igual que el original.</summary>
    public string? ContentHtml { get; set; }
    public int WordCount { get; set; } = 0;

    /// <summary>"queued" | "generating" | "draft" | "published" | "failed".</summary>
    public string Status { get; set; } = "queued";
    public string? SourceHash { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; } = 0;

    /// <summary>Modelo que hizo el borrador y lo que costó (registro AgentModels, USD).</summary>
    public string? ModelId { get; set; }
    public int TokensInput { get; set; } = 0;
    public int TokensOutput { get; set; } = 0;
    public decimal CostUsd { get; set; } = 0m;

    public DateTime? GeneratedAt { get; set; }
    /// <summary>Última vez que el autor guardó cambios a mano. Con valor, regenerar pide confirmación.</summary>
    public DateTime? ReviewedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public Work? Work { get; set; }
    public WorkChapter? Chapter { get; set; }
}
