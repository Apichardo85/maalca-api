using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Levantamiento de datos de un prospecto (negocio que todavía no tiene espacio, o que lo está
/// armando con nosotros). MaalCa lo crea desde /ops con su lista de preguntas, comparte el enlace
/// /levantamiento/{token} y el prospecto responde en una página con la marca de MaalCa — sin login,
/// sin correos de ida y vuelta, sin copiar datos a mano.
///
/// Es de la PLATAFORMA (sin AffiliateId), igual que PlatformContent: el prospecto aún no es afiliado.
/// Las preguntas y respuestas van como JSON porque cada levantamiento lleva su propia lista
/// (editable en /ops antes de enviarlo); no hay consultas por pregunta, así que no vale la pena
/// normalizarlas en tablas.
/// </summary>
public class ProspectIntake : BaseEntity
{
    /// <summary>Token público de /levantamiento/{token}. Aparte del Id para poder rotarlo.</summary>
    public Guid Token { get; set; } = Guid.NewGuid();

    public string BusinessName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    /// <summary>Título que ve el prospecto (ej. "Levantamiento para tu espacio en MaalCa").</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Mensaje de bienvenida en la cabecera del formulario.</summary>
    public string? IntroMessage { get; set; }

    /// <summary>Asesor de MaalCa que acompaña al prospecto (nombre y teléfono/correo visibles en el formulario).</summary>
    public string? AdvisorName { get; set; }
    public string? AdvisorContact { get; set; }

    /// <summary>"es" | "en" — idioma de los textos fijos del formulario (botones, avisos).</summary>
    public string Language { get; set; } = "es";

    /// <summary>JSON: [{ key, label, type, section?, help?, required?, options? }].</summary>
    public string QuestionsJson { get; set; } = "[]";
    /// <summary>JSON: { [key]: string | string[] | { path, name } }. Se guarda en cada autoguardado.</summary>
    public string? AnswersJson { get; set; }

    /// <summary>Ver ProspectIntakeStatus.</summary>
    public string Status { get; set; } = ProspectIntakeStatus.Open;
    public DateTime? LastSavedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedIp { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Correo del admin de plataforma que lo creó — recibe el aviso cuando el prospecto envía.</summary>
    public string CreatedByEmail { get; set; } = string.Empty;
    /// <summary>Notas internas (solo /ops).</summary>
    public string? Notes { get; set; }
}

/// <summary>Estados del levantamiento. Constantes (no texto libre) para que web y API no diverjan.</summary>
public static class ProspectIntakeStatus
{
    /// <summary>Enlace activo: el prospecto puede responder y guardar.</summary>
    public const string Open = "open";
    /// <summary>El prospecto lo envió; queda de solo lectura hasta que /ops lo reabra.</summary>
    public const string Submitted = "submitted";
    /// <summary>MaalCa ya revisó las respuestas.</summary>
    public const string Reviewed = "reviewed";
    /// <summary>Cerrado sin uso (enlace inactivo).</summary>
    public const string Closed = "closed";

    public static readonly string[] All = { Open, Submitted, Reviewed, Closed };
}
