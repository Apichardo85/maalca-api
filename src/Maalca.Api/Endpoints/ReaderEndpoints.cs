using Maalca.Application.Creator;

namespace Maalca.Api.Endpoints;

/// <summary>
/// Club de lectores del espacio de autor (ver Application/Creator/ReaderClub.cs). Público: suscribirse a las
/// cartas, confirmar, entrar por enlace, darse de baja, membresía y "seguir leyendo". Dashboard: ajustes,
/// lectores y cartas en /space/[slug]/readers. La sesión del lector viaja en X-Reader-Token.
/// </summary>
public static class ReaderEndpoints
{
    public static void AddReaderServices(IServiceCollection services)
    {
        services.AddSingleton<ReaderTokens>();
        services.AddScoped<IReaderMailer, ReaderMailer>();
        services.AddScoped<IReaderClubService, ReaderClubService>();
        services.AddHostedService<Maalca.Api.Workers.ReaderLetterWorker>();
    }

    public static void MapReaderEndpoints(WebApplication app)
    {
        const string pub = "/api/public/affiliates/{slug}/readers";

        app.MapGet(pub + "/club", async (IReaderClubService svc, string slug, HttpResponse response) =>
        {
            var result = await svc.GetPublicClubAsync(slug);
            if (result is null) return NotFound();
            response.Headers.CacheControl = "public, max-age=60";
            return Results.Ok(result);
        }).AllowAnonymous();

        app.MapPost(pub, async (IReaderClubService svc, string slug, ReaderSubscribeRequest request)
            => await Guard(async () => await svc.SubscribeAsync(slug, request) is null ? NotFound() : Results.Ok(new { status = "check_email" })))
            .AllowAnonymous();

        app.MapPost(pub + "/login", async (IReaderClubService svc, string slug, ReaderEmailRequest request)
            => await Guard(async () => await svc.RequestLoginAsync(slug, request) is null ? NotFound() : Results.Ok(new { status = "check_email" })))
            .AllowAnonymous();

        app.MapPost(pub + "/confirm", async (IReaderClubService svc, string slug, ReaderTokenRequest request) =>
        {
            if (!Guid.TryParse(request.Token, out var token)) return NotFound();
            var result = await svc.ConfirmAsync(slug, token);
            return result is null ? NotFound() : Results.Ok(result);
        }).AllowAnonymous();

        app.MapPost(pub + "/verify", async (IReaderClubService svc, string slug, ReaderTokenRequest request) =>
        {
            var result = await svc.VerifyLoginAsync(slug, request.Token);
            return result is null ? NotFound() : Results.Ok(result);
        }).AllowAnonymous();

        app.MapPost(pub + "/unsubscribe", async (IReaderClubService svc, string slug, ReaderTokenRequest request) =>
        {
            var result = await svc.UnsubscribeAsync(slug, request.Token);
            return result is null ? NotFound() : Results.Ok(result);
        }).AllowAnonymous();

        app.MapGet(pub + "/me", async (HttpContext ctx, IReaderClubService svc, string slug) =>
        {
            ctx.Response.Headers.CacheControl = "private, no-store";
            var result = await svc.GetMeAsync(slug, WorkEndpoints.ReaderToken(ctx));
            return result is null ? Results.Unauthorized() : Results.Ok(result);
        }).AllowAnonymous();

        app.MapPut(pub + "/progress", async (HttpContext ctx, IReaderClubService svc, string slug, SaveReaderProgressRequest request)
            => await svc.SaveProgressAsync(slug, WorkEndpoints.ReaderToken(ctx), request) ? Results.NoContent() : Results.Unauthorized())
            .AllowAnonymous();

        app.MapPost(pub + "/membership/checkout", async (HttpContext ctx, IReaderClubService svc, string slug, MembershipCheckoutRequest request)
            => await Guard(async () =>
            {
                var result = await svc.CreateMembershipCheckoutAsync(slug, WorkEndpoints.ReaderToken(ctx), request);
                return result is null ? NotFound() : Results.Ok(result);
            }))
            .AllowAnonymous();

        app.MapPost(pub + "/membership/sync", async (HttpContext ctx, IReaderClubService svc, string slug, MembershipSyncRequest request) =>
        {
            ctx.Response.Headers.CacheControl = "private, no-store";
            var result = await svc.SyncMembershipCheckoutAsync(slug, WorkEndpoints.ReaderToken(ctx), request);
            return result is null ? NotFound() : Results.Ok(result);
        }).AllowAnonymous();

        app.MapPost(pub + "/membership/portal", async (HttpContext ctx, IReaderClubService svc, string slug, ReaderPortalRequest request)
            => await Guard(async () =>
            {
                var result = await svc.CreatePortalAsync(slug, WorkEndpoints.ReaderToken(ctx), request);
                return result is null ? Results.Unauthorized() : Results.Ok(result);
            }))
            .AllowAnonymous();

        // ── Dashboard del autor ───────────────────────────────────────────────────
        const string dash = "/api/affiliates/{id:guid}/readers";

        app.MapGet(dash + "/settings", async (HttpContext ctx, IReaderClubService svc, Guid id)
            => IsActiveAffiliate(ctx, id) ? Results.Ok(await svc.GetSettingsAsync(id)) : Results.Forbid())
            .RequireAuthorization();

        app.MapPut(dash + "/settings", async (HttpContext ctx, IReaderClubService svc, Guid id, UpdateReaderClubSettingsRequest request)
            => CanWrite(ctx, id) ? await Guard(async () => Results.Ok(await svc.UpdateSettingsAsync(id, request))) : Results.Forbid())
            .RequireAuthorization();

        app.MapGet(dash, async (HttpContext ctx, IReaderClubService svc, Guid id, string? filter, string? q, int? skip, int? take)
            => IsActiveAffiliate(ctx, id) ? Results.Ok(await svc.ListReadersAsync(id, filter, q, skip ?? 0, take ?? 50)) : Results.Forbid())
            .RequireAuthorization();

        app.MapGet(dash + "/export", async (HttpContext ctx, IReaderClubService svc, Guid id) =>
        {
            if (!CanWrite(ctx, id)) return Results.Forbid();
            var csv = await svc.ExportCsvAsync(id);
            return Results.File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(),
                "text/csv; charset=utf-8", "lectores.csv");
        }).RequireAuthorization();

        app.MapDelete(dash + "/{readerId:guid}", async (HttpContext ctx, IReaderClubService svc, Guid id, Guid readerId)
            => CanWrite(ctx, id)
                ? await Guard(async () => await svc.DeleteReaderAsync(id, readerId) ? Results.NoContent() : NotFound())
                : Results.Forbid())
            .RequireAuthorization();

        app.MapGet(dash + "/letters", async (HttpContext ctx, IReaderClubService svc, Guid id)
            => IsActiveAffiliate(ctx, id) ? Results.Ok(await svc.ListLettersAsync(id)) : Results.Forbid())
            .RequireAuthorization();

        app.MapPost(dash + "/letters", async (HttpContext ctx, IReaderClubService svc, Guid id, UpsertReaderLetterRequest request)
            => CanWrite(ctx, id) ? await Guard(async () => Results.Ok(await svc.CreateLetterAsync(id, request))) : Results.Forbid())
            .RequireAuthorization();

        app.MapPut(dash + "/letters/{letterId:guid}", async (HttpContext ctx, IReaderClubService svc, Guid id, Guid letterId, UpsertReaderLetterRequest request)
            => CanWrite(ctx, id)
                ? await Guard(async () => await svc.UpdateLetterAsync(id, letterId, request) is { } r ? Results.Ok(r) : NotFound())
                : Results.Forbid())
            .RequireAuthorization();

        app.MapDelete(dash + "/letters/{letterId:guid}", async (HttpContext ctx, IReaderClubService svc, Guid id, Guid letterId)
            => CanWrite(ctx, id)
                ? await Guard(async () => await svc.DeleteLetterAsync(id, letterId) ? Results.NoContent() : NotFound())
                : Results.Forbid())
            .RequireAuthorization();

        app.MapPost(dash + "/letters/{letterId:guid}/send", async (HttpContext ctx, IReaderClubService svc, Guid id, Guid letterId)
            => CanWrite(ctx, id)
                ? await Guard(async () => await svc.QueueLetterAsync(id, letterId) is { } r ? Results.Ok(r) : NotFound())
                : Results.Forbid())
            .RequireAuthorization();

        app.MapPost(dash + "/letters/{letterId:guid}/test", async (HttpContext ctx, IReaderClubService svc, Guid id, Guid letterId, SendTestLetterRequest request)
            => CanWrite(ctx, id)
                ? await Guard(async () => await svc.SendTestLetterAsync(id, letterId, request) switch
                {
                    null => NotFound(),
                    true => Results.Ok(new { sent = true }),
                    false => Results.Json(new { error = new { code = "MAIL_FAILED", message = "No se pudo mandar la prueba." } }, statusCode: 502),
                })
                : Results.Forbid())
            .RequireAuthorization();
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
        catch (Stripe.StripeException ex)
        {
            return Results.Json(new { error = new { code = "PAYMENT_ERROR", message = ex.StripeError?.Message ?? "Stripe no respondió." } }, statusCode: 502);
        }
    }
}
