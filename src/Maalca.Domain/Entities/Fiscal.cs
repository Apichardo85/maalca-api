using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Datos tributarios de un negocio para emitir comprobantes fiscales electrónicos (e-CF, DGII). Uno por negocio.
/// El certificado digital NO se guarda aquí ni en ningún lado: pasa en memoria al proveedor (Alanube) en el alta de
/// la empresa y solo se conservan emisor y vigencia para avisar antes de que venza.
/// </summary>
public class FiscalProfile : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    /// <summary>RNC (9 dígitos) o cédula (11) del contribuyente, solo dígitos.</summary>
    public string Rnc { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string? Municipality { get; set; }
    public string? Email { get; set; }
    /// <summary>"sandbox" | "production": a qué ambiente del proveedor emite este negocio.</summary>
    public string Environment { get; set; } = "sandbox";
    /// <summary>"draft" (datos guardados) | "registered" (empresa creada en el proveedor) | "active" (puede emitir).</summary>
    public string Status { get; set; } = "draft";
    /// <summary>Indicador de ITBIS (1 = 18%, 2 = 16%, 3 = 0%, 4 = exento) para ventas sin tasa de impuesto.</summary>
    public int DefaultBillingIndicator { get; set; } = 4;
    /// <summary>1 = bien, 2 = servicio. Aplica a todas las líneas hasta que el catálogo lo guarde por producto.</summary>
    public int DefaultGoodService { get; set; } = 1;

    // Alta en el proveedor.
    public string? ProviderCompanyId { get; set; }
    public DateTime? ProviderRegisteredAt { get; set; }
    // URLs que el proveedor devuelve y que el dueño registra en la Oficina Virtual de la DGII.
    public string? ReceptionUrl { get; set; }
    public string? ApprovalUrl { get; set; }
    public string? AuthenticationUrl { get; set; }

    public string? CertificateIssuer { get; set; }
    public DateTime? CertificateStartsAt { get; set; }
    public DateTime? CertificateExpiresAt { get; set; }
}

/// <summary>Rango de e-NCF autorizado por la DGII para un tipo de comprobante (31 crédito fiscal, 32 consumo).</summary>
public class FiscalSequence : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public int DocType { get; set; }
    /// <summary>Próximo número a asignar. Se incrementa con una sola sentencia SQL: dos ventas simultáneas nunca comparten número.</summary>
    public long NextNumber { get; set; }
    public long LastNumber { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Comprobante fiscal electrónico emitido (o en proceso) por un negocio.</summary>
public class ElectronicInvoice : BaseEntity
{
    public Guid AffiliateId { get; set; }
    /// <summary>Venta de origen. Null si el comprobante no sale de un pedido.</summary>
    public Guid? OrderId { get; set; }
    public int DocType { get; set; }
    /// <summary>e-NCF de 13 caracteres, p. ej. E320000000005.</summary>
    public string Encf { get; set; } = string.Empty;
    /// <summary>"queued" | "processing" | "accepted" | "rejected" | "failed".</summary>
    public string Status { get; set; } = "queued";
    /// <summary>Estado legal que informa el proveedor: ACCEPTED, ACCEPTED_WITH_OBSERVATIONS, REJECTED, IN_PROCESS.</summary>
    public string? LegalStatus { get; set; }
    public string? ProviderId { get; set; }

    public string? BuyerRnc { get; set; }
    public string? BuyerName { get; set; }
    public string Currency { get; set; } = "DOP";
    /// <summary>Base facturada (gravada + exenta), sin ITBIS.</summary>
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    /// <summary>Monto no facturable (propina): se cobra pero no forma parte del total del e-CF.</summary>
    public decimal NonBillable { get; set; }
    public decimal Total { get; set; }
    public DateOnly StampDate { get; set; }
    public int PaymentMethod { get; set; } = 1;

    public string? XmlUrl { get; set; }
    public string? PdfUrl { get; set; }
    public string? SecurityCode { get; set; }
    public string? StampUrl { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int Attempts { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }

    public List<ElectronicInvoiceItem> Items { get; set; } = new();
}

public class ElectronicInvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public int LineNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public int BillingIndicator { get; set; }
    public int GoodService { get; set; } = 1;
}

/// <summary>Historial de estados de un comprobante: de dónde vino cada cambio (api, webhook, poll, manual).</summary>
public class ElectronicInvoiceEvent : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Guid AffiliateId { get; set; }
    public string Source { get; set; } = "api";
    public string Status { get; set; } = string.Empty;
    public string? LegalStatus { get; set; }
    public string? Detail { get; set; }
}

/// <summary>Cada notificación que llega del proveedor, tal cual, para auditoría y diagnóstico.</summary>
public class FiscalWebhookEvent : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public bool Handled { get; set; }
    public string? Error { get; set; }
}
