using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Common;

/// <summary>Módulo que le corresponde al negocio por su rubro pero que su plan todavía no incluye.</summary>
public record LockedModuleDto(string Token, string RequiredTier);

/// <summary>
/// La ÚNICA regla que decide qué módulos ve un negocio:
///
///   activos = (prendidos para su rubro — ModuleCatalog.FilterActive — ∩ permitidos por su nivel)
///             ∪ cortesías de ops (Affiliate.ModulosCortesia)
///
/// Antes había tres reglas sueltas que no se hablaban (FilterActive sin plan, PlanLimitService con
/// un trial que nunca bloqueaba nada, y BuildCapabilities que miraba solo el enum Plan): un trial
/// vencido conservaba todos los módulos y un trial vigente no podía usar la pantalla.
///
/// Cortesía "*" = el negocio conserva todo lo que tiene prendido, sin mirar el plan (así se
/// protegió a los clientes existentes en la migración ModulePlanRulesAndCortesia).
///
/// Estático a propósito: lo usan servicios que ya tienen AppDbContext, sin tocar su DI. Las reglas
/// (ModulePlanRules) se cachean 10 min y se invalidan al guardar desde /ops/modulos.
/// </summary>
public static class ModuleAccess
{
    public const int TrialDays = 30;
    public const string CourtesyAll = "*";
    private static readonly TimeSpan RulesTtl = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim RulesLock = new(1, 1);
    private static IReadOnlyDictionary<string, PlanTier>? _rules;
    private static DateTime _rulesAt;

    /// <summary>¿Venció el trial de un Starter? (Planes pagados nunca "vencen" aquí.)</summary>
    public static bool IsTrialExpired(Affiliate a)
    {
        if (a.Plan != Plan.Free) return false;
        if (a.TrialOverrideEndsAt.HasValue) return a.TrialOverrideEndsAt.Value < DateTime.UtcNow;
        return a.CreatedAt.AddDays(TrialDays) < DateTime.UtcNow;
    }

    public static DateTime? TrialEndsAt(Affiliate a) =>
        a.Plan != Plan.Free ? null : a.TrialOverrideEndsAt ?? a.CreatedAt.AddDays(TrialDays);

    /// <summary>Starter con trial vigente = Professional (el trial se siente como el plan completo).</summary>
    public static PlanTier GetEffectiveTier(Affiliate a) => a.Plan switch
    {
        Plan.Enterprise => PlanTier.Professional,
        Plan.Entrepreneur => PlanTier.Growth,
        _ => IsTrialExpired(a) ? PlanTier.Starter : PlanTier.Professional,
    };

    public static string PublicTierName(PlanTier tier) => tier.ToString(); // Starter · Growth · Professional

    public static string[] ParseCourtesy(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t == CourtesyAll || ModuleCatalog.Whitelist.Contains(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    public static async Task<IReadOnlyDictionary<string, PlanTier>> GetRulesAsync(AppDbContext db)
    {
        var cached = _rules;
        if (cached is not null && DateTime.UtcNow - _rulesAt < RulesTtl) return cached;

        await RulesLock.WaitAsync();
        try
        {
            if (_rules is not null && DateTime.UtcNow - _rulesAt < RulesTtl) return _rules;

            var stored = await db.ModulePlanRules.AsNoTracking().ToListAsync();
            var dict = ModuleCatalog.Modules.ToDictionary(m => m.Token, m => m.DefaultMinTier, StringComparer.OrdinalIgnoreCase);
            foreach (var r in stored)
                if (dict.ContainsKey(r.Token)) dict[r.Token] = r.MinTier;

            _rules = dict;
            _rulesAt = DateTime.UtcNow;
            return dict;
        }
        finally
        {
            RulesLock.Release();
        }
    }

    public static void InvalidateRules() => _rules = null;

    public static async Task<string[]> GetActiveModulesAsync(AppDbContext db, Affiliate a)
    {
        var rules = await GetRulesAsync(db);
        return ComputeActive(a, rules);
    }

    /// <summary>Versión sin base de datos, con las reglas ya cargadas (para listas de negocios).</summary>
    public static string[] ComputeActive(Affiliate a, IReadOnlyDictionary<string, PlanTier> rules)
    {
        var bt = a.BusinessType.ToString();
        var selected = ModuleCatalog.FilterActive(a.ModulosActivos, bt);
        var courtesy = ParseCourtesy(a.ModulosCortesia);

        if (courtesy.Contains(CourtesyAll)) return selected;

        var tier = GetEffectiveTier(a);
        var byPlan = selected.Where(t => tier >= rules.GetValueOrDefault(t, PlanTier.Growth));
        return byPlan.Concat(courtesy).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Prendidos para su rubro pero bloqueados por el plan: la vitrina muestra el candado.</summary>
    public static async Task<IReadOnlyList<LockedModuleDto>> GetLockedModulesAsync(AppDbContext db, Affiliate a)
    {
        var rules = await GetRulesAsync(db);
        var active = ComputeActive(a, rules).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ModuleCatalog.FilterActive(a.ModulosActivos, a.BusinessType.ToString())
            .Where(t => !active.Contains(t))
            .Select(t => new LockedModuleDto(t, PublicTierName(rules.GetValueOrDefault(t, PlanTier.Growth))))
            .ToList();
    }

    /// <summary>
    /// Pantallas incluidas: trial vigente 1 · Starter vencido 0 (salvo cortesía de pantalla) ·
    /// Growth 1 · Professional 2. Las extra pagadas (ExtraScreens) se suman aparte.
    /// </summary>
    public static int GetIncludedScreens(Affiliate a)
    {
        if (a.Plan == Plan.Enterprise) return 2;
        if (a.Plan == Plan.Entrepreneur) return 1;
        if (!IsTrialExpired(a)) return 1;
        var courtesy = ParseCourtesy(a.ModulosCortesia);
        return courtesy.Contains(CourtesyAll) || courtesy.Contains("board", StringComparer.OrdinalIgnoreCase) ? 1 : 0;
    }
}
