using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Application.Services;
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

builder.Services.AddControllers();
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Maalca.Api.Hubs.QueueHub>("/hubs/queue");

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

// ============ CUSTOMER ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/customers", async (ICustomerService customerService, Guid affiliateId, int page = 1, int limit = 20, string? search = null, string? status = null) =>
{
    var result = await customerService.GetCustomersAsync(affiliateId, page, limit, search, status);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/customers/{id:guid}", async (ICustomerService customerService, Guid affiliateId, Guid id) =>
{
    var result = await customerService.GetCustomerAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/customers", async (ICustomerService customerService, Guid affiliateId, Customer customer) =>
{
    var result = await customerService.CreateCustomerAsync(affiliateId, customer);
    return Results.Created($"/api/affiliates/{affiliateId:guid}/customers/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/customers/{id:guid}", async (ICustomerService customerService, Guid affiliateId, Guid id, Customer customer) =>
{
    var result = await customerService.UpdateCustomerAsync(affiliateId, id, customer);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/customers/{id:guid}", async (ICustomerService customerService, Guid affiliateId, Guid id) =>
{
    var result = await customerService.DeleteCustomerAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

// ============ APPOINTMENT ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/appointments", async (IAppointmentService appointmentService, Guid affiliateId, DateTime? date = null, string? status = null, int page = 1) =>
{
    var result = await appointmentService.GetAppointmentsAsync(affiliateId, date, status, page);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (IAppointmentService appointmentService, Guid affiliateId, Guid id) =>
{
    var result = await appointmentService.GetAppointmentAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/appointments", async (IAppointmentService appointmentService, Guid affiliateId, Appointment appointment) =>
{
    var result = await appointmentService.CreateAppointmentAsync(affiliateId, appointment);
    return Results.Created($"/api/affiliates/{affiliateId:guid}/appointments/{result.Id}", result);
});

app.MapPatch("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (IAppointmentService appointmentService, Guid affiliateId, Guid id, string status) =>
{
    var result = await appointmentService.UpdateAppointmentStatusAsync(affiliateId, id, status);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

// ============ SERVICE ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/services", async (IServiceService serviceService, Guid affiliateId, string? category = null, string? status = null) =>
{
    var result = await serviceService.GetServicesAsync(affiliateId, category, status);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id) =>
{
    var result = await serviceService.GetServiceAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/services", async (IServiceService serviceService, Guid affiliateId, Maalca.Domain.Entities.Service service) =>
{
    var result = await serviceService.CreateServiceAsync(affiliateId, service);
    return Results.Created($"/api/affiliates/{affiliateId:guid}/services/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id, Maalca.Domain.Entities.Service service) =>
{
    var result = await serviceService.UpdateServiceAsync(affiliateId, id, service);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/services/{id:guid}", async (IServiceService serviceService, Guid affiliateId, Guid id) =>
{
    var result = await serviceService.DeleteServiceAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
});

// ============ INVENTORY ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/inventory", async (IInventoryService inventoryService, Guid affiliateId, string? category = null, string? status = null, int page = 1) =>
{
    var result = await inventoryService.GetInventoryAsync(affiliateId, category, status, page);
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/inventory/movements", async (IInventoryService inventoryService, Guid affiliateId, InventoryMovement movement) =>
{
    try
    {
        var result = await inventoryService.CreateMovementAsync(affiliateId, movement);
        return Results.Created($"/api/affiliates/{affiliateId:guid}/inventory/{movement.InventoryItemId}", result);
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
    return Results.Created($"/api/affiliates/{affiliateId:guid}/queue/{result.Id}", result);
});

app.MapPatch("/api/affiliates/{affiliateId:guid}/queue/{id:guid}", async (IQueueService queueService, Guid affiliateId, Guid id, string status, Guid? barberId = null) =>
{
    var result = await queueService.UpdateQueueEntryAsync(affiliateId, id, status, barberId);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

// ============ TEAM ENDPOINTS ============
app.MapGet("/api/affiliates/{affiliateId:guid}/team", async (ITeamService teamService, Guid affiliateId, string? department = null, string? status = null) =>
{
    var result = await teamService.GetTeamAsync(affiliateId, department, status);
    return Results.Ok(result);
});

app.MapGet("/api/affiliates/{affiliateId:guid}/team/{id:guid}", async (ITeamService teamService, Guid affiliateId, Guid id) =>
{
    var result = await teamService.GetTeamMemberAsync(affiliateId, id);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPost("/api/affiliates/{affiliateId:guid}/team", async (ITeamService teamService, Guid affiliateId, TeamMember member) =>
{
    var result = await teamService.CreateTeamMemberAsync(affiliateId, member);
    return Results.Created($"/api/affiliates/{affiliateId:guid}/team/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/team/{id:guid}", async (ITeamService teamService, Guid affiliateId, Guid id, TeamMember member) =>
{
    var result = await teamService.UpdateTeamMemberAsync(affiliateId, id, member);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/team/{id:guid}", async (ITeamService teamService, Guid affiliateId, Guid id) =>
{
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
    return Results.Created($"/api/affiliates/{affiliateId:guid}/products/{result.Id}", result);
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
    return Results.Created($"/api/affiliates/{affiliateId:guid}/invoices/{result.Id}", result);
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
    return Results.Created($"/api/affiliates/{affiliateId:guid}/giftcards/{result.Id}", result);
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
    return Results.Created($"/api/affiliates/{affiliateId:guid}/campaigns/{result.Id}", result);
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

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
