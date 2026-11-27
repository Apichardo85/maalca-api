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

        // ── Página pública del autor ──────────────────────────────────────────────
        // Borradores y capítulos cerrados nunca salen. Caché corta: la ventana gratis abre y cierra sola.

        // Con X-Reader-Token (sesión del club) la respuesta depende del lector: nunca se cachea en público.

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

        app.MapGet("/api/public/affiliates/{slug}/works/{workSlug}", async (HttpContext ctx, IWorkService svc, IReaderClubService club, string slug, string workSlug) =>
        {
            var reader = await ReaderAsync(ctx, club, slug);
            var result = await svc.GetPublicWorkAsync(slug, workSlug, reader);
            if (result is null) return NotFound();
            Cache(ctx, reader);
            return Results.Ok(result);
        }).AllowAnonymous();

        app.MapGet("/api/public/affiliates/{slug}/works/{workSlug}/chapters/{number:int}", async (HttpContext ctx, IWorkService svc, IReaderClubService club, string slug, string workSlug, int number) =>
        {
            var reader = await ReaderAsync(ctx, club, slug);
            var result = await svc.GetPublicChapterAsync(slug, workSlug, number, reader);
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

    private static async Task<IResult> Guard(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } }); }
    }
}
