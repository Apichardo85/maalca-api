using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class OnboardingService : IOnboardingService
{
    private readonly AppDbContext _db;
    private readonly IAffiliateMapService _mapService;
    private readonly ICanalService _canalService;

    public OnboardingService(AppDbContext db, IAffiliateMapService mapService, ICanalService canalService)
    {
        _db = db;
        _mapService = mapService;
        _canalService = canalService;
    }

    public async Task<OnboardingResponse> OnboardAsync(string supabaseUserId, string email, OnboardingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length < 2 || request.Name.Length > 100)
            throw new ArgumentException("Name must be between 2 and 100 characters.");

        var existingMaps = await _mapService.GetMapsForUserAsync(supabaseUserId);
        if (existingMaps.Count > 0)
            throw new InvalidOperationException("User already has an affiliate.");

        if (!Enum.TryParse<BusinessType>(request.BusinessType, ignoreCase: true, out var businessType))
            throw new ArgumentException($"Invalid BusinessType: {request.BusinessType}");

        if (request.WhatsApp != null && !IsValidPhone(request.WhatsApp))
            throw new ArgumentException("WhatsApp must be a valid phone number (7–15 digits).");

        var slug = await GenerateUniqueSlugAsync(request.Name);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var affiliate = new Affiliate
            {
                Name = request.Name,
                Description = request.Description?.Trim(),
                WhatsApp = request.WhatsApp?.Trim(),
                BusinessType = businessType,
                Slug = slug,
                Plan = Plan.Free,
                PlanStatus = PlanStatus.Active,
                Published = true,
                PrimaryColor = request.PrimaryColor?.Trim(),
                LogoUrl = request.LogoUrl?.Trim()
            };

            _db.Affiliates.Add(affiliate);
            await _db.SaveChangesAsync();

            SeedDemoCatalog(affiliate.Id, businessType);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(affiliate.WhatsApp))
                await _canalService.CreateAsync(affiliate.Id,
                    new CreateCanalRequest("WhatsApp", "Manual", affiliate.WhatsApp));

            await _mapService.CreateMapAsync(supabaseUserId, email, affiliate.Id, AffiliateRole.Owner);

            await tx.CommitAsync();

            return new OnboardingResponse(
                affiliate.Id, affiliate.Name, affiliate.Slug!,
                affiliate.BusinessType.ToString(),
                affiliate.Description, affiliate.WhatsApp,
                affiliate.PrimaryColor, affiliate.LogoUrl);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private void SeedDemoCatalog(Guid affiliateId, BusinessType businessType)
    {
        switch (businessType)
        {
            case BusinessType.Barber:
                AddDemoServices(affiliateId, [
                    ("Corte Clásico",    "Fade o degradado a elección",               "Cortes", 25m,  30),
                    ("Corte + Barba",    "Servicio completo, toalla caliente incluida","Combos", 40m,  45),
                    ("Solo Barba",       "Perfilado y toalla caliente",                "Barba",  18m,  20),
                    ("Diseño y Líneas",  "Corte con diseño personalizado",             "Cortes", 35m,  35),
                    ("Corte Niño",       "Menores de 12",                              "Cortes", 18m,  20),
                ]);
                break;

            case BusinessType.Service:
            case BusinessType.Professional:
                AddDemoServices(affiliateId, [
                    ("Consulta Inicial",     "Evaluación y diagnóstico",       "Consultas",    50m,  60),
                    ("Servicio Estándar",    "Atención completa",               "Servicios",   120m,  60),
                    ("Mantenimiento",        "Revisión periódica",              "Mantenimiento", 75m, 30),
                    ("Paquete Mensual",      "4 sesiones al mes",               "Paquetes",    400m,  60),
                    ("Emergencia / Urgente", "Disponibilidad prioritaria",      "Servicios",   200m,  45),
                ]);
                break;

            case BusinessType.Restaurant:
                AddDemoProducts(affiliateId, [
                    ("Mofongo de Cerdo",      "Plátano verde majado con chicharrón crocante",       "Platos fuertes", 14.99m),
                    ("Pollo Guisado",         "Pollo en salsa criolla con sazón casera",             "Platos fuertes", 12.99m),
                    ("Sancocho Dominicano",   "Siete carnes, viandas, plátano y aguacate",           "Sopas",          16.99m),
                    ("Tres Golpes",           "Mangú, huevo, queso frito y salami",                  "Desayunos",      10.99m),
                    ("Tostones con Queso",    "Plátano verde frito con queso derretido",             "Acompañantes",    6.99m),
                ]);
                break;

            case BusinessType.Creator:
            case BusinessType.Publisher:
                AddDemoProducts(affiliateId, [
                    ("Guía Digital",      "Tu primera publicación digital",              "Guías",       29.99m),
                    ("Plantillas Premium","Colección de plantillas editables",            "Plantillas",  49.99m),
                    ("Sesión 1:1",        "Consultoría o sesión personalizada",           "Consultoría", 99.99m),
                    ("Curso Completo",    "Acceso completo a todos los módulos",          "Cursos",     149.99m),
                    ("Membresía Mensual", "Contenido nuevo cada mes",                     "Membresías",  19.99m),
                ]);
                break;

            case BusinessType.Retail:
                AddDemoInventoryItems(affiliateId, [
                    ("Producto Destacado A", "Descripción de tu producto principal", "Destacados", 29.99m),
                    ("Producto B",           "Descripción corta",                     "Categoría 1", 19.99m),
                    ("Producto C",           "Descripción corta",                     "Categoría 1", 24.99m),
                    ("Producto D",           "Descripción corta",                     "Categoría 2", 14.99m),
                    ("Producto E",           "Descripción corta",                     "Categoría 2",  9.99m),
                ]);
                break;
        }
    }

    private void AddDemoServices(Guid affiliateId,
        (string Name, string Desc, string Category, decimal Price, int Duration)[] items)
    {
        for (var i = 0; i < items.Length; i++)
        {
            var (name, desc, category, price, duration) = items[i];
            _db.Services.Add(new Service
            {
                AffiliateId = affiliateId,
                Name = name,
                Description = desc,
                Category = category,
                Price = price,
                DurationMinutes = duration,
                IsDemo = true,
                IsPubliclyVisible = true,
                Status = "Active",
                SortOrder = i
            });
        }
    }

    private void AddDemoProducts(Guid affiliateId,
        (string Name, string Desc, string Category, decimal Price)[] items)
    {
        for (var i = 0; i < items.Length; i++)
        {
            var (name, desc, category, price) = items[i];
            _db.Products.Add(new Product
            {
                AffiliateId = affiliateId,
                Name = name,
                Description = desc,
                Category = category,
                Price = price,
                IsDemo = true,
                IsPubliclyVisible = true,
                Status = "Active",
                SortOrder = i
            });
        }
    }

    private void AddDemoInventoryItems(Guid affiliateId,
        (string Name, string Desc, string Category, decimal Price)[] items)
    {
        for (var i = 0; i < items.Length; i++)
        {
            var (name, desc, category, price) = items[i];
            _db.InventoryItems.Add(new InventoryItem
            {
                AffiliateId = affiliateId,
                Name = name,
                Description = desc,
                Category = category,
                UnitPrice = price,
                Quantity = 0,
                IsDemo = true,
                IsPubliclyVisible = true,
                Status = "Active",
                SortOrder = i
            });
        }
    }

    internal static bool IsValidPhone(string phone)
    {
        var digits = Regex.Replace(phone, @"[^\d]", "");
        return digits.Length >= 7 && digits.Length <= 15;
    }

    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var baseSlug = Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');

        if (!await _db.Affiliates.AnyAsync(a => a.Slug == baseSlug))
            return baseSlug;

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!await _db.Affiliates.AnyAsync(a => a.Slug == candidate))
                return candidate;
        }
    }
}
