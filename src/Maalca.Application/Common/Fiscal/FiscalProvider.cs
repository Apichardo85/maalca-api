namespace Maalca.Application.Common.Fiscal;

/// <summary>
/// Puerto del proveedor de facturación electrónica. El dominio de MaalCa solo conoce esto: hoy lo implementa Alanube
/// (Services/Fiscal/AlanubeProvider.cs); cambiar de proveedor es escribir otra implementación y registrarla.
/// Los estados que devuelve son neutros: processing | accepted | rejected | failed.
/// </summary>
public interface IFiscalProvider
{
    /// <summary>Si el proveedor tiene credenciales para ese ambiente ("sandbox" | "production").</summary>
    bool IsConfigured(string environment);

    /// <summary>Da de alta la empresa (con su certificado) y registra el webhook de fin de emisión.</summary>
    Task<FiscalCompanyResult> RegisterCompanyAsync(string environment, FiscalCompanyRegistration request, CancellationToken ct = default);

    /// <summary>Envía un comprobante. Lanza FiscalProviderException si el proveedor lo rechaza o no responde.</summary>
    Task<FiscalDocumentResult> EmitAsync(FiscalAccount account, FiscalDocument document, CancellationToken ct = default);

    /// <summary>Consulta el estado actual de un comprobante ya enviado.</summary>
    Task<FiscalDocumentResult> GetDocumentAsync(FiscalAccount account, int docType, string providerId, CancellationToken ct = default);
}

/// <summary>Cuenta del negocio en el proveedor: ambiente y id de su empresa.</summary>
public record FiscalAccount(string Environment, string? CompanyId);

public record FiscalIssuer(string Rnc, string LegalName, string? TradeName, string Address, string? Province, string? Municipality, string? Email);

public record FiscalBuyer(string? Rnc, string? Name);

public record FiscalLine(int LineNumber, string Name, decimal Quantity, decimal UnitPrice, decimal Amount, int BillingIndicator, int GoodService);

/// <summary>Montos por tasa de ITBIS ya calculados por MaalCa (el proveedor los valida contra las líneas).</summary>
public record FiscalTotals(
    decimal Taxed1, decimal Taxed2, decimal Taxed3, decimal Exempt,
    decimal Itbis1, decimal Itbis2, decimal Itbis3, decimal NonBillable)
{
    public decimal TaxedTotal => Taxed1 + Taxed2 + Taxed3;
    public decimal ItbisTotal => Itbis1 + Itbis2 + Itbis3;
    public decimal Total => TaxedTotal + Exempt + ItbisTotal;
}

/// <param name="PaymentType">1 contado, 2 crédito, 3 gratuito.</param>
/// <param name="PaymentMethod">1 efectivo, 2 transferencia, 3 tarjeta, 4 crédito, 8 otras.</param>
public record FiscalDocument(
    int DocType, string Encf, DateOnly StampDate, int PaymentType, int PaymentMethod,
    FiscalIssuer Issuer, FiscalBuyer? Buyer, IReadOnlyList<FiscalLine> Lines, FiscalTotals Totals);

public record FiscalCompanyRegistration(
    string Name, string? TradeName, string Identification, string Address, string? Province, string? Municipality, string? Email,
    string CertificateFileName, string CertificateBase64, string CertificatePassword,
    string WebhookUrl, string WebhookSecret);

public record FiscalCompanyResult(
    string CompanyId, string? ReceptionUrl, string? ApprovalUrl, string? AuthenticationUrl,
    string? CertificateIssuer, DateTime? CertificateStartsAt, DateTime? CertificateExpiresAt);

/// <param name="Status">processing | accepted | rejected | failed.</param>
public record FiscalDocumentResult(
    string? ProviderId, string Status, string? LegalStatus, string? Encf,
    string? XmlUrl, string? PdfUrl, string? SecurityCode, string? StampUrl,
    string? ErrorCode, string? ErrorMessage);

/// <summary>Falla del proveedor. Transient = no hubo respuesta definitiva (timeout, 5xx): el documento pudo haber llegado.</summary>
public class FiscalProviderException : Exception
{
    public string Code { get; }
    public bool Transient { get; }

    public FiscalProviderException(string code, string message, bool transient = false) : base(message)
    {
        Code = code;
        Transient = transient;
    }
}
