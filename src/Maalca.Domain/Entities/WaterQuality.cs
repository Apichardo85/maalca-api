using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Análisis de laboratorio del agua (o del hielo) de la fábrica. Alimenta el historial de calidad y la
/// página pública "Calidad". ParametersJson = lista de { name, value, unit, limitMin, limitMax, conforms }.
/// "Borrar" = IsActive false (queda en el historial interno).
/// </summary>
public class WaterQualityTest : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public DateOnly TestDate { get; set; }
    public string LabName { get; set; } = string.Empty;
    /// <summary>fisicoquimico | microbiologico | mixto.</summary>
    public string TestType { get; set; } = "fisicoquimico";
    /// <summary>Qué se analizó: lote, tanque o punto de muestra (ej. "Agua tratada – salida de ósmosis").</summary>
    public string? Sample { get; set; }
    /// <summary>conforme | no_conforme | observacion.</summary>
    public string Result { get; set; } = "conforme";
    /// <summary>JSON: lista de parámetros medidos.</summary>
    public string? ParametersJson { get; set; }
    /// <summary>URL del certificado (PDF o foto) subido.</summary>
    public string? CertificateUrl { get; set; }
    public string? Notes { get; set; }
    /// <summary>Si se muestra en la página pública de calidad.</summary>
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public Affiliate? Affiliate { get; set; }
}

/// <summary>Bitácora de mantenimiento del sistema de tratamiento (filtros, membrana, tanques, desinfección).</summary>
public class WaterMaintenanceLog : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public DateOnly LogDate { get; set; }
    /// <summary>cambio_filtro | cambio_membrana | limpieza_tanque | desinfeccion | calibracion | otro.</summary>
    public string Kind { get; set; } = "otro";
    public string Description { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public Affiliate? Affiliate { get; set; }
}
