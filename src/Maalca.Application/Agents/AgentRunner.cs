using System.Diagnostics;
using System.Text.Json;
using Maalca.Application.Common;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Agents;

public static class AgentJobStatus
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
}

public record AgentChatMessage(string Role, string Content);

public interface IAgentRunner
{
    /// <summary>Ejecuta un chat en la misma petición y devuelve el trabajo ya terminado (con su costo).</summary>
    Task<AgentJob> RunChatAsync(string agentKey, Guid? affiliateId, IReadOnlyList<AgentChatMessage> messages,
        string? requestedBy, CancellationToken ct);

    /// <summary>Deja un trabajo en la cola para que lo tome AgentJobWorker.</summary>
    Task<Guid> EnqueueAsync(string agentKey, Guid? affiliateId, IReadOnlyList<AgentChatMessage> messages,
        string? requestedBy, DateTime? runAfter, CancellationToken ct);

    /// <summary>Ejecuta un trabajo que el worker ya marcó como running.</summary>
    Task ExecuteClaimedAsync(Guid jobId, CancellationToken ct);
}

/// <summary>
/// El núcleo: carga la ficha del agente, resuelve modelo y proveedor, corre el ciclo
/// modelo → herramientas → modelo y deja registrado el resultado y el costo en AgentJobs.
/// Solo conoce los contratos (IAgentModelClient, IAgentTool); no sabe de Anthropic ni de /ops.
/// </summary>
public class AgentRunner : IAgentRunner
{
    /// <summary>Tope de idas y vueltas modelo ↔ herramientas por trabajo, para que un agente no gire sin fin.</summary>
    private const int MaxTurns = 6;
    /// <summary>Tope de lo que una herramienta puede devolverle al modelo (caracteres).</summary>
    private const int MaxToolResultChars = 120_000;
    private const int MaxMessages = 40;
    private const int MaxMessageChars = 20_000;

    private readonly AppDbContext _db;
    private readonly IEnumerable<IAgentModelClient> _clients;
    private readonly IEnumerable<IAgentTool> _tools;
    private readonly IEnumerable<IKnowledgeSource> _knowledge;
    private readonly IConfiguration _config;
    private readonly ILogger<AgentRunner> _logger;
    private readonly string _secret;

    public AgentRunner(AppDbContext db, IEnumerable<IAgentModelClient> clients, IEnumerable<IAgentTool> tools,
        IEnumerable<IKnowledgeSource> knowledge, IConfiguration config, ILogger<AgentRunner> logger)
    {
        _db = db;
        _clients = clients;
        _tools = tools;
        _knowledge = knowledge;
        _config = config;
        _logger = logger;
        _secret = config["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
    }

    public async Task<AgentJob> RunChatAsync(string agentKey, Guid? affiliateId, IReadOnlyList<AgentChatMessage> messages,
        string? requestedBy, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var job = new AgentJob
        {
            AgentKey = agentKey,
            AffiliateId = affiliateId,
            Kind = "chat",
            InputJson = SerializeInput(messages),
            Status = AgentJobStatus.Running,
            Attempts = 1,
            MaxAttempts = 1,
            RunAfter = now,
            LockedAt = now,
            StartedAt = now,
            RequestedBy = requestedBy,
        };
        _db.AgentJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        await ExecuteAsync(job, ct);
        return job;
    }

    public async Task<Guid> EnqueueAsync(string agentKey, Guid? affiliateId, IReadOnlyList<AgentChatMessage> messages,
        string? requestedBy, DateTime? runAfter, CancellationToken ct)
    {
        var job = new AgentJob
        {
            AgentKey = agentKey,
            AffiliateId = affiliateId,
            Kind = "task",
            InputJson = SerializeInput(messages),
            Status = AgentJobStatus.Queued,
            RunAfter = runAfter ?? DateTime.UtcNow,
            RequestedBy = requestedBy,
        };
        _db.AgentJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return job.Id;
    }

    public async Task ExecuteClaimedAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.AgentJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || job.Status != AgentJobStatus.Running) return;
        await ExecuteAsync(job, ct);
    }

    private async Task ExecuteAsync(AgentJob job, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        AgentModel? model = null;
        try
        {
            var def = await _db.AgentDefinitions.AsNoTracking().FirstOrDefaultAsync(a => a.Key == job.AgentKey, ct);
            if (def is null || !def.IsActive)
            {
                Fail(job, "AGENT_NOT_FOUND", "Ese agente no existe o está desactivado.");
                return;
            }

            var affiliateScope = job.AffiliateId?.ToString();
            var paused = await _db.AgentControls.AsNoTracking()
                .AnyAsync(c => c.Paused && (c.Scope == "global" || c.Scope == affiliateScope), ct);
            if (paused)
            {
                job.Status = AgentJobStatus.Blocked;
                job.ErrorCode = "PAUSED";
                job.Error = "Los agentes están en pausa.";
                return;
            }

            // Agentes de cliente (asistente del dueño): solo con el add-on activo y dentro del tope mensual de tokens.
            if (def.Visibility == "client")
            {
                if (job.AffiliateId is not Guid clientAffiliate)
                {
                    Fail(job, "INVALID_INPUT", "Este agente necesita un negocio.");
                    return;
                }
                var plan = await _db.AgentAffiliatePlans.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == clientAffiliate, ct);
                if (plan is null || !plan.AssistantEnabled)
                {
                    job.Status = AgentJobStatus.Blocked;
                    job.ErrorCode = "NOT_ENABLED";
                    job.Error = "El asistente no está activo para este negocio.";
                    return;
                }
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var used = await AgentQuota.TokensUsedSinceAsync(_db, clientAffiliate, monthStart);
                if (used >= plan.MonthlyTokenCap)
                {
                    job.Status = AgentJobStatus.Blocked;
                    job.ErrorCode = "QUOTA_EXCEEDED";
                    job.Error = "Este negocio llegó al tope mensual de uso del asistente.";
                    return;
                }
            }

            model = await _db.AgentModels.AsNoTracking().FirstOrDefaultAsync(m => m.Tier == def.ModelTier && m.IsActive, ct);
            if (model is null)
            {
                Fail(job, "MODEL_NOT_CONFIGURED", $"No hay un modelo activo para el nivel \"{def.ModelTier}\".");
                return;
            }
            job.ModelId = model.ModelId;

            var provider = await _db.AgentProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Key == model.ProviderKey && p.IsActive, ct);
            if (provider is null)
            {
                Fail(job, "NOT_CONFIGURED", $"El proveedor \"{model.ProviderKey}\" no existe o está desactivado.");
                return;
            }

            var client = _clients.FirstOrDefault(c => c.Kind == provider.Kind);
            if (client is null)
            {
                Fail(job, "NOT_CONFIGURED", $"No hay un cliente para proveedores de tipo \"{provider.Kind}\".");
                return;
            }

            var apiKey = ResolveApiKey(provider);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Fail(job, "NOT_CONFIGURED", $"Falta la llave del proveedor \"{provider.Key}\". Se configura en /ops.");
                return;
            }

            var internalAgent = def.Visibility == "internal";
            var allowed = def.Tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var tools = _tools
                .Where(t => allowed.Contains(t.Name) && (!t.InternalOnly || internalAgent))
                .ToList();
            var toolSpecs = tools.Select(t => new ModelToolSpec(t.Name, t.Description, t.InputSchemaJson)).ToList();
            var context = new AgentToolContext(job.AffiliateId, job.RequestedBy, internalAgent);

            var messages = ParseInput(job.InputJson);
            if (messages.Count == 0)
            {
                Fail(job, "INVALID_INPUT", "El trabajo no trae mensajes.");
                return;
            }

            // Conocimiento del negocio (perfil de marca, etc.) se suma al prompt solo si el trabajo trae un negocio.
            var systemPrompt = def.SystemPrompt;
            if (job.AffiliateId is Guid knowledgeAffiliate)
            {
                var lastUserText = messages.LastOrDefault(m => m.Role == "user")?.Text ?? string.Empty;
                var blocks = new List<string>();
                foreach (var source in _knowledge)
                {
                    try
                    {
                        var block = await source.GetContextAsync(knowledgeAffiliate, lastUserText, ct);
                        if (!string.IsNullOrWhiteSpace(block)) blocks.Add(block);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "La fuente de conocimiento {Source} falló; el agente sigue sin ella.", source.Name);
                    }
                }
                if (blocks.Count > 0)
                    systemPrompt += "\n\n# Contexto del negocio (datos de MaalCa; no lo contradigas)\n" + string.Join("\n\n", blocks);
            }

            string? finalText = null;
            for (var turn = 0; turn < MaxTurns; turn++)
            {
                var response = await client.CompleteAsync(
                    new ModelRequest(provider.BaseUrl, apiKey, model.ModelId, systemPrompt, messages, toolSpecs, def.MaxOutputTokens),
                    ct);
                job.TokensInput += response.TokensInput;
                job.TokensOutput += response.TokensOutput;

                if (response.ToolCalls.Count == 0)
                {
                    finalText = response.Text ?? string.Empty;
                    break;
                }

                messages.Add(new ModelMessage { Role = "assistant", Text = response.Text, ToolCalls = response.ToolCalls });

                var results = new List<ModelToolResult>();
                foreach (var call in response.ToolCalls)
                {
                    job.ToolCalls++;
                    var tool = tools.FirstOrDefault(t => string.Equals(t.Name, call.Name, StringComparison.OrdinalIgnoreCase));
                    if (tool is null)
                    {
                        results.Add(new ModelToolResult(call.Id, $"La herramienta \"{call.Name}\" no está disponible para este agente.", true));
                        continue;
                    }
                    try
                    {
                        var output = await tool.ExecuteAsync(context, call.Input, ct);
                        if (output.Length > MaxToolResultChars)
                            output = output[..MaxToolResultChars] + "\n[resultado recortado]";
                        results.Add(new ModelToolResult(call.Id, output, false));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Agents] la herramienta {Tool} falló en el trabajo {JobId}", call.Name, job.Id);
                        results.Add(new ModelToolResult(call.Id, "La herramienta falló: " + ex.Message, true));
                    }
                }
                messages.Add(new ModelMessage { Role = "user", ToolResults = results });
            }

            if (finalText is null)
            {
                Fail(job, "MAX_TURNS", $"El agente no terminó en {MaxTurns} pasos.");
                return;
            }

            job.Status = AgentJobStatus.Succeeded;
            job.ResultText = finalText;
            job.ErrorCode = null;
            job.Error = null;
        }
        catch (AgentModelException ex)
        {
            _logger.LogWarning("[Agents] el proveedor falló en el trabajo {JobId}: {Message}", job.Id, ex.Message);
            // 429 y 5xx (o sin respuesta) son pasajeros: un trabajo en cola se reintenta con espera creciente.
            var transient = ex.StatusCode is null or 429 or >= 500;
            if (job.Kind != "chat" && transient && job.Attempts < job.MaxAttempts)
            {
                job.Status = AgentJobStatus.Queued;
                job.RunAfter = DateTime.UtcNow.AddSeconds(30 * job.Attempts);
                job.LockedAt = null;
                job.ErrorCode = "UPSTREAM_ERROR";
                job.Error = ex.Message;
            }
            else
            {
                Fail(job, "UPSTREAM_ERROR", ex.Message);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado del servidor o cliente que cortó: el trabajo en cola vuelve a quedar disponible.
            if (job.Kind != "chat")
            {
                job.Status = AgentJobStatus.Queued;
                job.LockedAt = null;
            }
            else
            {
                Fail(job, "CANCELLED", "La petición se canceló antes de terminar.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Agents] error inesperado en el trabajo {JobId}", job.Id);
            Fail(job, "ERROR", ex.Message);
        }
        finally
        {
            sw.Stop();
            var now = DateTime.UtcNow;
            job.DurationMs += sw.ElapsedMilliseconds;
            job.UpdatedAt = now;
            if (job.Status != AgentJobStatus.Queued)
                job.FinishedAt = now;
            if (model is not null)
            {
                job.CostUsd = (job.TokensInput * model.InputPricePerMTok + job.TokensOutput * model.OutputPricePerMTok) / 1_000_000m;
            }
            // Sin el token de la petición: el costo ya se gastó y tiene que quedar registrado aunque el cliente corte.
            await _db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static void Fail(AgentJob job, string code, string message)
    {
        job.Status = AgentJobStatus.Failed;
        job.ErrorCode = code;
        job.Error = message.Length > 2000 ? message[..2000] : message;
    }

    /// <summary>Llave guardada desde /ops (cifrada); si no hay, la variable de entorno del API como respaldo.</summary>
    private string? ResolveApiKey(AgentProvider provider)
    {
        var stored = SecretBox.Unprotect(provider.ApiKeyEnc, _secret);
        if (!string.IsNullOrWhiteSpace(stored)) return stored;
        if (provider.Kind != "anthropic") return null;
        return _config["Anthropic:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    private static string SerializeInput(IReadOnlyList<AgentChatMessage> messages)
    {
        var clean = messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(MaxMessages)
            .Select(m => new
            {
                role = m.Role == "assistant" ? "assistant" : "user",
                content = m.Content.Length > MaxMessageChars ? m.Content[..MaxMessageChars] : m.Content,
            })
            .ToList();
        return JsonSerializer.Serialize(new { messages = clean });
    }

    private static List<ModelMessage> ParseInput(string inputJson)
    {
        var result = new List<ModelMessage>();
        try
        {
            using var doc = JsonDocument.Parse(inputJson);
            if (!doc.RootElement.TryGetProperty("messages", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return result;
            foreach (var m in arr.EnumerateArray())
            {
                var role = m.TryGetProperty("role", out var r) ? r.GetString() : null;
                var content = m.TryGetProperty("content", out var c) ? c.GetString() : null;
                if (string.IsNullOrWhiteSpace(content)) continue;
                result.Add(new ModelMessage { Role = role == "assistant" ? "assistant" : "user", Text = content });
            }
        }
        catch (JsonException)
        {
            // Entrada corrupta: se devuelve vacío y el trabajo falla con INVALID_INPUT.
        }
        // El API exige que la conversación empiece con un turno del usuario.
        while (result.Count > 0 && result[0].Role != "user")
            result.RemoveAt(0);
        return result;
    }
}
