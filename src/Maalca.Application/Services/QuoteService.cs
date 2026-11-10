using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Maalca.Application.Services;

/// <summary>
/// Cotizador por módulos y por nivel de país. Lee la configuración de /ops/precios (pesos, niveles,
/// países, ajustes, extras) y llama a la función pura PricingEngine.Price. Siempre en USD.
/// </summary>
public class QuoteService : IQuoteService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static int _version; // sube con cada edición desde /ops para invalidar el libro público

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IPlatformAddOnService _addOns;

    public QuoteService(AppDbContext db, IMemoryCache cache, IPlatformAddOnService addOns)
    {
        _db = db;
        _cache = cache;
        _addOns = addOns;
    }

    // ── Contexto de configuración ────────────────────────────────────────────

    private sealed record PricingContext(
        PricingSettings Settings,
        IReadOnlyDictionary<string, PlanTier> Rules,
        IReadOnlyDictionary<string, decimal> Weights,
        IReadOnlyDictionary<PlanTier, decimal> TierSums,
        IReadOnlyDictionary<string, decimal> UnitPrices,
        IReadOnlyList<PriceTier> Tiers,
        IReadOnlyDictionary<string, int> Countries);

    private async Task<PricingContext> LoadAsync()
    {
        var settings = await _db.PricingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1) ?? new PricingSettings();
        var rules = await ModuleAccess.GetRulesAsync(_db);
        var weights = (await _db.ModulePricings.AsNoTracking().ToListAsync())
            .ToDictionary(w => w.Token, w => w.Weight, StringComparer.OrdinalIgnoreCase);

        var sums = new Dictionary<PlanTier, decimal>();
        foreach (var m in ModuleCatalog.Modules)
        {
            var tier = rules.GetValueOrDefault(m.Token, m.DefaultMinTier);
            if (tier == PlanTier.Starter) continue;
            sums[tier] = sums.GetValueOrDefault(tier) + weights.GetValueOrDefault(m.Token, 1m);
        }

        var unitKeys = PricingEngine.DefaultUnitPrices.Keys.ToList();
        var units = (await _db.PlatformAddOns.AsNoTracking().Where(a => unitKeys.Contains(a.Key)).ToListAsync())
            .ToDictionary(a => a.Key, a => a.Price);

        var tiers = await _db.PriceTiers.AsNoTracking().OrderBy(t => t.Tier).ToListAsync();
        if (tiers.Count == 0) tiers.Add(new PriceTier { Tier = 1, Name = "Nivel 1", Factor = 1m, RoundTo = 0.01m });

        var countries = (await _db.CountryTiers.AsNoTracking().ToListAsync())
            .ToDictionary(c => c.Country.ToUpperInvariant(), c => c.Tier);

        return new PricingContext(settings, rules, weights, sums, units, tiers, countries);
    }

    private static PriceTier TierFor(PricingContext ctx, string? country)
    {
        var tier = 1;
        if (!string.IsNullOrWhiteSpace(country) && ctx.Countries.TryGetValue(country.Trim().ToUpperInvariant(), out var t))
            tier = t;
        return ctx.Tiers.FirstOrDefault(x => x.Tier == tier) ?? ctx.Tiers.First();
    }

    private static string Label(string token)
    {
        var m = ModuleCatalog.Get(token);
        return m?.Es ?? token;
    }

    private static List<PricingModuleInput> BuildModules(PricingContext ctx, IEnumerable<string> tokens, ISet<string> courtesy, bool courtesyAll)
    {
        var result = new List<PricingModuleInput>();
        foreach (var token in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var def = ModuleCatalog.Get(token);
            if (def is null) continue;
            var tier = ctx.Rules.GetValueOrDefault(def.Token, def.DefaultMinTier);
            result.Add(new PricingModuleInput(
                def.Token, def.Es, tier,
                ctx.Weights.GetValueOrDefault(def.Token, 1m),
                courtesyAll || courtesy.Contains(def.Token)));
        }
        return result;
    }

    private static PricingInput ToInput(
        PricingContext ctx, List<PricingModuleInput> modules, PriceTier tier, string? country,
        bool corporate, int locations, int vehicles, int users, int contractClients, int importClients) =>
        new(modules, ctx.TierSums, ctx.Settings, ctx.UnitPrices, corporate, locations, vehicles, users,
            contractClients, importClients, country, tier.Tier, tier.Name, tier.Factor, tier.RoundTo);

    // ── Cotizaciones ─────────────────────────────────────────────────────────

    public async Task<QuoteResultDto> ComputeForAffiliateAsync(Guid affiliateId)
    {
        var a = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == affiliateId)
            ?? throw new KeyNotFoundException("Ese negocio no existe.");
        var ctx = await LoadAsync();

        var selected = ModuleCatalog.FilterActive(a.ModulosActivos, a.BusinessType.ToString());
        var courtesyList = ModuleAccess.ParseCourtesy(a.ModulosCortesia);
        var courtesyAll = courtesyList.Contains(ModuleAccess.CourtesyAll);
        var courtesy = courtesyList.Where(t => t != ModuleAccess.CourtesyAll).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modules = BuildModules(ctx, selected.Concat(courtesy), courtesy, courtesyAll);

        var vehicles = await _db.Vehicles.CountAsync(v => v.AffiliateId == affiliateId && v.IsActive);
        var users = await _db.TeamMembers.CountAsync(t => t.AffiliateId == affiliateId && t.IsActive);
        var clients = await _db.Contracts.Where(c => c.AffiliateId == affiliateId && c.IsActive)
            .Select(c => c.CustomerId).Distinct().CountAsync();

        var tier = TierFor(ctx, a.Country);
        var input = ToInput(ctx, modules, tier, a.Country, a.IsCorporate, Math.Max(1, a.Locations),
            vehicles, users, clients, clients);
        return PricingEngine.Price(input);
    }

    public async Task<QuoteResultDto> ComputeForSelectionAsync(QuoteSelectionRequest request)
    {
        var ctx = await LoadAsync();
        var modules = BuildModules(ctx, request.Modules ?? Array.Empty<string>(), new HashSet<string>(), false);
        var tier = TierFor(ctx, request.Country);
        var input = ToInput(ctx, modules, tier, request.Country, request.IsCorporate,
            Math.Clamp(request.Locations, 1, 50), Math.Max(0, request.Vehicles), Math.Max(0, request.Users),
            Math.Max(0, request.ContractClients), Math.Max(0, request.ImportClients));
        return PricingEngine.Price(input);
    }

    public async Task<int> GetTierForCountryAsync(string? country)
    {
        var ctx = await LoadAsync();
        return TierFor(ctx, country).Tier;
    }

    // ── Público: /servicios y botón Mejorar ──────────────────────────────────

    public async Task<PublicPriceBookDto> GetPublicPriceBookAsync(string? country)
    {
        var key = $"pricing:book:{_version}:{(country ?? "").Trim().ToUpperInvariant()}";
        if (_cache.TryGetValue(key, out PublicPriceBookDto? cached) && cached is not null) return cached;

        var ctx = await LoadAsync();
        var tier = TierFor(ctx, country);
        var s = ctx.Settings;
        var fine = Math.Min(tier.RoundTo <= 0 ? 1m : tier.RoundTo, 1m); // módulos y extras: redondeo a $1 como mucho

        decimal Tiered(decimal list) => PricingEngine.ApplyTier(list, tier.Factor, tier.RoundTo);

        var plans = new List<PublicPlanPriceDto>
        {
            new("inicial", "Starter", 0m, 0m, 0m, 0m, 0m),
            new("emprendedor", "Growth", Tiered(s.GrowthUsd), s.GrowthUsd, Tiered(s.GrowthUsd * 10m), Tiered(s.SetupBaseUsd), s.SetupBaseUsd),
            new("profesional", "Professional", Tiered(s.ProfessionalUsd), s.ProfessionalUsd, Tiered(s.ProfessionalUsd * 10m), Tiered(s.SetupBaseUsd), s.SetupBaseUsd),
        };

        var modules = new List<PublicModulePriceDto>();
        foreach (var m in ModuleCatalog.Modules)
        {
            var t = ctx.Rules.GetValueOrDefault(m.Token, m.DefaultMinTier);
            if (t == PlanTier.Starter) continue;
            var solo = PricingEngine.Solo(s, ctx.TierSums, t, ctx.Weights.GetValueOrDefault(m.Token, 1m));
            modules.Add(new PublicModulePriceDto(
                m.Token, new LocalizedTextDto(m.Es, m.En), new LocalizedTextDto(m.DescEs, m.DescEn),
                t.ToString(), PricingEngine.ApplyTier(solo, tier.Factor, fine), solo));
        }

        // Extras (sin las claves internas de unidades): con el nivel, salvo los que no lo aplican (hardware)
        var discountable = (await _db.PlatformAddOns.AsNoTracking().ToListAsync())
            .ToDictionary(a => a.Key, a => a.ApplyTierDiscount);
        var addOns = (await _addOns.GetPublicAsync()).Select(a =>
        {
            if (!discountable.GetValueOrDefault(a.Key, true) || tier.Factor == 1m) return a;
            return a with
            {
                Price = PricingEngine.ApplyTier(a.Price, tier.Factor, fine),
                VolumePrice = a.VolumePrice is null ? null : PricingEngine.ApplyTier(a.VolumePrice.Value, tier.Factor, fine),
            };
        }).ToList();

        decimal U(string k) => ctx.UnitPrices.TryGetValue(k, out var v) ? v : PricingEngine.DefaultUnitPrices[k];
        var book = new PublicPriceBookDto(
            (country ?? "").Trim().ToUpperInvariant(), tier.Tier, tier.Name, tier.Factor,
            plans, modules, addOns,
            Tiered(s.CorporateMinUsd), s.CorporateMinUsd,
            PricingEngine.ApplyTier(U(PricingEngine.KeyVehicle), tier.Factor, fine),
            PricingEngine.ApplyTier(U(PricingEngine.KeyUser), tier.Factor, fine),
            PricingEngine.ApplyTier(U(PricingEngine.KeyClients100), tier.Factor, fine),
            Math.Round(s.ExtraLocationFactor * 100m, 0));

        _cache.Set(key, book, CacheTtl);
        return book;
    }

    public async Task<UpgradePriceDto> GetUpgradePriceAsync(Guid affiliateId)
    {
        var a = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == affiliateId)
            ?? throw new KeyNotFoundException("Ese negocio no existe.");
        var book = await GetPublicPriceBookAsync(a.Country);
        var growth = book.Plans.First(x => x.Id == "emprendedor");
        var pro = book.Plans.First(x => x.Id == "profesional");
        return new UpgradePriceDto(book.Country, book.Tier, growth.MonthlyUsd, pro.MonthlyUsd, growth.ListUsd, pro.ListUsd);
    }

    // ── /ops/precios ─────────────────────────────────────────────────────────

    public async Task<PricingConfigDto> GetConfigAsync()
    {
        var ctx = await LoadAsync();
        var s = ctx.Settings;
        var modules = new List<PricingModuleDto>();
        foreach (var m in ModuleCatalog.Modules.Where(d => ctx.Rules.GetValueOrDefault(d.Token, d.DefaultMinTier) != PlanTier.Starter))
        {
            var t = ctx.Rules.GetValueOrDefault(m.Token, m.DefaultMinTier);
            var w = ctx.Weights.GetValueOrDefault(m.Token, 1m);
            modules.Add(new PricingModuleDto(
                m.Token, m.Es, m.En, t.ToString(), w,
                PricingEngine.R2(PricingEngine.Share(s, ctx.TierSums, t, w)),
                PricingEngine.Solo(s, ctx.TierSums, t, w),
                PricingEngine.SetupOf(s, t, w)));
        }
        modules = modules.OrderBy(m => m.Tier).ThenBy(m => m.Token).ToList();

        return new PricingConfigDto(
            modules,
            ctx.Tiers.Select(t => new PricingTierDto(t.Tier, t.Name, t.Factor, t.RoundTo)).ToList(),
            ctx.Countries.OrderBy(c => c.Key).Select(c => new PricingCountryDto(c.Key, c.Value)).ToList(),
            new PricingSettingsDto(s.GrowthUsd, s.ProfessionalUsd, s.SoloMarkup, s.CorporateMinUsd, s.SetupBaseUsd,
                s.SetupGrowthPerWeightUsd, s.SetupProPerWeightUsd, s.IncludedUsers, s.IncludedClients, s.ExtraLocationFactor));
    }

    public async Task UpdateModuleWeightsAsync(UpdateModuleWeightsRequest request, string? adminEmail)
    {
        foreach (var item in request.Items)
        {
            if (!ModuleCatalog.Whitelist.Contains(item.Token))
                throw new ArgumentException($"El módulo \"{item.Token}\" no existe.");
            if (item.Weight < 0m || item.Weight > 20m)
                throw new ArgumentException("El peso debe estar entre 0 y 20.");
        }

        foreach (var item in request.Items)
        {
            var row = await _db.ModulePricings.FirstOrDefaultAsync(r => r.Token == item.Token);
            if (row is null)
            {
                row = new ModulePricing { Token = item.Token };
                _db.ModulePricings.Add(row);
            }
            row.Weight = decimal.Round(item.Weight, 2);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = adminEmail;
        }
        await _db.SaveChangesAsync();
        Invalidate();
    }

    public async Task UpdateTiersAsync(UpdateTiersRequest request, string? adminEmail)
    {
        if (request.Tiers.Count == 0 || request.Tiers.All(t => t.Tier != 1))
            throw new ArgumentException("Tiene que existir el Nivel 1 (precio de lista).");
        foreach (var t in request.Tiers)
        {
            if (t.Tier < 1 || t.Tier > 9) throw new ArgumentException("El nivel debe estar entre 1 y 9.");
            if (t.Factor <= 0m || t.Factor > 3m) throw new ArgumentException("El factor debe estar entre 0 y 3.");
            if (t.RoundTo < 0m || t.RoundTo > 100m) throw new ArgumentException("El redondeo debe estar entre 0 y 100.");
            if (string.IsNullOrWhiteSpace(t.Name)) throw new ArgumentException("Cada nivel necesita nombre.");
        }

        var existing = await _db.PriceTiers.ToListAsync();
        foreach (var t in request.Tiers)
        {
            var row = existing.FirstOrDefault(e => e.Tier == t.Tier);
            if (row is null)
            {
                row = new PriceTier { Tier = t.Tier };
                _db.PriceTiers.Add(row);
            }
            row.Name = t.Name.Trim();
            row.Factor = decimal.Round(t.Factor, 3);
            row.RoundTo = decimal.Round(t.RoundTo, 2);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = adminEmail;
        }
        var keep = request.Tiers.Select(t => t.Tier).ToHashSet();
        var removed = existing.Where(e => !keep.Contains(e.Tier)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(r => r.Tier).ToList();
            if (await _db.CountryTiers.AnyAsync(c => removedIds.Contains(c.Tier)))
                throw new ArgumentException("Hay países asignados a un nivel que quieres borrar. Muévelos antes.");
            _db.PriceTiers.RemoveRange(removed);
        }
        await _db.SaveChangesAsync();
        Invalidate();
    }

    public async Task UpdateCountriesAsync(UpdateCountriesRequest request, string? adminEmail)
    {
        var tiers = await _db.PriceTiers.Select(t => t.Tier).ToListAsync();
        foreach (var c in request.Countries)
        {
            if (string.IsNullOrWhiteSpace(c.Country) || c.Country.Trim().Length != 2)
                throw new ArgumentException("El país va en 2 letras (ej. DO, US).");
            if (!tiers.Contains(c.Tier))
                throw new ArgumentException($"El nivel {c.Tier} no existe.");
        }

        var wanted = request.Countries
            .GroupBy(c => c.Country.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Last().Tier);
        var existing = await _db.CountryTiers.ToListAsync();
        foreach (var kv in wanted)
        {
            var row = existing.FirstOrDefault(e => e.Country == kv.Key);
            if (row is null)
            {
                row = new CountryTier { Country = kv.Key };
                _db.CountryTiers.Add(row);
            }
            row.Tier = kv.Value;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = adminEmail;
        }
        _db.CountryTiers.RemoveRange(existing.Where(e => !wanted.ContainsKey(e.Country)));
        await _db.SaveChangesAsync();
        Invalidate();
    }

    public async Task UpdateSettingsAsync(PricingSettingsDto r, string? adminEmail)
    {
        if (r.GrowthUsd < 0 || r.ProfessionalUsd < r.GrowthUsd)
            throw new ArgumentException("Professional no puede costar menos que Growth.");
        if (r.SoloMarkup < 1m || r.SoloMarkup > 5m)
            throw new ArgumentException("El multiplicador del módulo suelto va entre 1 y 5.");
        if (r.ExtraLocationFactor < 0m || r.ExtraLocationFactor > 2m)
            throw new ArgumentException("El % del local adicional va entre 0 y 2 (0.5 = 50%).");
        if (r.IncludedUsers < 0 || r.IncludedClients < 0)
            throw new ArgumentException("Los incluidos no pueden ser negativos.");

        var row = await _db.PricingSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (row is null)
        {
            row = new PricingSettings { Id = 1 };
            _db.PricingSettings.Add(row);
        }
        row.GrowthUsd = r.GrowthUsd;
        row.ProfessionalUsd = r.ProfessionalUsd;
        row.SoloMarkup = r.SoloMarkup;
        row.CorporateMinUsd = r.CorporateMinUsd;
        row.SetupBaseUsd = r.SetupBaseUsd;
        row.SetupGrowthPerWeightUsd = r.SetupGrowthPerWeightUsd;
        row.SetupProPerWeightUsd = r.SetupProPerWeightUsd;
        row.IncludedUsers = r.IncludedUsers;
        row.IncludedClients = r.IncludedClients;
        row.ExtraLocationFactor = r.ExtraLocationFactor;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = adminEmail;
        await _db.SaveChangesAsync();
        Invalidate();
    }

    public async Task<bool> SetCorporateAsync(Guid affiliateId, SetCorporateRequest request)
    {
        var a = await _db.Affiliates.FirstOrDefaultAsync(x => x.Id == affiliateId);
        if (a is null) return false;
        a.IsCorporate = request.IsCorporate;
        a.Locations = Math.Clamp(request.Locations, 1, 50);
        await _db.SaveChangesAsync();
        return true;
    }

    private static void Invalidate() => Interlocked.Increment(ref _version);
}
