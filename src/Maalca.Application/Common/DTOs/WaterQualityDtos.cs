namespace Maalca.Application.Common.DTOs;

/// <summary>Un parámetro medido en el análisis. Limit* null = sin límite en ese lado.</summary>
public record WaterParameterDto(string Name, decimal? Value, string? Unit, decimal? LimitMin, decimal? LimitMax, bool? Conforms);

public record WaterQualityTestDto(
    Guid Id, DateOnly TestDate, string LabName, string TestType, string? Sample, string Result,
    IReadOnlyList<WaterParameterDto> Parameters, string? CertificateUrl, string? Notes, bool IsPublic);

public record UpsertWaterQualityTestRequest(
    DateOnly TestDate, string LabName, string TestType, string Result,
    string? Sample = null, List<WaterParameterDto>? Parameters = null,
    string? CertificateUrl = null, string? Notes = null, bool IsPublic = true);

public record WaterMaintenanceDto(Guid Id, DateOnly LogDate, string Kind, string Description, string? PerformedBy, bool IsPublic);

public record UpsertWaterMaintenanceRequest(
    DateOnly LogDate, string Kind, string Description, string? PerformedBy = null, bool IsPublic = true);

/// <summary>Resumen para el panel interno: último análisis, si está al día y cuántos fallaron este año.</summary>
public record WaterQualitySummaryDto(
    DateOnly? LastTestDate, string? LastResult, int? DaysSinceLastTest, bool Overdue, int NonConformingThisYear, int ReminderDays);

/// <summary>Lo que ve el público (sin datos internos): proceso, análisis y mantenimientos marcados como públicos.</summary>
public record PublicWaterQualityDto(
    string BusinessName, string? Logo, string? PrimaryColor,
    IReadOnlyList<ProcessStepDto> ProcessSteps,
    IReadOnlyList<WaterQualityTestDto> Tests,
    IReadOnlyList<WaterMaintenanceDto> Maintenance,
    DateOnly? LastTestDate, string? LastResult);
