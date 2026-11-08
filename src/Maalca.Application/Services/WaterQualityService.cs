using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class WaterQualityService : IWaterQualityService
{
    private static readonly string[] TestTypes = { "fisicoquimico", "microbiologico", "mixto" };
    private static readonly string[] Results = { "conforme", "no_conforme", "observacion" };
    private static readonly string[] Kinds = { "cambio_filtro", "cambio_membrana", "limpieza_tanque", "desinfeccion", "calibracion", "otro" };
    /// <summary>Días sin análisis a partir de los cuales el panel avisa que toca uno nuevo.</summary>
    private const int ReminderDays = 90;

    private readonly AppDbContext _db;

    public WaterQualityService(AppDbContext db) => _db = db;

    public async Task<WaterQualitySummaryDto> GetSummaryAsync(Guid affiliateId)
    {
        var last = await _db.WaterQualityTests.AsNoTracking()
            .Where(t => t.AffiliateId == affiliateId && t.IsActive)
            .OrderByDescending(t => t.TestDate).FirstOrDefaultAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yearStart = new DateOnly(today.Year, 1, 1);
        var bad = await _db.WaterQualityTests.CountAsync(t =>
            t.AffiliateId == affiliateId && t.IsActive && t.Result == "no_conforme" && t.TestDate >= yearStart);
        int? days = last is null ? null : today.DayNumber - last.TestDate.DayNumber;
        return new WaterQualitySummaryDto(last?.TestDate, last?.Result, days, days is null || days > ReminderDays, bad, ReminderDays);
    }

    public async Task<IReadOnlyList<WaterQualityTestDto>> GetTestsAsync(Guid affiliateId, int? year)
    {
        var q = _db.WaterQualityTests.AsNoTracking().Where(t => t.AffiliateId == affiliateId && t.IsActive);
        if (year.HasValue) q = q.Where(t => t.TestDate.Year == year.Value);
        var list = await q.OrderByDescending(t => t.TestDate).ThenByDescending(t => t.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<WaterQualityTestDto> CreateTestAsync(Guid affiliateId, UpsertWaterQualityTestRequest request, string? actorId)
    {
        ValidateTest(request);
        var test = new WaterQualityTest { AffiliateId = affiliateId, CreatedBy = actorId };
        ApplyTest(test, request);
        _db.WaterQualityTests.Add(test);
        await _db.SaveChangesAsync();
        return ToDto(test);
    }

    public async Task<WaterQualityTestDto?> UpdateTestAsync(Guid affiliateId, Guid testId, UpsertWaterQualityTestRequest request, string? actorId)
    {
        ValidateTest(request);
        var test = await _db.WaterQualityTests.FirstOrDefaultAsync(t => t.Id == testId && t.AffiliateId == affiliateId && t.IsActive);
        if (test is null) return null;
        ApplyTest(test, request);
        test.UpdatedAt = DateTime.UtcNow;
        test.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return ToDto(test);
    }

    public async Task<bool> DeleteTestAsync(Guid affiliateId, Guid testId, string? actorId)
    {
        var test = await _db.WaterQualityTests.FirstOrDefaultAsync(t => t.Id == testId && t.AffiliateId == affiliateId && t.IsActive);
        if (test is null) return false;
        test.IsActive = false;
        test.UpdatedAt = DateTime.UtcNow;
        test.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<WaterMaintenanceDto>> GetMaintenanceAsync(Guid affiliateId)
    {
        var list = await _db.WaterMaintenanceLogs.AsNoTracking()
            .Where(m => m.AffiliateId == affiliateId && m.IsActive)
            .OrderByDescending(m => m.LogDate).ThenByDescending(m => m.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<WaterMaintenanceDto> CreateMaintenanceAsync(Guid affiliateId, UpsertWaterMaintenanceRequest request, string? actorId)
    {
        ValidateMaintenance(request);
        var log = new WaterMaintenanceLog { AffiliateId = affiliateId, CreatedBy = actorId };
        ApplyMaintenance(log, request);
        _db.WaterMaintenanceLogs.Add(log);
        await _db.SaveChangesAsync();
        return ToDto(log);
    }

    public async Task<WaterMaintenanceDto?> UpdateMaintenanceAsync(Guid affiliateId, Guid logId, UpsertWaterMaintenanceRequest request, string? actorId)
    {
        ValidateMaintenance(request);
        var log = await _db.WaterMaintenanceLogs.FirstOrDefaultAsync(m => m.Id == logId && m.AffiliateId == affiliateId && m.IsActive);
        if (log is null) return null;
        ApplyMaintenance(log, request);
        log.UpdatedAt = DateTime.UtcNow;
        log.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return ToDto(log);
    }

    public async Task<bool> DeleteMaintenanceAsync(Guid affiliateId, Guid logId, string? actorId)
    {
        var log = await _db.WaterMaintenanceLogs.FirstOrDefaultAsync(m => m.Id == logId && m.AffiliateId == affiliateId && m.IsActive);
        if (log is null) return false;
        log.IsActive = false;
        log.UpdatedAt = DateTime.UtcNow;
        log.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PublicWaterQualityDto?> GetPublicAsync(string slug)
    {
        var affiliate = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.IsActive);
        if (affiliate is null) return null;

        var tests = await _db.WaterQualityTests.AsNoTracking()
            .Where(t => t.AffiliateId == affiliate.Id && t.IsActive && t.IsPublic)
            .OrderByDescending(t => t.TestDate).Take(24).ToListAsync();
        var maintenance = await _db.WaterMaintenanceLogs.AsNoTracking()
            .Where(m => m.AffiliateId == affiliate.Id && m.IsActive && m.IsPublic)
            .OrderByDescending(m => m.LogDate).Take(12).ToListAsync();

        return new PublicWaterQualityDto(
            affiliate.Name, affiliate.Logo, affiliate.PrimaryColor,
            JsonArrayField.Parse<ProcessStepDto>(affiliate.ProcessSteps),
            tests.Select(ToDto).ToList(),
            maintenance.Select(ToDto).ToList(),
            tests.FirstOrDefault()?.TestDate, tests.FirstOrDefault()?.Result);
    }

    private static void ValidateTest(UpsertWaterQualityTestRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.LabName)) throw new ArgumentException("Escribe el nombre del laboratorio.");
        if (r.LabName.Trim().Length > 150) throw new ArgumentException("El nombre del laboratorio es demasiado largo (máximo 150).");
        if (!TestTypes.Contains(r.TestType)) throw new ArgumentException($"Tipo de análisis no válido. Usa: {string.Join(", ", TestTypes)}.");
        if (!Results.Contains(r.Result)) throw new ArgumentException($"Resultado no válido. Usa: {string.Join(", ", Results)}.");
        if (r.TestDate > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) throw new ArgumentException("La fecha del análisis no puede ser futura.");
        if (r.Sample is { Length: > 200 }) throw new ArgumentException("La muestra es demasiado larga (máximo 200).");
        if (r.Parameters is not null)
        {
            if (r.Parameters.Count > 60) throw new ArgumentException("Demasiados parámetros (máximo 60).");
            if (r.Parameters.Any(p => string.IsNullOrWhiteSpace(p.Name))) throw new ArgumentException("Cada parámetro necesita un nombre.");
        }
    }

    private static void ApplyTest(WaterQualityTest t, UpsertWaterQualityTestRequest r)
    {
        t.TestDate = r.TestDate;
        t.LabName = r.LabName.Trim();
        t.TestType = r.TestType;
        t.Result = r.Result;
        t.Sample = string.IsNullOrWhiteSpace(r.Sample) ? null : r.Sample.Trim();
        t.ParametersJson = r.Parameters is { Count: > 0 }
            ? JsonArrayField.Serialize(r.Parameters.Select(p => p with { Name = p.Name.Trim() }))
            : null;
        t.CertificateUrl = string.IsNullOrWhiteSpace(r.CertificateUrl) ? null : r.CertificateUrl.Trim();
        t.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        t.IsPublic = r.IsPublic;
    }

    private static void ValidateMaintenance(UpsertWaterMaintenanceRequest r)
    {
        if (!Kinds.Contains(r.Kind)) throw new ArgumentException($"Tipo de mantenimiento no válido. Usa: {string.Join(", ", Kinds)}.");
        if (string.IsNullOrWhiteSpace(r.Description)) throw new ArgumentException("Escribe qué se hizo.");
        if (r.Description.Trim().Length > 300) throw new ArgumentException("La descripción es demasiado larga (máximo 300).");
        if (r.LogDate > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))) throw new ArgumentException("La fecha no puede ser futura.");
    }

    private static void ApplyMaintenance(WaterMaintenanceLog m, UpsertWaterMaintenanceRequest r)
    {
        m.LogDate = r.LogDate;
        m.Kind = r.Kind;
        m.Description = r.Description.Trim();
        m.PerformedBy = string.IsNullOrWhiteSpace(r.PerformedBy) ? null : r.PerformedBy.Trim();
        m.IsPublic = r.IsPublic;
    }

    private static WaterQualityTestDto ToDto(WaterQualityTest t) => new(
        t.Id, t.TestDate, t.LabName, t.TestType, t.Sample, t.Result,
        JsonArrayField.Parse<WaterParameterDto>(t.ParametersJson), t.CertificateUrl, t.Notes, t.IsPublic);

    private static WaterMaintenanceDto ToDto(WaterMaintenanceLog m) =>
        new(m.Id, m.LogDate, m.Kind, m.Description, m.PerformedBy, m.IsPublic);
}
