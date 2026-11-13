using System.Text.Json;

namespace Maalca.Application.Agents;

// Los cinco contratos del orquestador. El núcleo (AgentRunner) solo conoce estas interfaces;
// un conector nuevo implementa una y se registra en AgentEndpoints.AddAgentServices.

// ---------- 1. Modelo ----------

public record ModelToolSpec(string Name, string Description, string InputSchemaJson);

public record ModelToolCall(string Id, string Name, JsonElement Input);

public record ModelToolResult(string ToolCallId, string Content, bool IsError);

/// <summary>
/// Un turno de la conversación. Tres formas: texto simple; turno del asistente con llamadas a
/// herramientas (ToolCalls); o turno del usuario con los resultados de esas llamadas (ToolResults).
/// </summary>
public class ModelMessage
{
    public string Role { get; init; } = "user";
    public string? Text { get; init; }
    public IReadOnlyList<ModelToolCall> ToolCalls { get; init; } = Array.Empty<ModelToolCall>();
    public IReadOnlyList<ModelToolResult> ToolResults { get; init; } = Array.Empty<ModelToolResult>();
}

public record ModelRequest(
    string BaseUrl,
    string ApiKey,
    string ModelId,
    string SystemPrompt,
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<ModelToolSpec> Tools,
    int MaxOutputTokens);

public record ModelResponse(
    string? Text,
    IReadOnlyList<ModelToolCall> ToolCalls,
    int TokensInput,
    int TokensOutput,
    string? StopReason);

/// <summary>Quién piensa. Una implementación por dialecto de API (AgentProvider.Kind).</summary>
public interface IAgentModelClient
{
    string Kind { get; }
    Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken ct);
}

public class AgentModelException : Exception
{
    public int? StatusCode { get; }

    public AgentModelException(int? statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

// ---------- 2. Herramienta ----------

/// <param name="InternalAgent">true cuando el agente es de uso interno de MaalCa (Visibility = internal).</param>
public record AgentToolContext(Guid? AffiliateId, string? RequestedBy, bool InternalAgent);

/// <summary>
/// Qué puede hacer un agente. El modelo elige la herramienta y sus parámetros; el código la ejecuta.
/// Un dato (precio, horario, venta) siempre sale de aquí, nunca del modelo.
/// </summary>
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    /// <summary>JSON Schema del objeto de entrada.</summary>
    string InputSchemaJson { get; }
    /// <summary>true = solo la pueden usar agentes internos (ve datos de toda la plataforma).</summary>
    bool InternalOnly { get; }
    Task<string> ExecuteAsync(AgentToolContext context, JsonElement input, CancellationToken ct);
}

// ---------- 3. Conocimiento ----------

/// <summary>Qué sabe el agente de una marca: perfil, datos vivos, búsqueda en documentos. Conectores desde la fase 2.</summary>
public interface IKnowledgeSource
{
    string Name { get; }
    Task<string?> GetContextAsync(Guid affiliateId, string query, CancellationToken ct);
}

// ---------- 4. Medio ----------

public record MediaRequest(Guid AffiliateId, string Kind, string Prompt, string? OptionsJson);

/// <param name="Status">"ready", "pending" o "failed".</param>
public record MediaResult(string Status, string? Url, string? ExternalJobId, string? Error);

/// <summary>De dónde sale la imagen, el video o la voz. Conectores desde la fase 3 (el primero es la subida manual).</summary>
public interface IMediaGenerator
{
    string Name { get; }
    /// <summary>Tipos que produce: "image", "video", "voice".</summary>
    IReadOnlyCollection<string> Kinds { get; }
    Task<MediaResult> GenerateAsync(MediaRequest request, CancellationToken ct);
    /// <summary>Para generaciones largas: el worker consulta el estado en vez de esperar.</summary>
    Task<MediaResult> GetStatusAsync(string externalJobId, CancellationToken ct);
}

// ---------- 5. Canal ----------

/// <summary>Lo que el canal sabe hacer. La pantalla de compartir muestra "subir a mano" cuando Manual = true.</summary>
public record PublishCapabilities(bool Image, bool Video, bool Schedule, bool Metrics, bool Manual);

public record PublishRequest(
    Guid AffiliateId,
    string Caption,
    string? MediaUrl,
    string? MediaKind,
    DateTime? PublishAt,
    DateTime? EndsAt);

/// <param name="Status">"published", "scheduled", "manual" o "failed".</param>
public record PublishResult(string Status, string? ExternalPostId, string? ExternalUrl, string? Error);

public record ChannelMetrics(long? Reach, long? Views, long? Reactions, long? Comments, long? Shares, long? Saves, long? Clicks);

/// <summary>Dónde se publica y de dónde vienen las métricas. Conectores desde la fase 4 (pantalla y manual con enlace).</summary>
public interface IPublishChannel
{
    string Name { get; }
    PublishCapabilities Capabilities { get; }
    Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken ct);
    Task<ChannelMetrics?> GetMetricsAsync(Guid affiliateId, string externalPostId, CancellationToken ct);
}
