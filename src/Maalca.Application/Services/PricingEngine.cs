using Maalca.Application.Common.DTOs;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;

namespace Maalca.Application.Services;

public record PricingModuleInput(string Token, string Label, PlanTier Tier, decimal Weight, bool Courtesy);

/// <summary>Todo lo que la fórmula necesita; no toca la base de datos.</summary>
public record PricingInput(
    IReadOnlyList<PricingModuleInput> Modules,
    IReadOnlyDictionary<PlanTier, decimal> TierWeightSums,
    PricingSettings Settings,
    IReadOnlyDictionary<string, decimal> UnitPrices,
    bool IsCorporate,
    int Locations,
    int Vehicles,
    int Users,
    int ContractClients,
    int ImportClients,
    string? Country,
    int Tier,
    string TierName,
    decimal Factor,
    decimal RoundTo);

/// <summary>
/// La fórmula de precios de MaalCa, en una sola función pura (fácil de probar):
///   mensualidad = min(módulos sueltos, paquete del plan) + locales + unidades + soporte, con mínimo corporativo,
///   y al final el nivel de país (factor + redondeo). Todo en USD.
/// Ver docs/billing/cotizador-por-modulos.md.
/// </summary>
public static class PricingEngine
{
    public const string KeyVehicle = "unit-vehicle";
    public const string KeyUser = "unit-user";
    public const string KeyClients100 = "unit-clients-100";
    public const string KeySupport = "corp-support";
    public const string KeyImport100 = "data-import-100";

    public static readonly IReadOnlyDictionary<string, decimal> DefaultUnitPrices = new Dictionary<string, decimal>
    {
        [KeyVehicle] = 10m,
        [KeyUser] = 5m,
        [KeyClients100] = 10m,
        [KeySupport] = 150m,
        [KeyImport100] = 25m,
    };

    public static decimal Bag(PricingSettings s, PlanTier tier) => tier switch
    {
        PlanTier.Growth => s.GrowthUsd,
        PlanTier.Professional => Math.Max(0m, s.ProfessionalUsd - s.GrowthUsd),
        _ => 0m,
    };

    /// <summary>Parte de la bolsa de su plan que le toca al módulo, según su peso.</summary>
    public static decimal Share(PricingSettings s, IReadOnlyDictionary<PlanTier, decimal> sums, PlanTier tier, decimal weight)
    {
        var sum = sums.GetValueOrDefault(tier);
        return sum <= 0 ? 0m : Bag(s, tier) * weight / sum;
    }

    public static decimal Solo(PricingSettings s, IReadOnlyDictionary<PlanTier, decimal> sums, PlanTier tier, decimal weight) =>
        R2(Share(s, sums, tier, weight) * s.SoloMarkup);

    public static decimal SetupOf(PricingSettings s, PlanTier tier, decimal weight) => tier switch
    {
        PlanTier.Growth => R2(weight * s.SetupGrowthPerWeightUsd),
        PlanTier.Professional => R2(weight * s.SetupProPerWeightUsd),
        _ => 0m,
    };

    public static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Aplica el factor del nivel y redondea al múltiplo de RoundTo más cercano.</summary>
    public static decimal ApplyTier(decimal listUsd, decimal factor, decimal roundTo)
    {
        if (listUsd <= 0m) return 0m;
        var v = listUsd * factor;
        if (roundTo <= 0m) return R2(v);
        return Math.Round(v / roundTo, 0, MidpointRounding.AwayFromZero) * roundTo;
    }

    private static decimal Unit(IReadOnlyDictionary<string, decimal> prices, string key) =>
        prices.TryGetValue(key, out var v) ? v : DefaultUnitPrices[key];

    public static QuoteResultDto Price(PricingInput i)
    {
        var s = i.Settings;
        var monthly = new List<QuoteLineDto>();

        // 1) Módulos sueltos
        var paid = new List<PricingModuleInput>();
        decimal solos = 0m;
        foreach (var m in i.Modules.Where(m => m.Tier != PlanTier.Starter).OrderBy(m => m.Tier).ThenBy(m => m.Token))
        {
            if (m.Courtesy)
            {
                monthly.Add(new QuoteLineDto("modules", m.Token, m.Label, 1, 0m, 0m, true));
                continue;
            }
            var solo = Solo(s, i.TierWeightSums, m.Tier, m.Weight);
            solos += solo;
            paid.Add(m);
            monthly.Add(new QuoteLineDto("modules", m.Token, m.Label, 1, solo, solo, false));
        }

        // 2) Paquete: gana si sale más barato que los módulos sueltos
        string? pkgName = null;
        decimal savings = 0m;
        var baseUsd = solos;
        if (paid.Count > 0)
        {
            var pkgTier = paid.Any(m => m.Tier == PlanTier.Professional) ? PlanTier.Professional : PlanTier.Growth;
            var pkgPrice = pkgTier == PlanTier.Professional ? s.ProfessionalUsd : s.GrowthUsd;
            if (pkgPrice < solos)
            {
                pkgName = pkgTier.ToString();
                savings = R2(solos - pkgPrice);
                baseUsd = pkgPrice;
                monthly.Add(new QuoteLineDto("package", "package", $"Ahorro por paquete {pkgName}", 1, -savings, -savings, false));
            }
        }

        // 3) Locales adicionales (el primero va en la base)
        var extraLocations = Math.Max(0, i.Locations - 1);
        if (extraLocations > 0 && baseUsd > 0m)
        {
            var each = R2(baseUsd * s.ExtraLocationFactor);
            monthly.Add(new QuoteLineDto("units", "unit-location", "Local adicional", extraLocations, each, each * extraLocations, false));
        }

        // 4) Unidades reales
        if (i.Vehicles > 0)
        {
            var u = Unit(i.UnitPrices, KeyVehicle);
            monthly.Add(new QuoteLineDto("units", KeyVehicle, "Vehículo de reparto", i.Vehicles, u, u * i.Vehicles, false));
        }
        var extraUsers = Math.Max(0, i.Users - s.IncludedUsers);
        if (extraUsers > 0)
        {
            var u = Unit(i.UnitPrices, KeyUser);
            monthly.Add(new QuoteLineDto("units", KeyUser, $"Usuarios sobre {s.IncludedUsers}", extraUsers, u, u * extraUsers, false));
        }
        var clientBlocks = i.ContractClients > s.IncludedClients
            ? (int)Math.Ceiling((i.ContractClients - s.IncludedClients) / 100m) : 0;
        if (clientBlocks > 0)
        {
            var u = Unit(i.UnitPrices, KeyClients100);
            monthly.Add(new QuoteLineDto("units", KeyClients100, $"Clientes con contrato sobre {s.IncludedClients} (bloques de 100)", clientBlocks, u, u * clientBlocks, false));
        }

        // 5) Soporte corporativo
        if (i.IsCorporate)
        {
            var u = Unit(i.UnitPrices, KeySupport);
            monthly.Add(new QuoteLineDto("support", KeySupport, "Soporte corporativo", 1, u, u, false));
        }

        var monthlyList = monthly.Sum(l => l.TotalUsd);

        // 6) Mínimo corporativo
        if (i.IsCorporate && monthlyList < s.CorporateMinUsd)
        {
            var adj = R2(s.CorporateMinUsd - monthlyList);
            monthly.Add(new QuoteLineDto("minimum", "corp-min", "Ajuste a mínimo corporativo", 1, adj, adj, false));
            monthlyList = s.CorporateMinUsd;
        }

        monthlyList = R2(monthlyList);
        var monthlyFinal = ApplyTier(monthlyList, i.Factor, i.RoundTo);
        if (monthlyFinal != monthlyList)
            monthly.Add(TierLine(i, monthlyFinal - monthlyList));

        // 7) Implementación (pago único)
        var setup = new List<QuoteLineDto>
        {
            new("setup", "setup-base", "Implementación base: levantamiento, configuración y capacitación", 1, s.SetupBaseUsd, s.SetupBaseUsd, false),
        };
        decimal modulesSetup = 0m;
        foreach (var m in paid)
        {
            var v = SetupOf(s, m.Tier, m.Weight);
            modulesSetup += v;
            setup.Add(new QuoteLineDto("setup", m.Token, m.Label, 1, v, v, false));
        }
        if (extraLocations > 0 && modulesSetup > 0m)
        {
            var each = R2(modulesSetup * s.ExtraLocationFactor);
            setup.Add(new QuoteLineDto("setup", "unit-location", "Configuración de local adicional", extraLocations, each, each * extraLocations, false));
        }
        var importBlocks = (int)Math.Ceiling(Math.Max(0, i.ImportClients) / 100m);
        if (importBlocks > 0)
        {
            var u = Unit(i.UnitPrices, KeyImport100);
            setup.Add(new QuoteLineDto("import", KeyImport100, "Importación de clientes (bloques de 100)", importBlocks, u, u * importBlocks, false));
        }
        var setupList = R2(setup.Sum(l => l.TotalUsd));
        var setupFinal = ApplyTier(setupList, i.Factor, i.RoundTo);
        if (setupFinal != setupList)
            setup.Add(TierLine(i, setupFinal - setupList));

        return new QuoteResultDto(
            i.Country, i.Tier, i.TierName, i.Factor, i.IsCorporate, Math.Max(1, i.Locations),
            pkgName, savings, monthly, monthlyList, monthlyFinal, setup, setupList, setupFinal);
    }

    private static QuoteLineDto TierLine(PricingInput i, decimal delta)
    {
        var pct = Math.Round((1m - i.Factor) * 100m, 0);
        var label = pct > 0 ? $"Precio {i.TierName} (−{pct}%)" : "Redondeo";
        return new QuoteLineDto("tier", "tier", label, 1, delta, delta, false);
    }
}
