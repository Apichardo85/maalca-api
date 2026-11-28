using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Application.Creator;
using Maalca.Application.Services;

namespace Maalca.Api.Endpoints;

/// <summary>
/// Espacio de autor (BusinessType.Creator): obras y capítulos. Fuera de Program.cs a propósito;
/// Program.cs solo llama a AddWorkServices y MapWorkEndpoints. Ver Work.cs para la regla de lectura.
/// </summary>
public static class WorkEndpoints
{
    public static void AddWorkServices(IServiceCollection services)
    {
        services.AddScoped<IWorkService, WorkService>();
        // Traducción de obras: el panel pide borradores, el worker los hace (WorkTranslationService).
        services.AddScoped<IWorkTranslationService, WorkTranslationService>();
        services.AddHostedService<Maalca.Api.Workers.WorkTranslationWorker>();
        // Sello editorial (contratos autor ↔ sello) y exportación de libros (EPUB).
        services.AddScoped<IImprintService, ImprintService>();
        services.AddScoped<IWorkExportService, WorkExportService>();
        services.AddHttpClient(WorkExportService.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MaalCa-Export/1.0");
        });
    }

    public static void MapWorkEndpoints(WebApplication app)
    {
        // ── Dashboard del dueño (/space/[slug]/works) ─────────────────────────────
        // Leer: cualquier usuario del negocio activo. Escribir: dueño/gerente (Staff no).

        app.MapGet("/api/affiliates/{id:guid}/works", async (HttpContext ctx, IWorkService svc, Guid id) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            return Results.Ok(await svc.GetAsync(id));
        }).RequireAuthorization();

        app.MapGet("/api/affiliates/{id:guid}/works/{workId:guid}", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            var result = await svc.GetDetailAsync(id, workId);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization();

        app.MapPost("/api/affiliates/{id:guid}/works", async (HttpContext ctx, IWorkService svc, Guid id, UpsertWorkRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.CreateAsync(id, request);
                return Results.Created($"/api/affiliates/{id}/works/{result.Id}", result);
            });
        }).RequireAuthorization();

        app.MapPut("/api/affiliates/{id:guid}/works/{workId:guid}", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId, UpsertWorkRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.UpdateAsync(id, workId, request);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapDelete("/api/affiliates/{id:guid}/works/{workId:guid}", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await svc.DeleteAsync(id, workId) ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization();

        app.MapGet("/api/affiliates/{id:guid}/works/{workId:guid}/chapters/{chapterId:guid}", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId, Guid chapterId) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            var result = await svc.GetChapterAsync(id, workId, chapterId);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization();

        app.MapPost("/api/affiliates/{id:guid}/works/{workId:guid}/chapters", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId, UpsertWorkChapterRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.CreateChapterAsync(id, workId, request);
                return result is null ? Results.NotFound() : Results.Created($"/api/affiliates/{id}/works/{workId}/chapters/{result.Id}", result);
            });
        }).RequireAuthorization();

        app.MapPut("/api/affiliates/{id:guid}/works/{workId:guid}/chapters/{chapterId:guid}", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId, Guid chapterId, UpsertWorkChapterRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.UpdateChapterAsync(id, workId, chapterId, request);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapDelete("/api/affiliates/{id:guid}/works/{workId:guid}/chapters/{chapterId:guid}", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId, Guid chapterId) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await svc.DeleteChapterAsync(id, workId, chapterId) ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization();

        app.MapPost("/api/affiliates/{id:guid}/works/{workId:guid}/chapters-import", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId, ImportWorkChaptersRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.ImportChaptersAsync(id, workId, request);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapPut("/api/affiliates/{id:guid}/works/{workId:guid}/chapters-order", async (HttpContext ctx, IWorkService svc, Guid id, Guid workId, ReorderWorkChaptersRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.ReorderChaptersAsync(id, workId, request);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        // ── Traducción (WorkTranslationService) ───────────────────────────────────
        // Mismas reglas: leer cualquiera del negocio; pedir borradores, editar y publicar solo dueño/gerente.
        const string tr = "/api/affiliates/{id:guid}/works/{workId:guid}/translations/{lang}";

        app.MapGet(tr, async (HttpContext ctx, IWorkTranslationService svc, Guid id, Guid workId, string lang) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.GetOverviewAsync(id, workId, lang);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapPut(tr + "/settings", async (HttpContext ctx, IWorkTranslationService svc, Guid id, Guid workId, string lang, UpsertWorkTranslationSettingsRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.SaveSettingsAsync(id, workId, lang, request);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapPost(tr + "/queue", async (HttpContext ctx, IWorkTranslationService svc, Guid id, Guid workId, string lang, QueueWorkTranslationRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.QueueAsync(id, workId, lang, request);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapGet(tr + "/chapters/{chapterId:guid}", async (HttpContext ctx, IWorkTranslationService svc, Guid id, Guid workId, string lang, Guid chapterId) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.GetChapterAsync(id, workId, lang, chapterId);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapPut(tr + "/chapters/{chapterId:guid}", async (HttpContext ctx, IWorkTranslationService svc, Guid id, Guid workId, string lang, Guid chapterId, UpdateWorkChapterTranslationRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.UpdateChapterAsync(id, workId, lang, chapterId, request);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapPost(tr + "/chapters/{chapterId:guid}/publish", async (HttpContext ctx, IWorkTranslationService svc, Guid id, Guid workId, string lang, Guid chapterId, PublishWorkChapterTranslationRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var result = await svc.SetPublishedAsync(id, workId, lang, chapterId, request.Published);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        // ── Página pública del autor ──────────────────────────────────────────────
        // Borradores y capítulos cerrados nunca salen. Caché corta: la ventana gratis abre y cierra sola.

        // Con X-Reader-Token (sesión del club) la respuesta depende del lector: nunca se cachea en público.

        // ── Exportar (autor dueño o sello con el que sale la obra) ──────────────────
        app.MapGet("/api/affiliates/{id:guid}/works/{workId:guid}/export/preflight", async (HttpContext ctx, IWorkExportService svc, Guid id, Guid workId, string? lang, CancellationToken ct) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            var result = await svc.PreflightAsync(id, workId, lang, ct);
            return result is null ? NotFound() : Results.Ok(result);
        }).RequireAuthorization();

        app.MapGet("/api/affiliates/{id:guid}/works/{workId:guid}/export/epub", async (HttpContext ctx, IWorkExportService svc, Guid id, Guid workId, string? lang, CancellationToken ct) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            return await Guard(async () =>
            {
                var file = await svc.BuildEpubAsync(id, workId, lang, ct);
                if (file is null) return NotFound();
                ctx.Response.Headers.CacheControl = "no-store";
                return Results.File(file.Bytes, file.ContentType, file.FileName);
            });
        }).RequireAuthorization();

        // ── Sello editorial: lado del sello (el negocio activo es Publisher) ────────
        app.MapGet("/api/affiliates/{id:guid}/imprint/authors", async (HttpContext ctx, IImprintService svc, Guid id) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            return await ImprintGuard(async () => Results.Ok(await svc.GetAuthorsAsync(id)));
        }).RequireAuthorization();

        app.MapPost("/api/affiliates/{id:guid}/imprint/authors", async (HttpContext ctx, IImprintService svc, Guid id, InviteImprintAuthorRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await ImprintGuard(async () =>
            {
                var result = await svc.InviteAsync(id, request);
                return Results.Created($"/api/affiliates/{id}/imprint/authors/{result.Id}", result);
            });
        }).RequireAuthorization();

        app.MapPut("/api/affiliates/{id:guid}/imprint/authors/{contractId:guid}", async (HttpContext ctx, IImprintService svc, Guid id, Guid contractId, UpdateImprintContractRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await ImprintGuard(async () =>
            {
                var result = await svc.UpdateTermsAsync(id, contractId, request);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapPost("/api/affiliates/{id:guid}/imprint/authors/{contractId:guid}/end", async (HttpContext ctx, IImprintService svc, Guid id, Guid contractId) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await ImprintGuard(async () =>
            {
                var result = await svc.EndAsync(id, contractId);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapGet("/api/affiliates/{id:guid}/imprint/works", async (HttpContext ctx, IImprintService svc, Guid id) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            return await ImprintGuard(async () => Results.Ok(await svc.GetWorksAsync(id)));
        }).RequireAuthorization();

        // ── Sello editorial: lado del autor ─────────────────────────────────────────
        app.MapGet("/api/affiliates/{id:guid}/imprint-contracts", async (HttpContext ctx, IImprintService svc, Guid id) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            return Results.Ok(await svc.GetMyContractsAsync(id));
        }).RequireAuthorization();

        app.MapPost("/api/affiliates/{id:guid}/imprint-contracts/{contractId:guid}/respond", async (HttpContext ctx, IImprintService svc, Guid id, Guid contractId, RespondImprintContractRequest request) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            return await ImprintGuard(async () =>
            {
                var result = await svc.RespondAsync(id, contractId, request.Accept);
                return result is null ? NotFound() : Results.Ok(result);
            });
        }).RequireAuthorization();

        app.MapGet("/api/affiliates/{id:guid}/imprint-options", async (HttpContext ctx, IImprintService svc, Guid id) =>
        {
            if (!IsActiveAffiliate(ctx, id)) return Results.Forbid();
            return Results.Ok(await svc.GetOptionsAsync(id));
        }).RequireAuthorization();

        // Sitemap de la web: obras publicadas de autores publicados. Público y cacheable (solo slugs y fechas).
        app.MapGet("/api/public/sitemap/works", async (IWorkService svc, HttpResponse response) =>
        {
            response.Headers.CacheControl = "public, max-age=3600";
            return Results.Ok(await svc.GetSitemapAsync());
        }).AllowAnonymous();

        app.MapGet("/api/public/affiliates/{slug}/works", async (HttpContext ctx, IWorkService svc, IReaderClubService club, string slug) =>
        {
            var reader = await ReaderAsync(ctx, club, slug);
            var result = await svc.GetPublicAsync(slug, reader);
            if (result is null) return NotFound();
            Cache(ctx, reader);
            return Results.Ok(result);
        }).AllowAnonymous();

        // ?lang=en: títulos y texto traducidos donde haya traducción publicada; el resto, en español.
        app.MapGet("/api/public/affiliates/{slug}/works/{workSlug}", async (HttpContext ctx, IWorkService svc, IReaderClubService club, string slug, string workSlug, string? lang) =>
        {
            var reader = await ReaderAsync(ctx, club, slug);
            var result = await svc.GetPublicWorkAsync(slug, workSlug, reader, lang);
            if (result is null) return NotFound();
            Cache(ctx, reader);
            return Results.Ok(result);
        }).AllowAnonymous();

        app.MapGet("/api/public/affiliates/{slug}/works/{workSlug}/chapters/{number:int}", async (HttpContext ctx, IWorkService svc, IReaderClubService club, string slug, string workSlug, int number, string? lang) =>
        {
            var reader = await ReaderAsync(ctx, club, slug);
            var result = await svc.GetPublicChapterAsync(slug, workSlug, number, reader, lang);
            if (result is null) return NotFound();
            Cache(ctx, reader);
            return Results.Ok(result);
        }).AllowAnonymous();
    }

    internal static string? ReaderToken(HttpContext ctx)
        => ctx.Request.Headers.TryGetValue("X-Reader-Token", out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : null;

    private static async Task<ReaderContext?> ReaderAsync(HttpContext ctx, IReaderClubService club, string slug)
    {
        var token = ReaderToken(ctx);
        return token is null ? null : await club.ResolveAsync(slug, token);
    }

    private static void Cache(HttpContext ctx, ReaderContext? reader)
    {
        var personal = reader is not null || ReaderToken(ctx) is not null;
        ctx.Response.Headers.CacheControl = personal ? "private, no-store" : "public, max-age=60";
    }

    private static bool IsActiveAffiliate(HttpContext ctx, Guid id)
        => ctx.User.FindFirst("active_affiliate_id")?.Value == id.ToString();

    private static bool CanWrite(HttpContext ctx, Guid id)
        => IsActiveAffiliate(ctx, id) && ctx.User.FindFirst("role")?.Value != "Staff";

    private static IResult NotFound()
        => Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Not found" } });

    // Como Guard, y además: el negocio no es un sello → 403 con el motivo.
    private static async Task<IResult> ImprintGuard(Func<Task<IResult>> action)
    {
        try { return await Guard(action); }
        catch (InvalidOperationException ex) { return Results.Json(new { error = new { code = "NOT_AN_IMPRINT", message = ex.Message } }, statusCode: 403); }
    }

    private static async Task<IResult> Guard(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } }); }
        catch (WorkConflictException ex) { return Results.Conflict(new { error = new { code = "STALE_WORK", message = ex.Message, currentRevision = ex.CurrentRevision } }); }
    }
}
