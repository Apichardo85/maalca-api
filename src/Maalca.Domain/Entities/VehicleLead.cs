using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Un interesado en un vehículo de un negocio Dealer: lo que el cliente final deja desde la vitrina pública
/// (financiamiento, "vendo mi carro", prueba de manejo o una consulta). Es un lead del cliente final hacia un
/// afiliado -- ProspectIntake NO sirve para esto (es de plataforma, sin AffiliateId).
///
/// Kind: "financing" | "tradein" | "inquiry" | "testdrive". Status: "new" | "contacted" | "won" | "lost".
/// VehicleSummary es una foto del vehículo al momento del lead ("2022 Toyota RAV4 XLE"): si el dueño borra el
/// vehículo, el lead sigue siendo legible (VehicleListingId pasa a null, no se borra el lead).
/// Los montos (DownPayment, MonthlyEstimate) van en la moneda base del negocio (Currency).
/// La cuota la calcula el SERVIDOR con la tasa configurada, no se confía en el número del navegador.
/// </summary>
public class VehicleLead : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid? VehicleListingId { get; set; }
    public string? VehicleSummary { get; set; }

    public string Kind { get; set; } = "inquiry";

    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }

    // financing
    public string? Currency { get; set; }
    public decimal? DownPayment { get; set; }
    public int? TermMonths { get; set; }
    public decimal? MonthlyEstimate { get; set; }

    // tradein
    public string? TradeMake { get; set; }
    public string? TradeModel { get; set; }
    public int? TradeYear { get; set; }
    public int? TradeMileage { get; set; }
    public string? TradeMileageUnit { get; set; }
    /// <summary>"excellent" | "good" | "fair" | "poor".</summary>
    public string? TradeCondition { get; set; }
    /// <summary>JSON array de URLs (máx. 6).</summary>
    public string? TradeImages { get; set; }

    // testdrive
    public DateTime? PreferredAt { get; set; }

    public string Status { get; set; } = "new";
    public string? InternalNotes { get; set; }
    public string? Source { get; set; }

    public Affiliate? Affiliate { get; set; }
    public VehicleListing? VehicleListing { get; set; }
}
