using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Application.Services;
using Maalca.Api.Middleware;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Auth;
using Maalca.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "maalca-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "maalca-web";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAffiliateService, AffiliateService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IGiftCardService, GiftCardService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<IAffiliateMapService, AffiliateMapService>();
builder.Services.AddScoped<IPlatformAdminService, PlatformAdminService>();
builder.Services.AddScoped<IPublicCatalogService, PublicCatalogService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IPlanLimitService, PlanLimitService>();
builder.Services.AddScoped<ICatalogCrudService, CatalogCrudService>();
builder.Services.AddScoped<IMilestoneService, MilestoneService>();
builder.Services.AddScoped<ICanalService, CanalService>();
builder.Services.AddScoped<IInteractionEventService, InteractionEventService>();
builder.Services.AddScoped<IStripeBillingService, StripeBillingService>();
builder.Services.AddScoped<IStripeConnectService, StripeConnectService>();
builder.Services.AddScoped<IOrderNotificationService, OrderNotificationService>();
builder.Services.AddScoped<Maalca.Application.Common.Interfaces.IOrderRealtimeNotifier, Maalca.Api.Hubs.SignalROrderRealtimeNotifier>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IScreenAdService, ScreenAdService>();
builder.Services.AddScoped<IScreenService, ScreenService>();
builder.Services.AddScoped<IPublicBookingService, PublicBookingService>();

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

// Red de seguridad global contra ciclos de referencia en respuestas JSON. Bug real encontrado:
// GET /api/affiliates/{id}/appointments hace .Include(a => a.Service), y EF Core's change
// tracker auto-completa la navegación inversa Service.Appointments con ese mismo appointment
// (fix-up automático) aunque nunca se pidió explícitamente — eso crea Appointment→Service→
// Appointments[mismo Appointment]→Service→... y el serializador tira una excepción a mitad de
// stream, dejando al cliente con un JSON cortado ("Expected ',' o '}'..."). Pasó en producción
// con el primer afiliado (Pegote) que ya tenía citas reales creadas. IgnoreCycles corta la
// propiedad que cerraría el ciclo en vez de tirar excepción — no afecta ninguna respuesta que
// no tuviera el problema.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddSingleton<SupabaseJwksCache>();
builder.Services.AddSingleton<SupabaseTokenVerifier>();

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("MaalCaWeb", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:3001",
                "https://maalca.com",
                "https://www.maalca.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("X-Onboarding-Required");
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseCors("MaalCaWeb");
app.UseMiddleware<SupabaseAuthMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Maalca.Api.Hubs.QueueHub>("/hubs/queue");
app.MapHub<Maalca.Api.Hubs.OrdersHub>("/hubs/orders");

// ============ AUTH ENDPOINTS ============
app.MapPost("/api/auth/login", async (IAuthService authService, LoginRequest request) =>
{
    var result = await authService.LoginAsync(request);
    if (result == null)
        return Results.Unauthorized();
    return Results.Ok(result);
});

app.MapPost("/api/auth/refresh", async (IAuthService authService, RefreshTokenRequest request) =>
{
    var result = await authService.RefreshTokenAsync(request);
    if (result == null)
        return Results.Unauthorized();
    return Results.Ok(result);
});

// ============ AFFILIATE ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}", async (IAffiliateService affiliateService, Guid affiliateId) =>
{
    var result = await affiliateService.GetAffiliateAsync(affiliateId);
    if (result == null)
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });
    return Results.Ok(result);
});

app.MapGet("/api/me/affiliates", async (HttpContext ctx, IAffiliateMapService mapService, AppDbContext db) =>
{
    var sub = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(sub))
        return Results.Unauthorized();

    // ClaimPendingInvitesAsync ya corrió en SupabaseAuthMiddleware para este mismo request
    // (tiene que pasar ahí, antes de resolver los claims — ver comentario en ese archivo).
    var maps = await mapService.GetMapsForUserAsync(sub);
    if (maps.Count == 0)
        return Results.Ok(Array.Empty<AffiliateSummaryDto>());

    // IsImpersonation=false — el negocio que un admin de plataforma está soportando temporalmente
    // no debe aparecer en su propio selector de negocios (entra por /space/{slug} directo desde /ops).
    var ownMaps = maps.Where(m => !m.IsImpersonation).ToList();

    var affiliateIds = ownMaps.Select(m => m.AffiliateId).ToList();
    var affiliates = await db.Affiliates
        .Where(a => affiliateIds.Contains(a.Id))
        .ToDictionaryAsync(a => a.Id);

    var result = ownMaps
        .Where(m => affiliates.ContainsKey(m.AffiliateId))
        .Select(m => new AffiliateSummaryDto(
            m.AffiliateId,
            affiliates[m.AffiliateId].Name,
            affiliates[m.AffiliateId].Slug,
            affiliates[m.AffiliateId].BusinessType.ToString(),
            affiliates[m.AffiliateId].Plan.ToString(),
            m.Role.ToString()
        ));

    return Results.Ok(result);
});

app.MapGet("/api/me/admin-status", (HttpContext ctx) =>
{
    var isAdmin = ctx.User.FindFirst("platform_admin")?.Value == "true";
    var role = ctx.User.FindFirst("platform_role")?.Value;
    return Results.Ok(new MyAdminStatusDto(isAdmin, string.IsNullOrEmpty(role) ? null : role));
});

// ============ OPS ENDPOINTS (Fase 60 — panel de operaciones para admins de plataforma) ============
// Todos requieren el claim platform_admin=="true", resuelto en SupabaseAuthMiddleware. No están
// atados a ningún afiliado — son de la plataforma entera, por eso viven fuera de /api/affiliates.

app.MapGet("/api/ops/overview", async (HttpContext ctx, IPlatformAdminService opsService) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    return Results.Ok(await opsService.GetOverviewAsync());
});

app.MapGet("/api/ops/affiliates", async (HttpContext ctx, IPlatformAdminService opsService) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    return Results.Ok(await opsService.GetAffiliatesAsync());
});

app.MapPatch("/api/ops/affiliates/{affiliateId:guid}", async (
    HttpContext ctx, IPlatformAdminService opsService, Guid affiliateId, SetAffiliateStatusRequest request) =>
{
    // Publicar/pausar un negocio es una acción destructiva de plataforma — solo Owner, no Support
    // (ver comentario de gating en PlatformAdmin.cs).
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    if (ctx.User.FindFirst("platform_role")?.Value != nameof(PlatformAdminRole.Owner))
        return Results.Forbid();

    try
    {
        var result = await opsService.SetAffiliateStatusAsync(affiliateId, request.Published, request.Active);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
});

app.MapPost("/api/ops/impersonate/{affiliateId:guid}", async (HttpContext ctx, IPlatformAdminService opsService, Guid affiliateId) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    var sub = ctx.User.FindFirst("sub")?.Value;
    var email = ctx.User.FindFirst("email")?.Value ?? "";
    if (string.IsNullOrEmpty(sub))
        return Results.Unauthorized();

    try
    {
        var session = await opsService.StartImpersonationAsync(sub, email, affiliateId);
        return Results.Ok(session);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
});

app.MapDelete("/api/ops/impersonate", async (HttpContext ctx, IPlatformAdminService opsService) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    var sub = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(sub))
        return Results.Unauthorized();

    await opsService.EndImpersonationAsync(sub);
    return Results.NoContent();
});

// ---- Equipo interno de plataforma (Fase 82/83) — separado del equipo por-afiliado. Solo Owner
// puede invitar/cambiar rol/quitar; Support solo puede ver quién es parte del equipo. ----

app.MapGet("/api/ops/team", async (HttpContext ctx, IPlatformAdminService opsService) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    return Results.Ok(await opsService.GetPlatformTeamAsync());
});

app.MapPost("/api/ops/team", async (HttpContext ctx, IPlatformAdminService opsService, InvitePlatformAdminRequest request) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    if (ctx.User.FindFirst("platform_role")?.Value != nameof(PlatformAdminRole.Owner))
        return Results.Forbid();
    if (!Enum.TryParse<PlatformAdminRole>(request.Role, out var role))
        return Results.BadRequest(new { error = new { code = "INVALID_ROLE", message = "Rol inválido." } });

    try
    {
        var member = await opsService.InvitePlatformAdminAsync(request.Email, role);
        return Results.Ok(member);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
    }
});

app.MapPatch("/api/ops/team/{platformAdminId:guid}", async (
    HttpContext ctx, IPlatformAdminService opsService, Guid platformAdminId, UpdatePlatformAdminRoleRequest request) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    if (ctx.User.FindFirst("platform_role")?.Value != nameof(PlatformAdminRole.Owner))
        return Results.Forbid();
    if (!Enum.TryParse<PlatformAdminRole>(request.Role, out var role))
        return Results.BadRequest(new { error = new { code = "INVALID_ROLE", message = "Rol inválido." } });

    try
    {
        var member = await opsService.UpdatePlatformAdminRoleAsync(platformAdminId, role);
        return Results.Ok(member);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
    }
});

app.MapDelete("/api/ops/team/{platformAdminId:guid}", async (HttpContext ctx, IPlatformAdminService opsService, Guid platformAdminId) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    if (ctx.User.FindFirst("platform_role")?.Value != nameof(PlatformAdminRole.Owner))
        return Results.Forbid();

    try
    {
        await opsService.RemovePlatformAdminAsync(platformAdminId);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
    }
});

// ---- Notas CRM internas por afiliado (Fase 84) — visibles solo en /ops, cualquier admin
// (Owner o Support) puede leer y escribir; no son una acción destructiva. ----

app.MapGet("/api/ops/affiliates/{affiliateId:guid}/notes", async (HttpContext ctx, IPlatformAdminService opsService, Guid affiliateId) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    return Results.Ok(await opsService.GetAffiliateNotesAsync(affiliateId));
});

app.MapPost("/api/ops/affiliates/{affiliateId:guid}/notes", async (
    HttpContext ctx, IPlatformAdminService opsService, Guid affiliateId, CreateAffiliateNoteRequest request) =>
{
    if (ctx.User.FindFirst("platform_admin")?.Value != "true")
        return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { error = new { code = "EMPTY_TEXT", message = "La nota no puede estar vacía." } });

    var email = ctx.User.FindFirst("email")?.Value ?? "";
    var note = await opsService.AddAffiliateNoteAsync(affiliateId, email, request.Text);
    return Results.Ok(note);
});

// ============ COLLABORATOR ENDPOINTS (Fase 8 — dashboard multiusuario con roles) ============
// Solo Owner puede administrar el equipo. Exclusivo de plan Emprendedor — un negocio Gratis no
// puede sumar usuarios adicionales (mismo criterio que el resto de features de ese plan).
// Nota: se llama "collaborators" (no "team") porque "/team" ya existe más abajo para el
// concepto de staff/empleados del negocio (ITeamService) — son cosas distintas.

app.MapGet("/api/affiliates/{id:guid}/collaborators", async (
    HttpContext ctx, IAffiliateMapService mapService, Guid id) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value != "Owner")
        return Results.Forbid();

    var team = await mapService.GetTeamAsync(id);
    var result = team.Select(m => new TeamMemberDto(
        m.Id, m.Email, m.Role.ToString(), string.IsNullOrEmpty(m.SupabaseUserId), m.CreatedAt, m.TeamMemberId));
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{id:guid}/collaborators", async (
    HttpContext ctx, IAffiliateMapService mapService, AppDbContext db, Guid id, InviteTeamMemberRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value != "Owner")
        return Results.Forbid();

    var affiliate = await db.Affiliates.FindAsync(id);
    if (affiliate is null) return Results.NotFound();
    if (affiliate.Plan != Plan.Entrepreneur)
        return Results.BadRequest(new { error = new { code = "PLAN_REQUIRED", message = "Invitar usuarios es parte del plan Emprendedor." } });

    if (string.IsNullOrWhiteSpace(request.Email) || !Enum.TryParse<AffiliateRole>(request.Role, ignoreCase: true, out var role))
        return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "Correo o rol inválido." } });

    try
    {
        var map = await mapService.InviteAsync(id, request.Email, role, request.TeamMemberId);
        return Results.Created($"/api/affiliates/{id}/collaborators/{map.Id}",
            new TeamMemberDto(map.Id, map.Email, map.Role.ToString(), Pending: true, map.CreatedAt, map.TeamMemberId));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = new { code = "ALREADY_INVITED", message = ex.Message } });
    }
});

app.MapPatch("/api/affiliates/{id:guid}/collaborators/{mapId:guid}", async (
    HttpContext ctx, IAffiliateMapService mapService, Guid id, Guid mapId, UpdateTeamMemberRoleRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value != "Owner")
        return Results.Forbid();

    if (!Enum.TryParse<AffiliateRole>(request.Role, ignoreCase: true, out var role))
        return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "Rol inválido." } });

    try
    {
        var map = await mapService.UpdateRoleAsync(id, mapId, role);
        if (map is null) return Results.NotFound();
        return Results.Ok(new TeamMemberDto(map.Id, map.Email, map.Role.ToString(), string.IsNullOrEmpty(map.SupabaseUserId), map.CreatedAt, map.TeamMemberId));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = new { code = "LAST_OWNER", message = ex.Message } });
    }
});

app.MapDelete("/api/affiliates/{id:guid}/collaborators/{mapId:guid}", async (
    HttpContext ctx, IAffiliateMapService mapService, Guid id, Guid mapId) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value != "Owner")
        return Results.Forbid();

    try
    {
        var removed = await mapService.RemoveAsync(id, mapId);
        return removed ? Results.NoContent() : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = new { code = "LAST_OWNER", message = ex.Message } });
    }
});

// ============ CUSTOMER ENDPOINTS (mismo gating que /team — legacy sin chequeo de
// pertenencia; lectura requiere ser el afiliado activo, escritura además excluye Staff) ============
app.MapGet("/api/affiliates/{affiliateId:guid}/customers", async (HttpContext ctx, ICustomerService customerService, Guid affiliateId, int page = 1, int limit = 20, string? search = null, string? status = null) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    var result = await customerService.GetCustomersAsync(affiliateId, page, limit, search, status);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/customers/{id:guid}", async (HttpContext ctx, ICustomerService customerService, Guid affiliateId, Guid id) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    var result = await customerService.GetCustomerAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/customers", async (HttpContext ctx, ICustomerService customerService, Guid affiliateId, Customer customer) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await customerService.CreateCustomerAsync(affiliateId, customer);
    return Results.Created($"/api/affiliates/{affiliateId}/customers/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/customers/{id:guid}", async (HttpContext ctx, ICustomerService customerService, Guid affiliateId, Guid id, Customer customer) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await customerService.UpdateCustomerAsync(affiliateId, id, customer);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/customers/{id:guid}", async (HttpContext ctx, ICustomerService customerService, Guid affiliateId, Guid id) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await customerService.DeleteCustomerAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

// ============ APPOINTMENT ENDPOINTS (dashboard — agenda manual del negocio; el flujo de
// reserva público, cuando exista, va a ser un endpoint /api/public/... aparte, no este) ============
app.MapGet("/api/affiliates/{affiliateId:guid}/appointments", async (HttpContext ctx, IAppointmentService appointmentService, Guid affiliateId, DateTime? date = null, string? status = null, int page = 1) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    var result = await appointmentService.GetAppointmentsAsync(affiliateId, date, status, page);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (HttpContext ctx, IAppointmentService appointmentService, Guid affiliateId, Guid id) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    var result = await appointmentService.GetAppointmentAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/appointments", async (HttpContext ctx, IAppointmentService appointmentService, Guid affiliateId, Appointment appointment) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await appointmentService.CreateAppointmentAsync(affiliateId, appointment);
    return Results.Created($"/api/affiliates/{affiliateId}/appointments/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (HttpContext ctx, IAppointmentService appointmentService, Guid affiliateId, Guid id, Appointment appointment) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await appointmentService.UpdateAppointmentAsync(affiliateId, id, appointment);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPatch("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (HttpContext ctx, IAppointmentService appointmentService, Guid affiliateId, Guid id, string status) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    // Staff sí puede mover el estado (Confirmada/Completada/No-show) — es su trabajo del día a
    // día — pero no crear/editar/borrar citas completas.
    var result = await appointmentService.UpdateAppointmentStatusAsync(affiliateId, id, status);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (HttpContext ctx, IAppointmentService appointmentService, Guid affiliateId, Guid id) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await appointmentService.DeleteAppointmentAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

// ============ SERVICE ENDPOINTS ============
// Mismo hueco que team/customers/appointments tenían: cualquier usuario autenticado podía
// leer/editar el catálogo de servicios de OTRO afiliado con solo cambiar el guid en la URL.
// Confirmado que ningún caller público (plantillas sin login) pega directo a estos endpoints —
// solo el dashboard (space/[slug]/agenda, /api/space/[slug]/services) que ya manda el token.
app.MapGet("/api/affiliates/{affiliateId:guid}/services", async (IServiceService serviceService, Guid affiliateId, HttpContext ctx, string? category = null, string? status = null) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    var result = await serviceService.GetServicesAsync(affiliateId, category, status);
    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/api/affiliates/{affiliateId:guid}/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id, HttpContext ctx) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    var result = await serviceService.GetServiceAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/affiliates/{affiliateId:guid}/services", async (IServiceService serviceService, Guid affiliateId, Maalca.Domain.Entities.Service service, HttpContext ctx) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await serviceService.CreateServiceAsync(affiliateId, service);
    return Results.Created($"/api/affiliates/{affiliateId}/services/{result.Id}", result);
}).RequireAuthorization();

app.MapPut("/api/affiliates/{affiliateId:guid}/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id, Maalca.Domain.Entities.Service service, HttpContext ctx) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await serviceService.UpdateServiceAsync(affiliateId, id, service);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
}).RequireAuthorization();

app.MapDelete("/api/affiliates/{affiliateId:guid}/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id, HttpContext ctx) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await serviceService.DeleteServiceAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
}).RequireAuthorization();

// ============ INVENTORY ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/inventory", async (IInventoryService inventoryService, Guid affiliateId, string? category = null, string? status = null, int page = 1) =>
{
    var result = await inventoryService.GetInventoryAsync(affiliateId, category, status, page);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/inventory/{id:guid}", async (IInventoryService inventoryService, Guid affiliateId, Guid id) =>
{
    var result = await inventoryService.GetInventoryItemAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/inventory", async (IInventoryService inventoryService, Guid affiliateId, InventoryItem item) =>
{
    var result = await inventoryService.CreateInventoryItemAsync(affiliateId, item);
    return Results.Created($"/api/affiliates/{affiliateId}/inventory/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/inventory/{id:guid}", async (IInventoryService inventoryService, Guid affiliateId, Guid id, InventoryItem item) =>
{
    var result = await inventoryService.UpdateInventoryItemAsync(affiliateId, id, item);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/inventory/{id:guid}", async (IInventoryService inventoryService, Guid affiliateId, Guid id) =>
{
    var result = await inventoryService.DeleteInventoryItemAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

app.MapPost("/api/affiliates/{affiliateId:guid}/inventory/movements", async (IInventoryService inventoryService, Guid affiliateId, InventoryMovement movement) =>
{
    try
    {
        var result = await inventoryService.CreateMovementAsync(affiliateId, movement);
        return Results.Created($"/api/affiliates/{affiliateId}/inventory/{movement.InventoryItemId}", result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
    }
});

// ============ QUEUE ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/queue", async (IQueueService queueService, Guid affiliateId) =>
{
    var result = await queueService.GetQueueAsync(affiliateId);
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/queue", async (IQueueService queueService, Guid affiliateId, QueueEntry entry) =>
{
    var result = await queueService.AddToQueueAsync(affiliateId, entry);
    return Results.Created($"/api/affiliates/{affiliateId}/queue/{result.Id}", result);
});

app.MapPatch("/api/affiliates/{affiliateId:guid}/queue/{id:guid}", async (IQueueService queueService, Guid affiliateId, Guid id, string status, Guid? barberId = null) =>
{
    var result = await queueService.UpdateQueueEntryAsync(affiliateId, id, status, barberId);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

// ============ TEAM ENDPOINTS (personal operativo del negocio — meseros, barberos, etc.;
// distinto de /api/affiliates/{id}/collaborators, que es quién puede iniciar sesión en el
// dashboard) ============
// Estos endpoints venían del dashboard legacy sin ningún chequeo de pertenencia — cualquier
// usuario autenticado podía leer/editar el staff de OTRO afiliado con solo cambiar el guid en
// la URL. Se agrega el mismo gating que el resto de /api/affiliates/{id}/... : lectura requiere
// que sea el afiliado activo del usuario; escritura además exige que no sea rol Staff.
app.MapGet("/api/affiliates/{affiliateId:guid}/team", async (HttpContext ctx, ITeamService teamService, Guid affiliateId, string? department = null, string? status = null) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    var result = await teamService.GetTeamAsync(affiliateId, department, status);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/team/{id:guid}", async (HttpContext ctx, ITeamService teamService, Guid affiliateId, Guid id) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    var result = await teamService.GetTeamMemberAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/team", async (HttpContext ctx, ITeamService teamService, Guid affiliateId, TeamMember member) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await teamService.CreateTeamMemberAsync(affiliateId, member);
    return Results.Created($"/api/affiliates/{affiliateId}/team/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/team/{id:guid}", async (HttpContext ctx, ITeamService teamService, Guid affiliateId, Guid id, TeamMember member) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await teamService.UpdateTeamMemberAsync(affiliateId, id, member);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/team/{id:guid}", async (HttpContext ctx, ITeamService teamService, Guid affiliateId, Guid id) =>
{
    if (ctx.User.FindFirst("active_affiliate_id")?.Value != affiliateId.ToString())
        return Results.Forbid();
    if (ctx.User.FindFirst("role")?.Value == "Staff")
        return Results.Forbid();
    var result = await teamService.DeleteTeamMemberAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

// ============ PRODUCT ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/products", async (IProductService productService, Guid affiliateId, string? category = null, string? status = null) =>
{
    var result = await productService.GetProductsAsync(affiliateId, category, status);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/products/{id:guid}", async (IProductService productService, Guid affiliateId, Guid id) =>
{
    var result = await productService.GetProductAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/products", async (IProductService productService, Guid affiliateId, Product product) =>
{
    var result = await productService.CreateProductAsync(affiliateId, product);
    return Results.Created($"/api/affiliates/{affiliateId}/products/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/products/{id:guid}", async (IProductService productService, Guid affiliateId, Guid id, Product product) =>
{
    var result = await productService.UpdateProductAsync(affiliateId, id, product);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/products/{id:guid}", async (IProductService productService, Guid affiliateId, Guid id) =>
{
    var result = await productService.DeleteProductAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

// ============ INVOICE ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/invoices", async (IInvoiceService invoiceService, Guid affiliateId, string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null) =>
{
    var result = await invoiceService.GetInvoicesAsync(affiliateId, status, dateFrom, dateTo);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/invoices/{id:guid}", async (IInvoiceService invoiceService, Guid affiliateId, Guid id) =>
{
    var result = await invoiceService.GetInvoiceAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/invoices", async (IInvoiceService invoiceService, Guid affiliateId, Invoice invoice) =>
{
    var result = await invoiceService.CreateInvoiceAsync(affiliateId, invoice);
    return Results.Created($"/api/affiliates/{affiliateId}/invoices/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/invoices/{id:guid}", async (IInvoiceService invoiceService, Guid affiliateId, Guid id, Invoice invoice) =>
{
    var result = await invoiceService.UpdateInvoiceAsync(affiliateId, id, invoice);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/invoices/{id:guid}", async (IInvoiceService invoiceService, Guid affiliateId, Guid id) =>
{
    var result = await invoiceService.DeleteInvoiceAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

// ============ GIFT CARD ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/giftcards", async (IGiftCardService giftCardService, Guid affiliateId, string? status = null) =>
{
    var result = await giftCardService.GetGiftCardsAsync(affiliateId, status);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/giftcards/{id:guid}", async (IGiftCardService giftCardService, Guid affiliateId, Guid id) =>
{
    var result = await giftCardService.GetGiftCardAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/giftcards", async (IGiftCardService giftCardService, Guid affiliateId, GiftCard giftCard) =>
{
    var result = await giftCardService.CreateGiftCardAsync(affiliateId, giftCard);
    return Results.Created($"/api/affiliates/{affiliateId}/giftcards/{result.Id}", result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/giftcards/{id:guid}/redeem", async (IGiftCardService giftCardService, Guid affiliateId, Guid id, RedeemGiftCardRequest request) =>
{
    var result = await giftCardService.RedeemGiftCardAsync(affiliateId, id, request.Amount);
    if (result == null)
        return Results.BadRequest(new { error = new { code = "REDEEM_FAILED", message = "Gift card not found, inactive, or insufficient balance" } });
    return Results.Ok(result);
});

// ============ CAMPAIGN ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/campaigns", async (ICampaignService campaignService, Guid affiliateId, string? status = null) =>
{
    var result = await campaignService.GetCampaignsAsync(affiliateId, status);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/campaigns/{id:guid}", async (ICampaignService campaignService, Guid affiliateId, Guid id) =>
{
    var result = await campaignService.GetCampaignAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/campaigns", async (ICampaignService campaignService, Guid affiliateId, Campaign campaign) =>
{
    var result = await campaignService.CreateCampaignAsync(affiliateId, campaign);
    return Results.Created($"/api/affiliates/{affiliateId}/campaigns/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/campaigns/{id:guid}", async (ICampaignService campaignService, Guid affiliateId, Guid id, Campaign campaign) =>
{
    var result = await campaignService.UpdateCampaignAsync(affiliateId, id, campaign);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/campaigns/{id:guid}", async (ICampaignService campaignService, Guid affiliateId, Guid id) =>
{
    var result = await campaignService.DeleteCampaignAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

// ============ METRICS ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/metrics", async (IMetricsService metricsService, Guid affiliateId) =>
{
    var result = await metricsService.GetMetricsAsync(affiliateId);
    return Results.Ok(result);
});

app.MapGet("/api/metrics/overview", async (ILeadService leadService) =>
{
    var result = await leadService.GetOverviewMetricsAsync();
    return Results.Ok(result);
});

// ============ LEAD ENDPOINTS ============
app.MapPost("/api/leads/properties", async (ILeadService leadService, Lead lead) =>
{
    var result = await leadService.CreatePropertyLeadAsync(lead);
    return Results.Created($"/api/leads/properties/{result.Id}", result);
});

app.MapPost("/api/leads/cirisonic", async (ILeadService leadService, Lead lead) =>
{
    var result = await leadService.CreateCirisonicLeadAsync(lead);
    return Results.Created($"/api/leads/cirisonic/{result.Id}", result);
});

// ============ ONBOARDING ============
app.MapPost("/api/onboarding", async (HttpContext ctx, IOnboardingService onboardingService, OnboardingRequest request) =>
{
    var sub = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(sub))
        return Results.Unauthorized();

    var email = ctx.User.FindFirst("email")?.Value ?? string.Empty;

    try
    {
        var result = await onboardingService.OnboardAsync(sub, email, request);
        return Results.Created($"/api/public/affiliates/{result.Slug}", result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = new { code = "ALREADY_ONBOARDED", message = ex.Message } });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

// ============ AFFILIATE PROFILE UPDATE ============
app.MapMethods("/api/affiliates/{id}/profile", new[] { "PATCH" },
    async (HttpContext ctx, IAffiliateService affiliateService,
           Guid id, UpdateAffiliateProfileRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var role = ctx.User.FindFirst("role")?.Value;
    if (role == "Staff")
        return Results.Forbid();

    try
    {
        var profile = await affiliateService.UpdateProfileAsync(id, request);
        if (profile is null)
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });

        return Results.Ok(profile);
    }
    catch (InvalidOperationException ex) when (ex.Message == PlanLimitService.TrialExpiredMessage)
    {
        return Results.Json(new { error = new { code = "TRIAL_EXPIRED", message = ex.Message } },
            statusCode: 402);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

// ============ AFFILIATE CONTENT UPDATE (ProcessSteps/Faq/Horario) ============
app.MapMethods("/api/affiliates/{id}/content", new[] { "PATCH" },
    async (HttpContext ctx, IAffiliateService affiliateService,
           Guid id, UpdateAffiliateContentRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var role = ctx.User.FindFirst("role")?.Value;
    if (role == "Staff")
        return Results.Forbid();

    try
    {
        var content = await affiliateService.UpdateContentAsync(id, request);
        if (content is null)
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });

        return Results.Ok(content);
    }
    catch (InvalidOperationException ex) when (ex.Message == PlanLimitService.TrialExpiredMessage)
    {
        return Results.Json(new { error = new { code = "TRIAL_EXPIRED", message = ex.Message } },
            statusCode: 402);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

// ============ BILLING (Stripe checkout) ============
app.MapPost("/api/affiliates/{id}/billing/checkout-session", async (
    HttpContext ctx, IStripeBillingService billingService,
    Guid id, CreateCheckoutSessionRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var role = ctx.User.FindFirst("role")?.Value;
    if (role == "Staff")
        return Results.Forbid();

    try
    {
        var session = await billingService.CreateCheckoutSessionAsync(id, request);
        return Results.Ok(session);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
});

app.MapPost("/api/affiliates/{id}/billing/portal-session", async (
    HttpContext ctx, IStripeBillingService billingService,
    Guid id, CreatePortalSessionRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var role = ctx.User.FindFirst("role")?.Value;
    if (role == "Staff")
        return Results.Forbid();

    try
    {
        var session = await billingService.CreatePortalSessionAsync(id, request.ReturnUrl);
        return Results.Ok(session);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
});

// ============ STRIPE CONNECT (afiliado recibe pagos de SUS clientes) ============
// Distinto de /billing arriba: eso es la suscripción MaalCa→afiliado. Esto es la cuenta
// donde el afiliado cobra a sus propios clientes (tarjeta/Apple Pay/Google Pay).
app.MapPost("/api/affiliates/{id}/connect/onboarding-link", async (
    HttpContext ctx, IStripeConnectService connectService,
    Guid id, CreateConnectOnboardingLinkRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var role = ctx.User.FindFirst("role")?.Value;
    if (role == "Staff")
        return Results.Forbid();

    try
    {
        var link = await connectService.CreateOnboardingLinkAsync(id, request);
        return Results.Ok(link);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = new { code = "MISSING_COUNTRY", message = ex.Message } });
    }
    catch (Stripe.StripeException ex)
    {
        return Results.BadRequest(new { error = new { code = "STRIPE_ERROR", message = ex.Message } });
    }
});

app.MapGet("/api/affiliates/{id}/connect/status", async (
    HttpContext ctx, IStripeConnectService connectService, Guid id) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        var status = await connectService.GetStatusAsync(id);
        return Results.Ok(status);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
});

// ============ ORDERS (panel admin del afiliado) ============
app.MapGet("/api/affiliates/{id}/orders", async (
    HttpContext ctx, IOrderService orderService, Guid id) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var orders = await orderService.GetOrdersAsync(id);
    return Results.Ok(orders);
});

app.MapPatch("/api/affiliates/{id}/orders/{orderId}/status", async (
    HttpContext ctx, IOrderService orderService, Guid id, Guid orderId, UpdateOrderStatusRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var role = ctx.User.FindFirst("role")?.Value;
    if (role == "Staff")
        return Results.Forbid();

    try
    {
        var result = await orderService.UpdateStatusAsync(id, orderId, request.Status);
        if (result is null)
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Order not found" } });
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

// ============ SCREEN ADS (comerciales del Menu Board — Fase 9 Etapa A) ============
app.MapGet("/api/affiliates/{id}/screen-ads", async (
    HttpContext ctx, IScreenAdService screenAdService, Guid id) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var ads = await screenAdService.GetAllAsync(id);
    return Results.Ok(ads);
});

app.MapPost("/api/affiliates/{id}/screen-ads", async (
    HttpContext ctx, IScreenAdService screenAdService, Guid id, CreateScreenAdRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        var ad = await screenAdService.CreateAsync(id, request);
        return Results.Created($"/api/affiliates/{id}/screen-ads/{ad.Id}", ad);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

app.MapPatch("/api/affiliates/{id}/screen-ads/{adId}", async (
    HttpContext ctx, IScreenAdService screenAdService, Guid id, Guid adId, UpdateScreenAdRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        var result = await screenAdService.UpdateAsync(id, adId, request);
        if (result is null)
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Ad not found" } });
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

app.MapDelete("/api/affiliates/{id}/screen-ads/{adId}", async (
    HttpContext ctx, IScreenAdService screenAdService, Guid id, Guid adId) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var deleted = await screenAdService.DeleteAsync(id, adId);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// ============ SCREENS (pantallas adicionales del Menu Board — Fase 9 Etapa B) ============
app.MapGet("/api/affiliates/{id}/screens", async (
    HttpContext ctx, IScreenService screenService, Guid id) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var screens = await screenService.GetAllAsync(id);
    return Results.Ok(screens);
});

app.MapPost("/api/affiliates/{id}/screens", async (
    HttpContext ctx, IScreenService screenService, Guid id, CreateScreenRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        var screen = await screenService.CreateAsync(id, request);
        return Results.Created($"/api/affiliates/{id}/screens/{screen.Id}", screen);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

app.MapPatch("/api/affiliates/{id}/screens/{screenId}", async (
    HttpContext ctx, IScreenService screenService, Guid id, Guid screenId, UpdateScreenRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        var result = await screenService.UpdateAsync(id, screenId, request);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Screen not found" } });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

app.MapDelete("/api/affiliates/{id}/screens/{screenId}", async (
    HttpContext ctx, IScreenService screenService, Guid id, Guid screenId) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        await screenService.DeleteAsync(id, screenId);
        return Results.NoContent();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Screen not found" } });
    }
});

// ============ AFFILIATE EVENTS ============
app.MapPost("/api/affiliates/{id}/events", async (
    HttpContext ctx, ILogger<Program> logger, IMilestoneService milestones,
    Guid id, AffiliateEventRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var allowed = new HashSet<string> { "link_shared" };
    if (!allowed.Contains(request.Type))
        return Results.BadRequest(new { error = new { code = "INVALID_EVENT_TYPE", message = "Unsupported event type." } });

    logger.LogInformation("AffiliateEvent affiliate={AffiliateId} type={Type} metadata={@Metadata}",
        id, request.Type, request.Metadata);

    if (request.Type == MilestoneKeys.LinkShared)
        await milestones.MarkAsync(id, MilestoneKeys.LinkShared);

    return Results.NoContent();
});

// ============ CATALOG CRUD (dashboard) ============
app.MapGet("/api/affiliates/{id}/catalog-items", async (HttpContext ctx, ICatalogCrudService catalogCrud, Guid id) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var items = await catalogCrud.GetItemsAsync(id);
    return Results.Ok(items);
});

app.MapGet("/api/affiliates/{id}/catalog-items/{itemId}", async (HttpContext ctx, ICatalogCrudService catalogCrud, Guid id, Guid itemId) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var item = await catalogCrud.GetItemAsync(id, itemId);
    return item == null ? Results.NotFound() : Results.Ok(item);
});

app.MapPost("/api/affiliates/{id}/catalog-items", async (
    HttpContext ctx, ICatalogCrudService catalogCrud, IMilestoneService milestones,
    Guid id, CreateCatalogItemRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        var item = await catalogCrud.CreateItemAsync(id, request);
        await milestones.MarkAsync(id, MilestoneKeys.FirstProductAdded,
            metadata: $$$"""{"itemId":"{{{item.Id}}}","source":"created"}""");
        return Results.Created($"/api/affiliates/{id}/catalog-items/{item.Id}", item);
    }
    catch (InvalidOperationException ex) when (ex.Message == PlanLimitService.TrialExpiredMessage)
    {
        return Results.Json(new { error = new { code = "TRIAL_EXPIRED", message = ex.Message } },
            statusCode: 402);
    }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("Plan limit"))
    {
        return Results.Json(new { error = new { code = "PLAN_LIMIT_REACHED", message = ex.Message } },
            statusCode: 402);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

app.MapMethods("/api/affiliates/{id}/catalog-items/{itemId}", new[] { "PATCH" },
    async (HttpContext ctx, ICatalogCrudService catalogCrud, IMilestoneService milestones,
           Guid id, Guid itemId, UpdateCatalogItemRequest request) =>
{
    var sub = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(sub))
        return Results.Unauthorized();

    try
    {
        var (item, wasDemo) = await catalogCrud.UpdateAsync(sub, id, itemId, request);

        if (wasDemo)
            await milestones.MarkAsync(id, MilestoneKeys.FirstProductAdded,
                metadata: $$$"""{"itemId":"{{{itemId}}}","source":"demo_edited"}""");

        return Results.Ok(item);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex) when (ex.Message == PlanLimitService.TrialExpiredMessage)
    {
        return Results.Json(new { error = new { code = "TRIAL_EXPIRED", message = ex.Message } },
            statusCode: 402);
    }
    catch (InvalidOperationException ex) when (ex.Message.StartsWith("Plan limit"))
    {
        return Results.Json(new { error = new { code = "PLAN_LIMIT_REACHED", message = ex.Message } },
            statusCode: 402);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

app.MapDelete("/api/affiliates/{id}/catalog-items/{itemId}", async (HttpContext ctx, ICatalogCrudService catalogCrud, Guid id, Guid itemId) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var deleted = await catalogCrud.DeleteItemAsync(id, itemId);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// ============ CANALES CRUD (dashboard) ============
app.MapGet("/api/affiliates/{id}/canales", async (HttpContext ctx, ICanalService canalService, Guid id) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var canales = await canalService.GetCanalesAsync(id);
    return Results.Ok(canales);
});

app.MapPost("/api/affiliates/{id}/canales", async (HttpContext ctx, ICanalService canalService, IMilestoneService milestones, Guid id, CreateCanalRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        var canal = await canalService.CreateAsync(id, request);

        if (canal.Tipo == CanalTipo.WhatsApp.ToString() && canal.Activo)
            await milestones.MarkAsync(id, MilestoneKeys.WhatsAppConfigured);

        return Results.Created($"/api/affiliates/{id}/canales/{canal.Id}", canal);
    }
    catch (InvalidOperationException ex) when (ex.Message == PlanLimitService.TrialExpiredMessage)
    {
        return Results.Json(new { error = new { code = "TRIAL_EXPIRED", message = ex.Message } },
            statusCode: 402);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
});

app.MapMethods("/api/affiliates/{id}/canales/{canalId}", new[] { "PATCH" },
    async (HttpContext ctx, ICanalService canalService, Guid id, Guid canalId, UpdateCanalRequest request) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    try
    {
        var canal = await canalService.UpdateAsync(id, canalId, request);
        return canal == null ? Results.NotFound() : Results.Ok(canal);
    }
    catch (InvalidOperationException ex) when (ex.Message == PlanLimitService.TrialExpiredMessage)
    {
        return Results.Json(new { error = new { code = "TRIAL_EXPIRED", message = ex.Message } },
            statusCode: 402);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
});

app.MapDelete("/api/affiliates/{id}/canales/{canalId}", async (HttpContext ctx, ICanalService canalService, Guid id, Guid canalId) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var deleted = await canalService.DeleteAsync(id, canalId);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// ============ METRICS (dashboard) ============
app.MapGet("/api/affiliates/{id}/metrics/detailed", async (HttpContext ctx, AppDbContext db, Guid id, int days = 30) =>
{
    var activeAffiliate = ctx.User.FindFirst("active_affiliate_id")?.Value;
    if (activeAffiliate != id.ToString())
        return Results.Forbid();

    var effectiveDays = days > 0 ? days : 30;
    var startDate = DateTime.UtcNow.Date.AddDays(-(effectiveDays - 1));

    // Materialize raw events first — GroupBy with per-Tipo conditional counts on a
    // date-truncated key doesn't translate reliably to SQL, so aggregate in-memory instead.
    var rawEvents = await db.EventosInteraccion
        .Where(e => e.AffiliateId == id && e.CreatedAt >= startDate)
        .Select(e => new { e.CreatedAt, e.Tipo })
        .ToListAsync();

    var countsByDay = rawEvents
        .GroupBy(e => e.CreatedAt.Date)
        .ToDictionary(g => g.Key, g => (
            PageViews: g.Count(x => x.Tipo == EventoTipo.PageView),
            QrScans: g.Count(x => x.Tipo == EventoTipo.QrScan),
            CanalClicks: g.Count(x => x.Tipo == EventoTipo.CanalClick)));

    // Pedidos pagados en el mismo rango — mismo patrón que rawEvents arriba: se trae solo lo
    // necesario y se agrupa en memoria por fecha (evita repetir el problema de GroupBy+Date en SQL).
    var paidOrders = await db.Orders
        .Where(o => o.AffiliateId == id && o.Status == OrderStatus.Paid && o.CreatedAt >= startDate)
        .Select(o => new { o.CreatedAt, o.Total, o.Currency })
        .ToListAsync();

    var ordersByDay = paidOrders
        .GroupBy(o => o.CreatedAt.Date)
        .ToDictionary(g => g.Key, g => g.Count());

    var dailyCounts = new List<DailyCountDto>();
    for (var day = startDate; day <= DateTime.UtcNow.Date; day = day.AddDays(1))
    {
        countsByDay.TryGetValue(day, out var counts);
        ordersByDay.TryGetValue(day, out var ordersThatDay);
        dailyCounts.Add(new DailyCountDto(day.ToString("yyyy-MM-dd"),
            counts.PageViews, counts.QrScans, counts.CanalClicks, ordersThatDay));
    }

    var totalVisits = dailyCounts.Sum(d => d.PageViews);
    var totalRevenue = paidOrders.Sum(o => o.Total);
    var conversion = new ConversionSummaryDto(
        Visits: totalVisits,
        PaidOrders: paidOrders.Count,
        ConversionRatePct: totalVisits > 0 ? Math.Round(paidOrders.Count * 100m / totalVisits, 1) : 0,
        Revenue: totalRevenue,
        Currency: paidOrders.FirstOrDefault()?.Currency ?? "USD"
    );

    var canalClickCounts = await db.EventosInteraccion
        .Where(e => e.AffiliateId == id && e.Tipo == EventoTipo.CanalClick && e.CanalId != null && e.CreatedAt >= startDate)
        .GroupBy(e => e.CanalId!.Value)
        .Select(g => new { CanalId = g.Key, Clicks = g.Count() })
        .ToListAsync();

    var canales = await db.Canales
        .Where(c => c.AffiliateId == id)
        .ToDictionaryAsync(c => c.Id);

    var byCanal = canalClickCounts
        .Where(cc => canales.ContainsKey(cc.CanalId))
        .Select(cc => new CanalBreakdownDto(
            cc.CanalId, canales[cc.CanalId].Tipo.ToString(), canales[cc.CanalId].NombreVisible, cc.Clicks))
        .OrderByDescending(c => c.Clicks)
        .ToList();

    return Results.Ok(new DetailedMetricsResponse(dailyCounts, byCanal, conversion));
});

// ============ SPACE DASHBOARD AGGREGATOR ============
app.MapGet("/api/space/{slug}", async (
    HttpContext ctx,
    AppDbContext db,
    IMilestoneService milestones,
    string slug) =>
{
    var sub = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(sub))
        return Results.Unauthorized();

    var affiliate = await db.Affiliates
        .FirstOrDefaultAsync(a => a.Slug == slug);
    if (affiliate is null)
        return Results.NotFound();

    var userMap = await db.UserAffiliateMaps
        .FirstOrDefaultAsync(m => m.SupabaseUserId == sub && m.AffiliateId == affiliate.Id);
    if (userMap is null)
        return Results.Forbid();

    var items = affiliate.BusinessType switch
    {
        BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher =>
            (await db.Products
                .Where(p => p.AffiliateId == affiliate.Id)
                .OrderBy(p => p.SortOrder)
                .ToListAsync())
                .Select(p => new SpaceItemDto(p.Id, p.Name, p.Category, p.IsDemo, p.Status == "Active", p.ImageUrl,
                    p.Description,
                    TokenList.Parse(p.Periods), TokenList.Parse(p.Flags), p.Featured, p.Popular))
                .ToList(),

        BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
            await db.Services
                .Where(s => s.AffiliateId == affiliate.Id)
                .OrderBy(s => s.SortOrder)
                .Select(s => new SpaceItemDto(s.Id, s.Name, s.Category, s.IsDemo, s.Status == "Active", s.ImageUrl,
                    s.Description,
                    new List<string>(), null, null, null))
                .ToListAsync(),

        BusinessType.Retail =>
            await db.InventoryItems
                .Where(i => i.AffiliateId == affiliate.Id)
                .OrderBy(i => i.SortOrder)
                .Select(i => new SpaceItemDto(i.Id, i.Name, i.Category, i.IsDemo, i.Status == "Active", i.ImageUrl,
                    i.Description,
                    new List<string>(), null, null, null))
                .ToListAsync(),

        _ => new List<SpaceItemDto>()
    };

    var realCount = items.Count(i => !i.IsDemo);

    var canales = (await db.Canales
        .Where(c => c.AffiliateId == affiliate.Id && c.Activo)
        .OrderBy(c => c.Orden)
        .ToListAsync())
        .Select(c => new CanalDto(c.Id, c.Tipo.ToString(), c.Metodo.ToString(), c.ValorCrudo,
            c.EnlaceGenerado, c.NombreVisible, c.Verificado, c.Orden, c.Activo))
        .ToList();

    HashSet<string> completedKeys;
    try { completedKeys = await milestones.GetCompletedKeysAsync(affiliate.Id); }
    catch { completedKeys = new HashSet<string>(); }

    var eventCounts = await db.EventosInteraccion
        .Where(e => e.AffiliateId == affiliate.Id)
        .GroupBy(e => e.Tipo)
        .Select(g => new { Tipo = g.Key, Count = g.Count() })
        .ToDictionaryAsync(g => g.Tipo, g => g.Count);

    var visitasCount = eventCounts.GetValueOrDefault(EventoTipo.PageView);
    var escaneosQrCount = eventCounts.GetValueOrDefault(EventoTipo.QrScan);
    var clicsCanalesCount = eventCounts.GetValueOrDefault(EventoTipo.CanalClick);

    // Disponible = "this metric is tracked", not "count > 0" — all four are live and
    // tracked today, so 0 is a real, valid value (a brand-new affiliate with no
    // traffic yet) and must render as "0", not as "Próximamente" (which the frontend
    // reserves for genuinely unbuilt features). Was previously gating disponible on
    // count > 0, which made every one of these show "Próximamente" for any affiliate
    // with zero activity — indistinguishable from the feature not existing at all.
    var kpis = new KpisDto(
        Visitas: new KpiValueDto(visitasCount, true),
        ItemsPublicados: new KpiValueDto(realCount, true),
        EscaneosQr: new KpiValueDto(escaneosQrCount, true),
        ClicsCanales: new KpiValueDto(clicsCanalesCount, true));

    DateTime? trialEndsAt = affiliate.Plan == Plan.Free ? affiliate.CreatedAt.AddDays(30) : null;
    int? trialDaysRemaining = trialEndsAt is null
        ? null
        : Math.Max(0, (int)Math.Ceiling((trialEndsAt.Value - DateTime.UtcNow).TotalDays));

    return Results.Ok(new SpaceResponse(
        new BusinessDto(
            affiliate.Id, affiliate.Slug!, affiliate.Name,
            affiliate.BusinessType.ToString(),
            affiliate.Plan.ToString().ToLower(),
            affiliate.PlanStatus.ToString(),
            affiliate.WhatsApp, affiliate.PrimaryColor,
            affiliate.DescriptionEn,
            canales, ModuleCatalog.FilterActive(affiliate.ModulosActivos),
            JsonArrayField.Parse<ProcessStepDto>(affiliate.ProcessSteps),
            JsonArrayField.Parse<FaqItemDto>(affiliate.Faq),
            JsonArrayField.Parse<HorarioEntryDto>(affiliate.Horario),
            affiliate.Timezone,
            trialDaysRemaining, trialEndsAt, affiliate.Currency),
        items, realCount,
        new ProgressDto(
            FirstProductAdded: completedKeys.Contains(MilestoneKeys.FirstProductAdded),
            CanalesConfigured: canales.Count > 0,
            LinkShared: completedKeys.Contains(MilestoneKeys.LinkShared)),
        kpis,
        userMap.Role.ToString()));
});

// ============ AFFILIATE SLUG RESOLVER ============
app.MapGet("/api/affiliates/by-slug/{slug}", async (HttpContext ctx, AppDbContext db, string slug) =>
{
    var sub = ctx.User.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(sub))
        return Results.Unauthorized();

    var affiliate = await db.Affiliates
        .Where(a => a.Slug == slug)
        .Select(a => new { a.Id, a.Slug, a.Name })
        .FirstOrDefaultAsync();
    if (affiliate is null)
        return Results.NotFound();

    var hasAccess = await db.UserAffiliateMaps
        .AnyAsync(m => m.SupabaseUserId == sub && m.AffiliateId == affiliate.Id);
    if (!hasAccess)
        return Results.Forbid();

    return Results.Ok(new AffiliateSlugLookupDto(affiliate.Id, affiliate.Slug!, affiliate.Name));
});

// ============ PUBLIC CATALOG ENDPOINTS (no auth) ============
app.MapGet("/api/public/affiliates/featured", async (IPublicCatalogService catalogService, HttpResponse response) =>
{
    var result = await catalogService.GetFeaturedAffiliatesAsync();
    response.Headers.CacheControl = "public, max-age=60";
    return Results.Ok(result);
})
.AllowAnonymous();

app.MapGet("/api/public/affiliates/{slug}", async (IPublicCatalogService catalogService, string slug, HttpResponse response) =>
{
    var result = await catalogService.GetAffiliateBySlugAsync(slug);
    if (result == null)
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });
    response.Headers.CacheControl = "public, max-age=60";
    return Results.Ok(result);
})
.AllowAnonymous();

app.MapGet("/api/public/affiliates/{slug}/catalog", async (IPublicCatalogService catalogService, string slug, HttpResponse response, Guid? screenId) =>
{
    var result = await catalogService.GetCatalogAsync(slug, screenId);
    if (result == null)
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });
    response.Headers.CacheControl = "public, max-age=60";
    return Results.Ok(result);
})
.AllowAnonymous();

// ============ PUBLIC BOOKING (agenda pública, sin login) ============
app.MapGet("/api/public/affiliates/{slug}/team", async (IPublicBookingService bookingService, string slug, HttpResponse response) =>
{
    var result = await bookingService.GetPublicTeamAsync(slug);
    if (result == null)
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });
    response.Headers.CacheControl = "public, max-age=30";
    return Results.Ok(result);
})
.AllowAnonymous();

app.MapGet("/api/public/affiliates/{slug}/services", async (IPublicBookingService bookingService, string slug, HttpResponse response) =>
{
    var result = await bookingService.GetPublicServicesAsync(slug);
    if (result == null)
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });
    response.Headers.CacheControl = "public, max-age=30";
    return Results.Ok(result);
})
.AllowAnonymous();

app.MapPost("/api/public/affiliates/{slug}/appointments", async (
    IPublicBookingService bookingService, string slug, CreatePublicAppointmentRequest request) =>
{
    try
    {
        var result = await bookingService.CreatePublicAppointmentAsync(slug, request);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = new { code = "SLOT_TAKEN", message = ex.Message } });
    }
})
.AllowAnonymous();

// ============ PUBLIC ORDERS (storefront checkout) ============
app.MapPost("/api/public/affiliates/{slug}/orders", async (
    IOrderService orderService, string slug, CreateOrderRequest request) =>
{
    try
    {
        var result = await orderService.CreateOrderAsync(slug, request);
        if (result is null)
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
    catch (Stripe.StripeException ex)
    {
        return Results.BadRequest(new { error = new { code = "STRIPE_ERROR", message = ex.Message } });
    }
})
.AllowAnonymous();

// Confirmación al volver del Checkout hospedado — ver nota en OrderService sobre por qué no
// hay webhook dedicado a esto todavía.
app.MapPost("/api/public/orders/{orderId}/confirm", async (
    IOrderService orderService, Guid orderId, ConfirmOrderRequest request) =>
{
    var result = await orderService.ConfirmCheckoutAsync(orderId, request.CheckoutSessionId);
    if (result is null)
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Order not found" } });
    return Results.Ok(result);
})
.AllowAnonymous();

// QrScan/CanalClick/PageView are fired by anonymous visitors on the public page — they never
// carry a Supabase JWT, so they cannot go through the authenticated /api/affiliates/{id}/events route.
app.MapPost("/api/public/affiliates/{slug}/events", async (
    AppDbContext db, IInteractionEventService events, string slug, PublicInteractionEventRequest request) =>
{
    var affiliate = await db.Affiliates
        .Where(a => a.Slug == slug && a.Published)
        .Select(a => new { a.Id })
        .FirstOrDefaultAsync();
    if (affiliate is null)
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });

    try
    {
        await events.RecordAsync(affiliate.Id, request.Type, request.CanalId);
        return Results.NoContent();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_INPUT", message = ex.Message } });
    }
})
.AllowAnonymous();

// ============ STRIPE WEBHOOK ============
// Bound to HttpContext only (no DTO parameter) so minimal-API does not attempt JSON
// model binding — Stripe's signature check requires the exact raw request body bytes.
app.MapPost("/api/webhooks/stripe", async (HttpContext ctx, IStripeBillingService billingService) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var json = await reader.ReadToEndAsync();
    var signature = ctx.Request.Headers["Stripe-Signature"].ToString();

    try
    {
        await billingService.HandleWebhookEventAsync(json, signature);
        return Results.Ok();
    }
    catch (Stripe.StripeException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_SIGNATURE", message = ex.Message } });
    }
})
.AllowAnonymous();

// ============ STRIPE CONNECT WEBHOOK ============
// Endpoint separado del de facturación de arriba — Stripe Connect envía eventos de cuenta
// (account.updated, etc.) a una URL propia, configurada aparte en el Dashboard, con su
// propio signing secret (STRIPE_CONNECT_WEBHOOK_SECRET).
app.MapPost("/api/webhooks/stripe-connect", async (HttpContext ctx, IStripeConnectService connectService) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var json = await reader.ReadToEndAsync();
    var signature = ctx.Request.Headers["Stripe-Signature"].ToString();

    try
    {
        await connectService.HandleWebhookEventAsync(json, signature);
        return Results.Ok();
    }
    catch (Stripe.StripeException ex)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_SIGNATURE", message = ex.Message } });
    }
})
.AllowAnonymous();

// Apply migrations + seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Fase 60 — panel de operaciones: siembra el primer admin de plataforma si la tabla está
    // vacía. SupabaseUserId="" hasta que esa persona inicie sesión con ese correo (mismo patrón
    // invite-claim que UserAffiliateMap) — ver PlatformAdminService.IsPlatformAdminAsync.
    if (!db.Set<PlatformAdmin>().Any())
    {
        db.Set<PlatformAdmin>().Add(new PlatformAdmin { SupabaseUserId = "", Email = "alejandropichardo85@gmail.com" });
        db.SaveChanges();
    }

    // Seed affiliates and users if DB is empty
    if (!db.Set<Affiliate>().Any())
    {
        var affiliates = new[]
        {
            // Pegote y Little Dominicana Restaurant son los dos casos reales mostrados en /casos y Home — Published=true.
            // El resto quedan como cuentas internas/demo, no expuestas públicamente todavía.
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"), Name = "Pegote Barbershop", Description = "Barbería premium", Modules = "appointments,payments,inventory,queue,team,products,campaigns", IsActive = true, Slug = "pegote-barber", BusinessType = BusinessType.Barber, Published = true, Address = "Elmira, NY", Plan = Plan.Entrepreneur, PlanStatus = PlanStatus.Active, PlanStartedAt = DateTime.UtcNow },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"), Name = "BritoColor", Description = "Salón de belleza", Modules = "appointments,payments,inventory,team,products,campaigns", IsActive = true, Slug = "britocolor", Plan = Plan.Entrepreneur, PlanStatus = PlanStatus.Active, PlanStartedAt = DateTime.UtcNow },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"), Name = "Little Dominicana Restaurant", Description = "Restaurante dominicano", Modules = "appointments,payments,inventory,team,products,campaigns", IsActive = true, Slug = "the-little-dominicana", BusinessType = BusinessType.Restaurant, Published = true, Address = "315 E Water St, Elmira NY 14901", Plan = Plan.Entrepreneur, PlanStatus = PlanStatus.Active, PlanStartedAt = DateTime.UtcNow },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"), Name = "Dr. Pichardo", Description = "Consulta médica", Modules = "appointments,payments,team,campaigns", IsActive = true, Slug = "dr-pichardo", BusinessType = BusinessType.Professional },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"), Name = "Masa Tina", Description = "Restaurante", Modules = "appointments,payments,inventory,team,products,campaigns", IsActive = true, Slug = "masa-tina", BusinessType = BusinessType.Restaurant },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000006"), Name = "MaalCa LLC", Description = "Ecosistema creativo", Modules = "appointments,payments,inventory,team,products,campaigns", IsActive = true, Slug = "maalca-llc", BusinessType = BusinessType.Creator },
        };
        db.Set<Affiliate>().AddRange(affiliates);
        db.SaveChanges();

        var hashedDemo = BCrypt.Net.BCrypt.HashPassword("demo");
        var users = new[]
        {
            new User { Email = "admin@maalca.com",      PasswordHash = hashedDemo, FullName = "Admin MaalCa",    Role = "Admin",   AffiliateId = affiliates[0].Id, IsActive = true },
            new User { Email = "pegote@maalca.com",      PasswordHash = hashedDemo, FullName = "Pegote Team",     Role = "Manager", AffiliateId = affiliates[0].Id, IsActive = true },
            new User { Email = "britocolor@maalca.com",  PasswordHash = hashedDemo, FullName = "BritoColor Team", Role = "Manager", AffiliateId = affiliates[1].Id, IsActive = true },
            new User { Email = "tld@maalca.com",         PasswordHash = hashedDemo, FullName = "TLD Team",        Role = "Manager", AffiliateId = affiliates[2].Id, IsActive = true },
            new User { Email = "drpichardo@maalca.com",  PasswordHash = hashedDemo, FullName = "Dr. Pichardo",    Role = "Manager", AffiliateId = affiliates[3].Id, IsActive = true },
            new User { Email = "masatina@maalca.com",    PasswordHash = hashedDemo, FullName = "Masa Tina",       Role = "Manager", AffiliateId = affiliates[4].Id, IsActive = true },
        };
        db.Set<User>().AddRange(users);
        db.SaveChanges();

        // ===== PEGOTE BARBERSHOP DEMO DATA =====
        var pegoteId = affiliates[0].Id;

        // --- Customers ---
        var customers = new[]
        {
            new Customer { Id = Guid.Parse("c1000000-0000-0000-0000-000000000001"), AffiliateId = pegoteId, Name = "Carlos Méndez", Email = "carlos@email.com", Phone = "809-555-0101", Status = "Active", TotalVisits = 12 },
            new Customer { Id = Guid.Parse("c1000000-0000-0000-0000-000000000002"), AffiliateId = pegoteId, Name = "Miguel Ángel Torres", Email = "miguel@email.com", Phone = "809-555-0102", Status = "Active", TotalVisits = 8 },
            new Customer { Id = Guid.Parse("c1000000-0000-0000-0000-000000000003"), AffiliateId = pegoteId, Name = "José Ramírez", Email = "jose@email.com", Phone = "809-555-0103", Status = "Active", TotalVisits = 5 },
            new Customer { Id = Guid.Parse("c1000000-0000-0000-0000-000000000004"), AffiliateId = pegoteId, Name = "Luis Hernández", Email = "luis@email.com", Phone = "809-555-0104", Status = "Active", TotalVisits = 3 },
            new Customer { Id = Guid.Parse("c1000000-0000-0000-0000-000000000005"), AffiliateId = pegoteId, Name = "Pedro Santana", Email = "pedro@email.com", Phone = "809-555-0105", Status = "Active", TotalVisits = 15 },
            new Customer { Id = Guid.Parse("c1000000-0000-0000-0000-000000000006"), AffiliateId = pegoteId, Name = "Andrés Castillo", Email = "andres@email.com", Phone = "809-555-0106", Status = "Inactive", TotalVisits = 1 },
        };
        db.Customers.AddRange(customers);
        db.SaveChanges();

        // --- Services ---
        var services = new[]
        {
            new Maalca.Domain.Entities.Service { Id = Guid.Parse("51000000-0000-0000-0000-000000000001"), AffiliateId = pegoteId, Name = "Corte Clásico", Description = "Corte de cabello tradicional con tijera y máquina", Price = 15.00m, DurationMinutes = 30, Category = "Cortes", IsActive = true },
            new Maalca.Domain.Entities.Service { Id = Guid.Parse("51000000-0000-0000-0000-000000000002"), AffiliateId = pegoteId, Name = "Corte + Barba", Description = "Corte de cabello y perfilado de barba", Price = 25.00m, DurationMinutes = 45, Category = "Cortes", IsActive = true },
            new Maalca.Domain.Entities.Service { Id = Guid.Parse("51000000-0000-0000-0000-000000000003"), AffiliateId = pegoteId, Name = "Barba Completa", Description = "Afeitado y perfilado de barba con toalla caliente", Price = 12.00m, DurationMinutes = 20, Category = "Barba", IsActive = true },
            new Maalca.Domain.Entities.Service { Id = Guid.Parse("51000000-0000-0000-0000-000000000004"), AffiliateId = pegoteId, Name = "Diseño de Cejas", Description = "Perfilado y diseño de cejas masculinas", Price = 8.00m, DurationMinutes = 15, Category = "Extras", IsActive = true },
            new Maalca.Domain.Entities.Service { Id = Guid.Parse("51000000-0000-0000-0000-000000000005"), AffiliateId = pegoteId, Name = "Corte Premium", Description = "Corte + barba + cejas + masaje capilar", Price = 40.00m, DurationMinutes = 60, Category = "Premium", IsActive = true },
            new Maalca.Domain.Entities.Service { Id = Guid.Parse("51000000-0000-0000-0000-000000000006"), AffiliateId = pegoteId, Name = "Corte Infantil", Description = "Corte para niños menores de 12 años", Price = 10.00m, DurationMinutes = 20, Category = "Cortes", IsActive = true },
        };
        db.Services.AddRange(services);
        db.SaveChanges();

        // --- Team Members ---
        var team = new[]
        {
            new TeamMember { Id = Guid.Parse("b1000000-0000-0000-0000-000000000001"), AffiliateId = pegoteId, Name = "Danny Pegote", Email = "danny@pegote.com", Phone = "809-555-0201", Role = "Barbero Senior", Department = "Barbería", JoinDate = new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
            new TeamMember { Id = Guid.Parse("b1000000-0000-0000-0000-000000000002"), AffiliateId = pegoteId, Name = "Ramón Cruz", Email = "ramon@pegote.com", Phone = "809-555-0202", Role = "Barbero", Department = "Barbería", JoinDate = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
            new TeamMember { Id = Guid.Parse("b1000000-0000-0000-0000-000000000003"), AffiliateId = pegoteId, Name = "Julio Reyes", Email = "julio@pegote.com", Phone = "809-555-0203", Role = "Barbero", Department = "Barbería", JoinDate = new DateTime(2022, 3, 10, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
            new TeamMember { Id = Guid.Parse("b1000000-0000-0000-0000-000000000004"), AffiliateId = pegoteId, Name = "María López", Email = "maria@pegote.com", Phone = "809-555-0204", Role = "Recepcionista", Department = "Administración", JoinDate = new DateTime(2021, 9, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
        };
        db.TeamMembers.AddRange(team);
        db.SaveChanges();

        // --- Products ---
        var products = new[]
        {
            new Product { Id = Guid.Parse("d1000000-0000-0000-0000-000000000001"), AffiliateId = pegoteId, Name = "Pomada Mate", Description = "Pomada de acabado mate para cabello", Category = "Styling", Price = 12.99m, Stock = 25, Status = "Active" },
            new Product { Id = Guid.Parse("d1000000-0000-0000-0000-000000000002"), AffiliateId = pegoteId, Name = "Cera para Cabello", Description = "Cera moldeadora fijación fuerte", Category = "Styling", Price = 14.99m, Stock = 18, Status = "Active" },
            new Product { Id = Guid.Parse("d1000000-0000-0000-0000-000000000003"), AffiliateId = pegoteId, Name = "Aceite para Barba", Description = "Aceite hidratante y acondicionador de barba", Category = "Barba", Price = 18.99m, Stock = 30, Status = "Active" },
            new Product { Id = Guid.Parse("d1000000-0000-0000-0000-000000000004"), AffiliateId = pegoteId, Name = "Shampoo Anticaspa", Description = "Shampoo profesional anticaspa", Category = "Cuidado", Price = 9.99m, Stock = 40, Status = "Active" },
            new Product { Id = Guid.Parse("d1000000-0000-0000-0000-000000000005"), AffiliateId = pegoteId, Name = "Gel Fijación Extra", Description = "Gel de fijación extra fuerte", Category = "Styling", Price = 7.99m, Stock = 50, Status = "Active" },
        };
        db.Products.AddRange(products);
        db.SaveChanges();

        // --- Inventory Items ---
        var inventory = new[]
        {
            new InventoryItem { Id = Guid.Parse("e1000000-0000-0000-0000-000000000001"), AffiliateId = pegoteId, Name = "Cuchillas de afeitar", Category = "Consumibles", Quantity = 200, MinStock = 50, UnitPrice = 0.50m, Status = "Active" },
            new InventoryItem { Id = Guid.Parse("e1000000-0000-0000-0000-000000000002"), AffiliateId = pegoteId, Name = "Toallas desechables", Category = "Consumibles", Quantity = 500, MinStock = 100, UnitPrice = 0.15m, Status = "Active" },
            new InventoryItem { Id = Guid.Parse("e1000000-0000-0000-0000-000000000003"), AffiliateId = pegoteId, Name = "Spray desinfectante", Category = "Limpieza", Quantity = 15, MinStock = 5, UnitPrice = 8.99m, Status = "Active" },
            new InventoryItem { Id = Guid.Parse("e1000000-0000-0000-0000-000000000004"), AffiliateId = pegoteId, Name = "Capas de corte", Category = "Equipamiento", Quantity = 10, MinStock = 3, UnitPrice = 12.00m, Status = "Active" },
            new InventoryItem { Id = Guid.Parse("e1000000-0000-0000-0000-000000000005"), AffiliateId = pegoteId, Name = "Aftershave", Category = "Consumibles", Quantity = 8, MinStock = 3, UnitPrice = 15.00m, Status = "Active" },
        };
        db.InventoryItems.AddRange(inventory);
        db.SaveChanges();

        // --- Appointments (próximos días) ---
        var today = DateTime.UtcNow.Date;
        var appointments = new[]
        {
            new Appointment { Id = Guid.NewGuid(), AffiliateId = pegoteId, CustomerId = customers[0].Id, ServiceId = services[1].Id, Date = today, Time = "09:00", Status = "Completed", AssignedToId = team[0].Id, Notes = "Cliente regular" },
            new Appointment { Id = Guid.NewGuid(), AffiliateId = pegoteId, CustomerId = customers[1].Id, ServiceId = services[0].Id, Date = today, Time = "10:00", Status = "Completed", AssignedToId = team[1].Id },
            new Appointment { Id = Guid.NewGuid(), AffiliateId = pegoteId, CustomerId = customers[2].Id, ServiceId = services[4].Id, Date = today, Time = "11:00", Status = "Scheduled", AssignedToId = team[0].Id },
            new Appointment { Id = Guid.NewGuid(), AffiliateId = pegoteId, CustomerId = customers[3].Id, ServiceId = services[2].Id, Date = today, Time = "14:00", Status = "Scheduled", AssignedToId = team[2].Id },
            new Appointment { Id = Guid.NewGuid(), AffiliateId = pegoteId, CustomerId = customers[4].Id, ServiceId = services[1].Id, Date = today.AddDays(1), Time = "09:30", Status = "Scheduled", AssignedToId = team[0].Id },
            new Appointment { Id = Guid.NewGuid(), AffiliateId = pegoteId, CustomerId = customers[0].Id, ServiceId = services[0].Id, Date = today.AddDays(1), Time = "11:00", Status = "Scheduled", AssignedToId = team[1].Id },
            new Appointment { Id = Guid.NewGuid(), AffiliateId = pegoteId, CustomerId = customers[1].Id, ServiceId = services[4].Id, Date = today.AddDays(2), Time = "10:00", Status = "Scheduled", AssignedToId = team[2].Id },
            new Appointment { Id = Guid.NewGuid(), AffiliateId = pegoteId, CustomerId = customers[3].Id, ServiceId = services[5].Id, Date = today.AddDays(-1), Time = "15:00", Status = "Completed", AssignedToId = team[1].Id },
        };
        db.Appointments.AddRange(appointments);
        db.SaveChanges();

        // --- Invoices ---
        var invoices = new[]
        {
            new Invoice { Id = Guid.Parse("f1000000-0000-0000-0000-000000000001"), AffiliateId = pegoteId, CustomerId = customers[0].Id, InvoiceNumber = "INV-2026-001", Subtotal = 25.00m, Tax = 1.75m, Total = 26.75m, Status = "Paid", IssueDate = today.AddDays(-7), PaidDate = today.AddDays(-7) },
            new Invoice { Id = Guid.Parse("f1000000-0000-0000-0000-000000000002"), AffiliateId = pegoteId, CustomerId = customers[1].Id, InvoiceNumber = "INV-2026-002", Subtotal = 40.00m, Tax = 2.80m, Total = 42.80m, Status = "Paid", IssueDate = today.AddDays(-5), PaidDate = today.AddDays(-5) },
            new Invoice { Id = Guid.Parse("f1000000-0000-0000-0000-000000000003"), AffiliateId = pegoteId, CustomerId = customers[4].Id, InvoiceNumber = "INV-2026-003", Subtotal = 52.99m, Tax = 3.71m, Total = 56.70m, Status = "Paid", IssueDate = today.AddDays(-3), PaidDate = today.AddDays(-2) },
            new Invoice { Id = Guid.Parse("f1000000-0000-0000-0000-000000000004"), AffiliateId = pegoteId, CustomerId = customers[2].Id, InvoiceNumber = "INV-2026-004", Subtotal = 15.00m, Tax = 1.05m, Total = 16.05m, Status = "Pending", IssueDate = today, DueDate = today.AddDays(30) },
            new Invoice { Id = Guid.Parse("f1000000-0000-0000-0000-000000000005"), AffiliateId = pegoteId, CustomerId = customers[3].Id, InvoiceNumber = "INV-2026-005", Subtotal = 25.00m, Tax = 1.75m, Total = 26.75m, Status = "Overdue", IssueDate = today.AddDays(-45), DueDate = today.AddDays(-15) },
        };
        db.Invoices.AddRange(invoices);
        db.SaveChanges();

        // --- Gift Cards ---
        var giftCards = new[]
        {
            new GiftCard { Id = Guid.NewGuid(), AffiliateId = pegoteId, Code = "PEGOTE-GIFT-001", InitialAmount = 50.00m, Balance = 50.00m, RecipientEmail = "amigo@email.com", Message = "Feliz cumpleaños", Status = "Active", ExpiresAt = today.AddMonths(6) },
            new GiftCard { Id = Guid.NewGuid(), AffiliateId = pegoteId, Code = "PEGOTE-GIFT-002", InitialAmount = 100.00m, Balance = 35.00m, RecipientEmail = "regalo@email.com", Message = "Disfruta tu corte", Status = "Active", ExpiresAt = today.AddMonths(3) },
            new GiftCard { Id = Guid.NewGuid(), AffiliateId = pegoteId, Code = "PEGOTE-GIFT-003", InitialAmount = 25.00m, Balance = 0.00m, Status = "Redeemed" },
        };
        db.GiftCards.AddRange(giftCards);
        db.SaveChanges();

        // --- Campaigns ---
        var campaigns = new[]
        {
            new Campaign { Id = Guid.NewGuid(), AffiliateId = pegoteId, Name = "Promo Semana Santa", Type = "email", TargetAudience = "Todos los clientes", Content = "20% de descuento en corte premium durante Semana Santa", Status = "Sent", Schedule = today.AddDays(-10) },
            new Campaign { Id = Guid.NewGuid(), AffiliateId = pegoteId, Name = "Lanzamiento Aceite Barba", Type = "sms", TargetAudience = "Clientes con barba", Content = "Nuevo aceite para barba disponible — pruébalo gratis con tu próximo corte", Status = "Draft" },
            new Campaign { Id = Guid.NewGuid(), AffiliateId = pegoteId, Name = "Referidos Julio", Type = "email", TargetAudience = "Clientes activos", Content = "Trae un amigo y ambos reciben 15% de descuento", Status = "Scheduled", Schedule = today.AddDays(5) },
        };
        db.Campaigns.AddRange(campaigns);
        db.SaveChanges();

        app.Logger.LogInformation("Seeded {Affiliates} affiliates, {Users} users, and Pegote demo data (customers, services, team, products, inventory, appointments, invoices, giftcards, campaigns)", affiliates.Length, users.Length);
    }
}

// ============================================================
// AGENT EXECUTIONS — observability for n8n AI agents
// ============================================================

// POST /api/agents/executions — log an execution from n8n
app.MapPost("/api/agents/executions", async (AgentExecution execution, AppDbContext db) =>
{
    execution.Id = Guid.NewGuid();
    execution.CreatedAt = DateTime.UtcNow;
    db.AgentExecutions.Add(execution);
    await db.SaveChangesAsync();
    return Results.Created($"/api/agents/executions/{execution.Id}", execution);
})
.WithName("LogAgentExecution")
.WithTags("Agents")
.AllowAnonymous();

// GET /api/agents/executions — list recent executions
app.MapGet("/api/agents/executions", async (AppDbContext db, int? limit, string? status) =>
{
    var query = db.AgentExecutions.AsQueryable();
    if (!string.IsNullOrEmpty(status))
        query = query.Where(e => e.Status == status);
    var results = await query
        .OrderByDescending(e => e.CreatedAt)
        .Take(limit ?? 50)
        .ToListAsync();
    return Results.Ok(results);
})
.WithName("ListAgentExecutions")
.WithTags("Agents")
.AllowAnonymous();

// GET /api/agents/executions/{id}
app.MapGet("/api/agents/executions/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var execution = await db.AgentExecutions.FindAsync(id);
    return execution is not null ? Results.Ok(execution) : Results.NotFound();
})
.WithName("GetAgentExecution")
.WithTags("Agents")
.AllowAnonymous();

// PATCH /api/agents/executions/{id} — update status (n8n calls when done)
app.MapMethods("/api/agents/executions/{id:guid}", new[] { "PATCH" }, async (Guid id, AgentExecution update, AppDbContext db) =>
{
    var execution = await db.AgentExecutions.FindAsync(id);
    if (execution is null) return Results.NotFound();

    if (!string.IsNullOrEmpty(update.Status)) execution.Status = update.Status;
    if (update.TokensInput > 0) execution.TokensInput = update.TokensInput;
    if (update.TokensOutput > 0) execution.TokensOutput = update.TokensOutput;
    if (update.CostUsd > 0) execution.CostUsd = update.CostUsd;
    if (update.DurationMs > 0) execution.DurationMs = update.DurationMs;
    if (!string.IsNullOrEmpty(update.ErrorMessage)) execution.ErrorMessage = update.ErrorMessage;
    if (!string.IsNullOrEmpty(update.PrUrl)) execution.PrUrl = update.PrUrl;
    if (!string.IsNullOrEmpty(update.BranchName)) execution.BranchName = update.BranchName;
    if (update.RetryCount > 0) execution.RetryCount = update.RetryCount;
    execution.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(execution);
})
.WithName("UpdateAgentExecution")
.WithTags("Agents")
.AllowAnonymous();

// GET /api/agents/stats — aggregate stats
app.MapGet("/api/agents/stats", async (AppDbContext db) =>
{
    var total = await db.AgentExecutions.CountAsync();
    var success = await db.AgentExecutions.CountAsync(e => e.Status == "success");
    var failed = await db.AgentExecutions.CountAsync(e => e.Status == "failed");
    var totalCost = await db.AgentExecutions.SumAsync(e => e.CostUsd);
    var avgDuration = total > 0 ? await db.AgentExecutions.AverageAsync(e => e.DurationMs) : 0;

    return Results.Ok(new
    {
        total,
        success,
        failed,
        successRate = total > 0 ? (double)success / total * 100 : 0,
        totalCostUsd = totalCost,
        avgDurationMs = avgDuration
    });
})
.WithName("GetAgentStats")
.WithTags("Agents")
.AllowAnonymous();

app.MapGet("/health", () =>
{
    var sha = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA")?[..7] ?? "unknown";
    return Results.Ok(new { status = "healthy", sha, buildTime = DateTime.UtcNow.ToString("o") });
})
   .AllowAnonymous();

app.Run();
