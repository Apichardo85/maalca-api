using Maalca.Application.Agents;
using Maalca.Application.Agents.Tools;
using Maalca.Domain.Entities;

namespace Maalca.Api.Endpoints;

/// <summary>
/// Orquestador de agentes (fase 1): registro de servicios y endpoints de /ops.
/// Vive fuera de Program.cs a propósito; Program.cs solo llama a AddAgentServices y MapAgentEndpoints.
/// Un conector nuevo (modelo, herramienta, medio o canal) se registra aquí, en AddAgentServices.
/// </summary>
public static class AgentEndpoints
{
    public static void AddAgentServices(IServiceCollection services)
    {
        // El modelo puede tardar: más margen que el cliente HTTP por defecto de 100 s no hace falta,
        // pero se fija explícito para que no dependa de un cambio global.
        services.AddHttpClient(AnthropicModelClient.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(120));

        // Contrato de modelo
        services.AddScoped<IAgentModelClient, AnthropicModelClient>();

        // Contrato de herramienta
        services.AddScoped<IAgentTool, OpsOverviewTool>();
        services.AddScoped<IAgentTool, OpsAffiliatesTool>();

        services.AddScoped<IAgentRunner, AgentRunner>();
        services.AddScoped<IAgentAdminService, AgentAdminService>();
        services.AddHostedService<Maalca.Api.Workers.AgentJobWorker>();
    }

    public static void MapAgentEndpoints(WebApplication app)
    {
        // Todos requieren platform_admin == "true" (claim resuelto en SupabaseAuthMiddleware), igual que
        // el resto de /api/ops. Cambiar fichas, modelos, llaves o la pausa exige además rol Owner.

        app.MapGet("/api/ops/agents", async (HttpContext ctx, IAgentAdminService admin) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return Results.Ok(await admin.GetStateAsync());
        });

        app.MapGet("/api/ops/agents/jobs", async (HttpContext ctx, IAgentAdminService admin, int? limit, string? agent, string? status) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return Results.Ok(await admin.GetJobsAsync(limit ?? 50, agent, status));
        });

        app.MapPut("/api/ops/agents/definitions/{key}", async (HttpContext ctx, IAgentAdminService admin, string key, UpsertAgentDefinitionRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            if (!IsOwner(ctx)) return OwnerOnly();
            try
            {
                return Results.Ok(await admin.UpsertAgentAsync(key, request, Email(ctx)));
            }
            catch (ArgumentException ex)
            {
                return Invalid(ex);
            }
        });

        app.MapPut("/api/ops/agents/models/{tier}", async (HttpContext ctx, IAgentAdminService admin, string tier, UpsertAgentModelRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            if (!IsOwner(ctx)) return OwnerOnly();
            try
            {
                return Results.Ok(await admin.UpsertModelAsync(tier, request, Email(ctx)));
            }
            catch (ArgumentException ex)
            {
                return Invalid(ex);
            }
        });

        app.MapPut("/api/ops/agents/providers/{key}", async (HttpContext ctx, IAgentAdminService admin, string key, UpsertAgentProviderRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            if (!IsOwner(ctx)) return OwnerOnly();
            try
            {
                return Results.Ok(await admin.UpsertProviderAsync(key, request, Email(ctx)));
            }
            catch (ArgumentException ex)
            {
                return Invalid(ex);
            }
        });

        app.MapPut("/api/ops/agents/pause", async (HttpContext ctx, IAgentAdminService admin, SetAgentPauseRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            if (!IsOwner(ctx)) return OwnerOnly();
            try
            {
                return Results.Ok(await admin.SetPauseAsync(request, Email(ctx)));
            }
            catch (ArgumentException ex)
            {
                return Invalid(ex);
            }
        });

        // Chat con un agente interno. Reemplaza a la llamada directa a Anthropic que hacía
        // maalca-web en /api/ops/assistant: misma entrada ({ messages }) y misma salida ({ reply }).
        app.MapPost("/api/ops/agents/{key}/chat", async (HttpContext ctx, IAgentRunner runner, string key, AgentChatRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            if (request.Messages is null || request.Messages.Count == 0)
                return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "Faltan mensajes." } });

            var job = await runner.RunChatAsync(key.Trim().ToLowerInvariant(), request.AffiliateId, request.Messages, Email(ctx), ctx.RequestAborted);

            if (job.Status == AgentJobStatus.Succeeded)
            {
                return Results.Ok(new AgentChatResponse(job.ResultText ?? string.Empty, job.Id, job.ModelId,
                    job.TokensInput, job.TokensOutput, job.CostUsd));
            }

            var statusCode = job.ErrorCode switch
            {
                "AGENT_NOT_FOUND" => 404,
                "PAUSED" => 409,
                "INVALID_INPUT" => 400,
                "NOT_CONFIGURED" or "MODEL_NOT_CONFIGURED" => 503,
                _ => 502,
            };
            // El detalle del proveedor queda en AgentJobs (y en /ops); al navegador va un mensaje fijo.
            var message = job.ErrorCode is "UPSTREAM_ERROR" or "ERROR" or "MAX_TURNS" or "CANCELLED"
                ? "El asistente no pudo responder."
                : job.Error ?? "El asistente no pudo responder.";
            return Results.Json(new { error = new { code = job.ErrorCode ?? "ERROR", message }, jobId = job.Id }, statusCode: statusCode);
        });
    }

    private static bool IsAdmin(HttpContext ctx) => ctx.User.FindFirst("platform_admin")?.Value == "true";

    private static bool IsOwner(HttpContext ctx) =>
        ctx.User.FindFirst("platform_role")?.Value == nameof(PlatformAdminRole.Owner);

    private static string? Email(HttpContext ctx) => ctx.User.FindFirst("email")?.Value;

    private static IResult OwnerOnly() =>
        Results.Json(new { error = new { code = "OWNER_ONLY", message = "Solo un Owner puede cambiar la configuración de agentes." } }, statusCode: 403);

    private static IResult Invalid(ArgumentException ex) =>
        Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
}

public record AgentChatRequest(List<AgentChatMessage>? Messages, Guid? AffiliateId);

public record AgentChatResponse(string Reply, Guid JobId, string? ModelId, int TokensInput, int TokensOutput, decimal CostUsd);
