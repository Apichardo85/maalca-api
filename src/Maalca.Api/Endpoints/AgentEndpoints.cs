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
        services.AddScoped<IAgentTool, BusinessCatalogTool>();
        services.AddScoped<IKnowledgeSource, BrandKnowledgeSource>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IAgentTool, ContentPlanStatusTool>();
        services.AddScoped<IAgentTool, ContentSaveDraftsTool>();
        services.AddScoped<IAgentTool, ContentListDraftsTool>();
        services.AddScoped<IAgentTool, ContentRecordReviewsTool>();
        services.AddScoped<IContentStudioService, ContentStudioService>();

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

        // ---- Fase 3: estudio de contenido (bandeja del staff) ----
        // Cualquier admin del staff puede generar, editar y decidir: es el trabajo diario de marketing.
        app.MapGet("/api/ops/content", async (HttpContext ctx, IContentStudioService content, Guid? affiliateId, string? month, string? status, int? limit) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => content.ListAsync(affiliateId, month, status, limit ?? 100));
        });

        app.MapGet("/api/ops/content/quota", async (HttpContext ctx, IContentStudioService content, Guid affiliateId, string month) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => content.GetQuotaAsync(affiliateId, month));
        });

        app.MapPost("/api/ops/content", async (HttpContext ctx, IContentStudioService content, CreateContentItemRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => content.CreateAsync(request, Email(ctx)));
        });

        app.MapPut("/api/ops/content/{id:guid}", async (HttpContext ctx, IContentStudioService content, Guid id, UpdateContentItemRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => content.UpdateAsync(id, request, Email(ctx)));
        });

        app.MapPost("/api/ops/content/{id:guid}/decision", async (HttpContext ctx, IContentStudioService content, Guid id, ContentDecisionRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => content.DecideAsync(id, request, Email(ctx)));
        });

        // Encola el trabajo; el AgentJobWorker lo toma en segundos y los borradores aparecen en la bandeja.
        app.MapPost("/api/ops/content/plan", async (HttpContext ctx, IContentStudioService content, RequestPlanRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => content.RequestPlanAsync(request, Email(ctx)));
        });

        app.MapPost("/api/ops/content/review", async (HttpContext ctx, IContentStudioService content, RequestReviewRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => content.RequestReviewAsync(request, Email(ctx)));
        });

        // ---- Fase 2: marca y plan por negocio ----
        // La marca la edita cualquier admin (el equipo de marketing no es Owner); el plan y el tope de tokens son dinero: solo Owner.
        app.MapGet("/api/ops/agents/affiliates/{affiliateId:guid}/brand", async (HttpContext ctx, IBrandService brand, Guid affiliateId) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => brand.GetBrandAsync(affiliateId));
        });

        app.MapPut("/api/ops/agents/affiliates/{affiliateId:guid}/brand", async (HttpContext ctx, IBrandService brand, Guid affiliateId, UpsertBrandProfileRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => brand.UpsertBrandAsync(affiliateId, request, Email(ctx)));
        });

        app.MapGet("/api/ops/agents/affiliates/{affiliateId:guid}/plan", async (HttpContext ctx, IBrandService brand, Guid affiliateId) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            return await Guard(() => brand.GetPlanAsync(affiliateId));
        });

        app.MapPut("/api/ops/agents/affiliates/{affiliateId:guid}/plan", async (HttpContext ctx, IBrandService brand, Guid affiliateId, UpsertAgentAffiliatePlanRequest request) =>
        {
            if (!IsAdmin(ctx)) return Results.Forbid();
            if (!IsOwner(ctx)) return OwnerOnly();
            return await Guard(() => brand.UpsertPlanAsync(affiliateId, request, Email(ctx)));
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
                "NOT_ENABLED" => 403,
                "QUOTA_EXCEEDED" => 429,
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

    private static async Task<IResult> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (ArgumentException ex)
        {
            return Invalid(ex);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
        }
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
