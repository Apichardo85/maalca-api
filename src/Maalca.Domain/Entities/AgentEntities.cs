using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

// Núcleo del orquestador de agentes (fase 1). No confundir con AgentExecution (AdditionalEntities.cs),
// que es la bitácora antigua de los agentes de código de n8n y no se usa aquí.

/// <summary>
/// Ficha de un agente: qué rol tiene, qué herramientas puede usar y qué nivel de modelo usa.
/// Se edita desde /ops; no hay agentes definidos en código.
/// </summary>
public class AgentDefinition : AuditableEntity
{
    /// <summary>Identificador estable en minúsculas con guiones, p. ej. "ops-assistant".</summary>
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    /// <summary>Nivel de modelo (AgentModel.Tier): fast, standard o premium.</summary>
    public string ModelTier { get; set; } = "standard";
    /// <summary>Nombres de herramientas permitidas, separados por coma. Vacío = sin herramientas.</summary>
    public string Tools { get; set; } = string.Empty;
    /// <summary>"internal" (solo staff de MaalCa) o "client" (lo usa el dueño del negocio).</summary>
    public string Visibility { get; set; } = "internal";
    public int MaxOutputTokens { get; set; } = 1024;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Proveedor de modelos (Anthropic hoy; un servidor local después). La llave se guarda cifrada con SecretBox.
/// </summary>
public class AgentProvider : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    /// <summary>Dialecto del API que habla: "anthropic". Decide qué IAgentModelClient lo atiende.</summary>
    public string Kind { get; set; } = "anthropic";
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public string? ApiKeyEnc { get; set; }
    public string? ApiKeyLast4 { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Registro de modelos: qué modelo concreto atiende cada nivel y cuánto cuesta (USD por millón de tokens).
/// Cambiar de modelo es editar esta fila, no los agentes.
/// </summary>
public class AgentModel : AuditableEntity
{
    public string Tier { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = "anthropic";
    public string ModelId { get; set; } = string.Empty;
    public decimal InputPricePerMTok { get; set; }
    public decimal OutputPricePerMTok { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Un trabajo de agente. Es a la vez la cola (Status = queued) y la bitácora de costo de cada ejecución.
/// Los chats de /ops se ejecutan en la misma petición; el resto lo toma AgentJobWorker.
/// </summary>
public class AgentJob : BaseEntity
{
    public string AgentKey { get; set; } = string.Empty;
    public Guid? AffiliateId { get; set; }
    /// <summary>"chat" (respuesta inmediata) o "task" (en cola).</summary>
    public string Kind { get; set; } = "chat";
    /// <summary>JSON: { "messages": [ { "role": "user", "content": "..." } ] }.</summary>
    public string InputJson { get; set; } = "{}";
    /// <summary>queued, running, succeeded, failed o blocked (agentes en pausa).</summary>
    public string Status { get; set; } = "queued";
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTime RunAfter { get; set; } = DateTime.UtcNow;
    public DateTime? LockedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ResultText { get; set; }
    public string? ErrorCode { get; set; }
    public string? Error { get; set; }
    public string? ModelId { get; set; }
    public int TokensInput { get; set; }
    public int TokensOutput { get; set; }
    public decimal CostUsd { get; set; }
    public long DurationMs { get; set; }
    public int ToolCalls { get; set; }
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Botón de pausa. Scope = "global" detiene todos los agentes; Scope = id de un afiliado detiene solo ese negocio.
/// </summary>
public class AgentControl : BaseEntity
{
    public string Scope { get; set; } = "global";
    public bool Paused { get; set; }
    public string? Reason { get; set; }
    public string? UpdatedBy { get; set; }
}

// ---------- Fase 2: marca y plan por negocio ----------

/// <summary>
/// Perfil de marca de un negocio: lo que un agente de marketing necesita saber para escribir "como ellos".
/// Un registro por negocio. Se edita desde /ops/negocios/{id}/marca; el dueño lo verá cuando abramos el add-on.
/// Los datos que cambian (precios, menú, horario) NO van aquí: el agente los lee de la base con herramientas.
/// </summary>
public class BrandProfile : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    /// <summary>Cómo habla la marca (tono, palabras que usa, nivel de formalidad).</summary>
    public string? Voice { get; set; }
    /// <summary>A quién le habla.</summary>
    public string? Audience { get; set; }
    /// <summary>Qué vende y qué lo hace distinto.</summary>
    public string? Offer { get; set; }
    /// <summary>Qué sí hacer en sus publicaciones.</summary>
    public string? Dos { get; set; }
    /// <summary>Qué nunca decir o mostrar.</summary>
    public string? Donts { get; set; }
    /// <summary>Fechas y temporadas que le importan al negocio (texto libre, una por línea).</summary>
    public string? SpecialDates { get; set; }
    public string? Hashtags { get; set; }
    /// <summary>"es", "en" o "es,en".</summary>
    public string Language { get; set; } = "es";
    public string? Notes { get; set; }
}

/// <summary>
/// Lo que un negocio tiene contratado en agentes: cuota de marketing y, si se activa el add-on,
/// el asistente del dueño con su tope mensual de tokens. Sin fila = no tiene nada.
/// El código hace cumplir estos números; no dependen de que el modelo "se acuerde".
/// </summary>
public class AgentAffiliatePlan : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    /// <summary>Publicaciones incluidas por mes (p. ej. 8).</summary>
    public int PostsPerMonth { get; set; }
    /// <summary>Redes incluidas, separadas por coma: facebook, instagram, tiktok, youtube.</summary>
    public string Networks { get; set; } = string.Empty;
    /// <summary>Add-on del asistente para el dueño.</summary>
    public bool AssistantEnabled { get; set; }
    /// <summary>Tope duro de tokens (entrada + salida) por mes para los agentes de cliente. 0 = sin cupo.</summary>
    public long MonthlyTokenCap { get; set; }
}
