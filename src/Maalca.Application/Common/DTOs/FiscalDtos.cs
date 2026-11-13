namespace Maalca.Application.Common.DTOs;

public record FiscalProfileDto(
    string Rnc, string LegalName, string? TradeName, string Address, string? Province, string? Municipality, string? Email,
    string Environment, string Status, int DefaultBillingIndicator, int DefaultGoodService,
    string? ReceptionUrl, string? ApprovalUrl, string? AuthenticationUrl,
    string? CertificateIssuer, DateTime? CertificateStartsAt, DateTime? CertificateExpiresAt);

public record FiscalProfileRequest(
    string? Rnc, string? LegalName, string? TradeName, string? Address, string? Province, string? Municipality, string? Email,
    string? Environment, int? DefaultBillingIndicator, int? DefaultGoodService);

/// <summary>El certificado (.p12) viaja en base64 y no se guarda: se entrega al proveedor y se descarta.</summary>
public record FiscalRegisterRequest(string? CertificateFileName, string? CertificateBase64, string? CertificatePassword);

public record FiscalSequenceDto(int DocType, long NextNumber, long LastNumber, long Remaining, DateOnly? ExpiresOn, bool IsActive);

/// <summary>Rango autorizado por la DGII: del número From al To (ambos incluidos).</summary>
public record FiscalSequenceRequest(int DocType, long FromNumber, long ToNumber, DateOnly? ExpiresOn);

public record FiscalOverviewDto(
    bool ProviderConfigured, FiscalProfileDto? Profile, List<FiscalSequenceDto> Sequences,
    string WebhookUrl, int Pending, int Rejected, int Failed);

public record EmitInvoiceRequest(Guid OrderId, int DocType, string? BuyerRnc, string? BuyerName);

public record ElectronicInvoiceItemDto(int LineNumber, string Name, decimal Quantity, decimal UnitPrice, decimal Amount, int BillingIndicator);

public record ElectronicInvoiceEventDto(DateTime At, string Source, string Status, string? LegalStatus, string? Detail);

public record ElectronicInvoiceDto(
    Guid Id, Guid? OrderId, int DocType, string Encf, string Status, string? LegalStatus,
    string? BuyerRnc, string? BuyerName, decimal Subtotal, decimal Tax, decimal NonBillable, decimal Total,
    DateOnly StampDate, string? XmlUrl, string? PdfUrl, string? SecurityCode, string? StampUrl,
    string? ErrorCode, string? ErrorMessage, DateTime CreatedAt, DateTime? FinishedAt,
    List<ElectronicInvoiceItemDto>? Items, List<ElectronicInvoiceEventDto>? Events);
