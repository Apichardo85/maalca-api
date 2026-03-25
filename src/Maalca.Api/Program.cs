using System.Threading.RateLimiting;
using Maalca.Api.Filters;
using Maalca.Api.Middleware;
using Maalca.Api.Services;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Application.Services;
using Maalca.Domain.Common.Interfaces;
using Maalca.Domain.Entities;
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

// Current User Service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException("JWT key not configured. Set Jwt:Key in configuration or JWT_SECRET_KEY environment variable.");
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

        // Support SignalR JWT via query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/queue"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("role", "Admin"));
    options.AddPolicy("ManagerOrAdmin", policy => policy.RequireClaim("role", "Admin", "Manager"));
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddControllers();
builder.Services.AddSignalR();

var app = builder.Build();

// Middleware pipeline (order matters)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<Maalca.Api.Hubs.QueueHub>("/hubs/queue");

// ============ AUTH ENDPOINTS (Public) ============
app.MapPost("/api/auth/login", async (IAuthService authService, LoginRequest request) =>
{
    var result = await authService.LoginAsync(request);
    if (result == null)
        return Results.Unauthorized();
    return Results.Ok(result);
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/refresh", async (IAuthService authService, RefreshTokenRequest request) =>
{
    var result = await authService.RefreshTokenAsync(request);
    if (result == null)
        return Results.Unauthorized();
    return Results.Ok(result);
}).RequireRateLimiting("auth");

// ============ AFFILIATE-SCOPED ENDPOINTS (Authenticated + Tenant-Isolated) ============
var affiliateGroup = app.MapGroup("/api/affiliates/{affiliateId:guid}")
    .RequireAuthorization()
    .AddEndpointFilter<AffiliateAuthorizationFilter>()
    .RequireRateLimiting("api");

// -- Affiliate Config --
affiliateGroup.MapGet("", async (IAffiliateService affiliateService, Guid affiliateId) =>
{
    var result = await affiliateService.GetAffiliateAsync(affiliateId);
    if (result == null)
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Affiliate not found" } });
    return Results.Ok(result);
});

// -- Customers --
affiliateGroup.MapGet("/customers", async (ICustomerService customerService, Guid affiliateId, int page = 1, int limit = 20, string? search = null, string? status = null) =>
{
    var result = await customerService.GetCustomersAsync(affiliateId, page, limit, search, status);
    return Results.Ok(result);
});

affiliateGroup.MapGet("/customers/{id:guid}", async (ICustomerService customerService, Guid affiliateId, Guid id) =>
{
    var result = await customerService.GetCustomerAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapPost("/customers", async (ICustomerService customerService, Guid affiliateId, Customer customer) =>
{
    var result = await customerService.CreateCustomerAsync(affiliateId, customer);
    return Results.Created($"/api/affiliates/{affiliateId}/customers/{result.Id}", result);
});

affiliateGroup.MapPut("/customers/{id:guid}", async (ICustomerService customerService, Guid affiliateId, Guid id, Customer customer) =>
{
    var result = await customerService.UpdateCustomerAsync(affiliateId, id, customer);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapDelete("/customers/{id:guid}", async (ICustomerService customerService, Guid affiliateId, Guid id) =>
{
    var result = await customerService.DeleteCustomerAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
}).RequireAuthorization("ManagerOrAdmin");

// -- Appointments --
affiliateGroup.MapGet("/appointments", async (IAppointmentService appointmentService, Guid affiliateId, DateTime? date = null, string? status = null, int page = 1) =>
{
    var result = await appointmentService.GetAppointmentsAsync(affiliateId, date, status, page);
    return Results.Ok(result);
});

affiliateGroup.MapGet("/appointments/{id:guid}", async (IAppointmentService appointmentService, Guid affiliateId, Guid id) =>
{
    var result = await appointmentService.GetAppointmentAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapPost("/appointments", async (IAppointmentService appointmentService, Guid affiliateId, Appointment appointment) =>
{
    var result = await appointmentService.CreateAppointmentAsync(affiliateId, appointment);
    return Results.Created($"/api/affiliates/{affiliateId}/appointments/{result.Id}", result);
});

affiliateGroup.MapPatch("/appointments/{id:guid}", async (IAppointmentService appointmentService, Guid affiliateId, Guid id, string status) =>
{
    var result = await appointmentService.UpdateAppointmentStatusAsync(affiliateId, id, status);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

// -- Services --
affiliateGroup.MapGet("/services", async (IServiceService serviceService, Guid affiliateId, string? category = null, string? status = null) =>
{
    var result = await serviceService.GetServicesAsync(affiliateId, category, status);
    return Results.Ok(result);
});

affiliateGroup.MapGet("/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id) =>
{
    var result = await serviceService.GetServiceAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapPost("/services", async (IServiceService serviceService, Guid affiliateId, Maalca.Domain.Entities.Service service) =>
{
    var result = await serviceService.CreateServiceAsync(affiliateId, service);
    return Results.Created($"/api/affiliates/{affiliateId}/services/{result.Id}", result);
});

affiliateGroup.MapPut("/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id, Maalca.Domain.Entities.Service service) =>
{
    var result = await serviceService.UpdateServiceAsync(affiliateId, id, service);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapDelete("/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id) =>
{
    var result = await serviceService.DeleteServiceAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
}).RequireAuthorization("ManagerOrAdmin");

// -- Inventory --
affiliateGroup.MapGet("/inventory", async (IInventoryService inventoryService, Guid affiliateId, string? category = null, string? status = null, int page = 1) =>
{
    var result = await inventoryService.GetInventoryAsync(affiliateId, category, status, page);
    return Results.Ok(result);
});

affiliateGroup.MapPost("/inventory/movements", async (IInventoryService inventoryService, Guid affiliateId, InventoryMovement movement) =>
{
    var result = await inventoryService.CreateMovementAsync(affiliateId, movement);
    return Results.Created($"/api/affiliates/{affiliateId}/inventory/{movement.InventoryItemId}", result);
});

// -- Queue --
affiliateGroup.MapGet("/queue", async (IQueueService queueService, Guid affiliateId) =>
{
    var result = await queueService.GetQueueAsync(affiliateId);
    return Results.Ok(result);
});

affiliateGroup.MapPost("/queue", async (IQueueService queueService, Guid affiliateId, QueueEntry entry) =>
{
    var result = await queueService.AddToQueueAsync(affiliateId, entry);
    return Results.Created($"/api/affiliates/{affiliateId}/queue/{result.Id}", result);
});

affiliateGroup.MapPatch("/queue/{id:guid}", async (IQueueService queueService, Guid affiliateId, Guid id, string status, Guid? barberId = null) =>
{
    var result = await queueService.UpdateQueueEntryAsync(affiliateId, id, status, barberId);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

// -- Team --
affiliateGroup.MapGet("/team", async (ITeamService teamService, Guid affiliateId, string? department = null, string? status = null) =>
{
    var result = await teamService.GetTeamAsync(affiliateId, department, status);
    return Results.Ok(result);
});

affiliateGroup.MapGet("/team/{id:guid}", async (ITeamService teamService, Guid affiliateId, Guid id) =>
{
    var result = await teamService.GetTeamMemberAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapPost("/team", async (ITeamService teamService, Guid affiliateId, TeamMember member) =>
{
    var result = await teamService.CreateTeamMemberAsync(affiliateId, member);
    return Results.Created($"/api/affiliates/{affiliateId}/team/{result.Id}", result);
});

affiliateGroup.MapPut("/team/{id:guid}", async (ITeamService teamService, Guid affiliateId, Guid id, TeamMember member) =>
{
    var result = await teamService.UpdateTeamMemberAsync(affiliateId, id, member);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapDelete("/team/{id:guid}", async (ITeamService teamService, Guid affiliateId, Guid id) =>
{
    var result = await teamService.DeleteTeamMemberAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
}).RequireAuthorization("ManagerOrAdmin");

// -- Products --
affiliateGroup.MapGet("/products", async (IProductService productService, Guid affiliateId, string? category = null, string? status = null) =>
{
    var result = await productService.GetProductsAsync(affiliateId, category, status);
    return Results.Ok(result);
});

affiliateGroup.MapGet("/products/{id:guid}", async (IProductService productService, Guid affiliateId, Guid id) =>
{
    var result = await productService.GetProductAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapPost("/products", async (IProductService productService, Guid affiliateId, Product product) =>
{
    var result = await productService.CreateProductAsync(affiliateId, product);
    return Results.Created($"/api/affiliates/{affiliateId}/products/{result.Id}", result);
});

affiliateGroup.MapPut("/products/{id:guid}", async (IProductService productService, Guid affiliateId, Guid id, Product product) =>
{
    var result = await productService.UpdateProductAsync(affiliateId, id, product);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapDelete("/products/{id:guid}", async (IProductService productService, Guid affiliateId, Guid id) =>
{
    var result = await productService.DeleteProductAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
}).RequireAuthorization("ManagerOrAdmin");

// -- Invoices --
affiliateGroup.MapGet("/invoices", async (IInvoiceService invoiceService, Guid affiliateId, string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null) =>
{
    var result = await invoiceService.GetInvoicesAsync(affiliateId, status, dateFrom, dateTo);
    return Results.Ok(result);
});

affiliateGroup.MapGet("/invoices/{id:guid}", async (IInvoiceService invoiceService, Guid affiliateId, Guid id) =>
{
    var result = await invoiceService.GetInvoiceAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapPost("/invoices", async (IInvoiceService invoiceService, Guid affiliateId, Invoice invoice) =>
{
    var result = await invoiceService.CreateInvoiceAsync(affiliateId, invoice);
    return Results.Created($"/api/affiliates/{affiliateId}/invoices/{result.Id}", result);
});

// -- Gift Cards --
affiliateGroup.MapGet("/giftcards", async (IGiftCardService giftCardService, Guid affiliateId, string? status = null) =>
{
    var result = await giftCardService.GetGiftCardsAsync(affiliateId, status);
    return Results.Ok(result);
});

affiliateGroup.MapGet("/giftcards/{id:guid}", async (IGiftCardService giftCardService, Guid affiliateId, Guid id) =>
{
    var result = await giftCardService.GetGiftCardAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapPost("/giftcards", async (IGiftCardService giftCardService, Guid affiliateId, GiftCard giftCard) =>
{
    var result = await giftCardService.CreateGiftCardAsync(affiliateId, giftCard);
    return Results.Created($"/api/affiliates/{affiliateId}/giftcards/{result.Id}", result);
});

// -- Campaigns --
affiliateGroup.MapGet("/campaigns", async (ICampaignService campaignService, Guid affiliateId, string? status = null) =>
{
    var result = await campaignService.GetCampaignsAsync(affiliateId, status);
    return Results.Ok(result);
});

affiliateGroup.MapGet("/campaigns/{id:guid}", async (ICampaignService campaignService, Guid affiliateId, Guid id) =>
{
    var result = await campaignService.GetCampaignAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

affiliateGroup.MapPost("/campaigns", async (ICampaignService campaignService, Guid affiliateId, Campaign campaign) =>
{
    var result = await campaignService.CreateCampaignAsync(affiliateId, campaign);
    return Results.Created($"/api/affiliates/{affiliateId}/campaigns/{result.Id}", result);
});

// -- Metrics --
affiliateGroup.MapGet("/metrics", async (IMetricsService metricsService, Guid affiliateId) =>
{
    var result = await metricsService.GetMetricsAsync(affiliateId);
    return Results.Ok(result);
});

// -- Audit Logs (Admin/Manager only) --
affiliateGroup.MapGet("/audit-logs", async (IAuditLogService auditLogService, Guid affiliateId, string? entityType = null, string? entityId = null, string? userId = null, DateTime? from = null, DateTime? to = null, int page = 1, int limit = 50) =>
{
    var result = await auditLogService.GetAuditLogsAsync(affiliateId, entityType, entityId, userId, from, to, page, limit);
    return Results.Ok(result);
}).RequireAuthorization("ManagerOrAdmin");

// ============ PUBLIC ENDPOINTS ============
app.MapGet("/api/metrics/overview", async (ILeadService leadService) =>
{
    var result = await leadService.GetOverviewMetricsAsync();
    return Results.Ok(result);
});

app.MapPost("/api/leads/properties", async (ILeadService leadService, Lead lead) =>
{
    var result = await leadService.CreatePropertyLeadAsync(lead);
    return Results.Created($"/api/leads/properties/{result.Id}", result);
}).RequireRateLimiting("api");

app.MapPost("/api/leads/cirisonic", async (ILeadService leadService, Lead lead) =>
{
    var result = await leadService.CreateCirisonicLeadAsync(lead);
    return Results.Created($"/api/leads/cirisonic/{result.Id}", result);
}).RequireRateLimiting("api");

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
