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
    return Results.Created($"/api/affiliates/{affiliateId}/customers/{result.Id}", result);
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
    return Results.Created($"/api/affiliates/{affiliateId}/appointments/{result.Id}", result);
});

app.MapPut("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (IAppointmentService appointmentService, Guid affiliateId, Guid id, Appointment appointment) =>
{
    var result = await appointmentService.UpdateAppointmentAsync(affiliateId, id, appointment);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapPatch("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (IAppointmentService appointmentService, Guid affiliateId, Guid id, string status) =>
{
    var result = await appointmentService.UpdateAppointmentStatusAsync(affiliateId, id, status);
    if (result == null)
        return Results.NotFound();
    return Results.Ok(result);
});

app.MapDelete("/api/affiliates/{affiliateId:guid}/appointments/{id:guid}", async (IAppointmentService appointmentService, Guid affiliateId, Guid id) =>
{
    var result = await appointmentService.DeleteAppointmentAsync(affiliateId, id);
    if (!result)
        return Results.NotFound();
    return Results.NoContent();
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
    return Results.Created($"/api/affiliates/{affiliateId}/services/{result.Id}", result);
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
    return Results.Created($"/api/affiliates/{affiliateId}/team/{result.Id}", result);
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

// Apply migrations + seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Seed affiliates and users if DB is empty
    if (!db.Set<Affiliate>().Any())
    {
        var affiliates = new[]
        {
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"), Name = "Pegote Barbershop", Description = "Barbería premium", Modules = "appointments,payments,inventory,queue,team,products,campaigns", IsActive = true },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"), Name = "BritoColor", Description = "Salón de belleza", Modules = "appointments,payments,inventory,team,products,campaigns", IsActive = true },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"), Name = "The Little Dominican", Description = "Restaurante dominicano", Modules = "appointments,payments,inventory,team,products,campaigns", IsActive = true },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"), Name = "Dr. Pichardo", Description = "Consulta médica", Modules = "appointments,payments,team,campaigns", IsActive = true },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"), Name = "Masa Tina", Description = "Restaurante", Modules = "appointments,payments,inventory,team,products,campaigns", IsActive = true },
            new Affiliate { Id = Guid.Parse("a1000000-0000-0000-0000-000000000006"), Name = "MaalCa LLC", Description = "Ecosistema creativo", Modules = "appointments,payments,inventory,team,products,campaigns", IsActive = true },
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

app.Run();
