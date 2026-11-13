using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Fiscal;
using Maalca.Application.Services.Fiscal;

namespace Maalca.Api.Endpoints;

/// <summary>
/// Facturación electrónica (e-CF, DGII). Vive fuera de Program.cs a propósito; Program.cs solo llama a
/// AddFiscalServices y MapFiscalEndpoints. Cambiar de proveedor = registrar otra implementación de IFiscalProvider aquí.
/// Rutas bajo /api/affiliates/{id}/fiscal (solo dueño/gerente y el perfil de Contabilidad, ver AccessProfiles) y un
/// webhook anónimo que se autentica con una llave derivada por negocio.
/// </summary>
public static class FiscalEndpoints
{
    public static void AddFiscalServices(IServiceCollection services)
    {
        services.AddHttpClient(AlanubeProvider.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(60));
        services.AddScoped<IFiscalProvider, AlanubeProvider>();
        services.AddScoped<IFiscalService, FiscalService>();
        services.AddHostedService<Workers.FiscalSyncWorker>();
    }

    public static void MapFiscalEndpoints(WebApplication app)
    {
        var g = "/api/affiliates/{id:guid}/fiscal";

        app.MapGet(g, async (HttpContext ctx, IFiscalService svc, Guid id) =>
            !Allowed(ctx, id) ? Results.Forbid() : Results.Ok(await svc.GetOverviewAsync(id, PublicBase(ctx))));

        app.MapPut($"{g}/profile", (HttpContext ctx, IFiscalService svc, Guid id, FiscalProfileRequest body) =>
            Run(ctx, id, () => svc.UpsertProfileAsync(id, body, Email(ctx))));

        app.MapPost($"{g}/register", (HttpContext ctx, IFiscalService svc, Guid id, FiscalRegisterRequest body) =>
            Run(ctx, id, () => svc.RegisterCompanyAsync(id, body, PublicBase(ctx), Email(ctx))));

        app.MapPut($"{g}/sequences", (HttpContext ctx, IFiscalService svc, Guid id, FiscalSequenceRequest body) =>
            Run(ctx, id, () => svc.UpsertSequenceAsync(id, body, Email(ctx))));

        app.MapGet($"{g}/invoices", (HttpContext ctx, IFiscalService svc, Guid id, string? status, int? limit) =>
            Run(ctx, id, () => svc.ListInvoicesAsync(id, status, limit ?? 50)));

        app.MapGet($"{g}/invoices/{{invoiceId:guid}}", (HttpContext ctx, IFiscalService svc, Guid id, Guid invoiceId) =>
            Run(ctx, id, () => svc.GetInvoiceAsync(id, invoiceId)));

        app.MapPost($"{g}/invoices", (HttpContext ctx, IFiscalService svc, Guid id, EmitInvoiceRequest body) =>
            Run(ctx, id, () => svc.EmitFromOrderAsync(id, body)));

        app.MapPost($"{g}/invoices/{{invoiceId:guid}}/retry", (HttpContext ctx, IFiscalService svc, Guid id, Guid invoiceId) =>
            Run(ctx, id, () => svc.RetryAsync(id, invoiceId)));

        app.MapPost($"{g}/invoices/{{invoiceId:guid}}/refresh", (HttpContext ctx, IFiscalService svc, Guid id, Guid invoiceId) =>
            Run(ctx, id, () => svc.RefreshAsync(id, invoiceId)));

        // Webhook de Alanube (documents.emissionFinished). Se configura solo al dar de alta la empresa; la llave
        // llega en x-api-key y se deriva por negocio, así que no hay nada que guardar ni rotar. El mensaje de prueba
        // del proveedor también responde 200.
        app.MapPost("/api/webhooks/alanube/{affiliateId:guid}", async (HttpContext ctx, IFiscalService svc, Guid affiliateId) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var payload = await reader.ReadToEndAsync();
            var ok = await svc.HandleWebhookAsync(affiliateId, payload, ctx.Request.Headers["x-api-key"].FirstOrDefault());
            return ok ? Results.Ok() : Results.Unauthorized();
        })
        .AllowAnonymous();
    }

    private static bool Allowed(HttpContext ctx, Guid id) =>
        ctx.User.FindFirst("active_affiliate_id")?.Value == id.ToString() && ctx.User.FindFirst("role")?.Value != "Staff";

    private static string? Email(HttpContext ctx) => ctx.User.FindFirst("email")?.Value;

    // Detrás del proxy de Railway el esquema llega como http: el webhook siempre debe ser https salvo en local.
    private static string PublicBase(HttpContext ctx)
    {
        var host = ctx.Request.Host.Value;
        var local = host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || host.StartsWith("127.0.0.1");
        return $"{(local ? ctx.Request.Scheme : "https")}://{host}";
    }

    private static async Task<IResult> Run<T>(HttpContext ctx, Guid id, Func<Task<T>> action)
    {
        if (!Allowed(ctx, id)) return Results.Forbid();
        try
        {
            return Results.Ok(await action());
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
        }
        catch (FiscalProviderException ex)
        {
            // 502: el problema es del proveedor o de la DGII, no de la petición.
            return Results.Json(new { error = new { code = ex.Code, message = ex.Message, transient = ex.Transient } }, statusCode: 502);
        }
    }
}
