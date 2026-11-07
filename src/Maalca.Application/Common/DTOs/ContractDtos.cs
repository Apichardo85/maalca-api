using System.Text.Json;

namespace Maalca.Application.Common.DTOs;

/// <summary>Contrato recurrente de un cliente. AgreedPrices = [{"itemId":"…","price":50}, …] o null (precios de lista).</summary>
public record ContractDto(
    Guid Id, Guid CustomerId, string? CustomerName, string DeliveryFrequency, string BillingCycle,
    int CreditDays, JsonElement? AgreedPrices, DateOnly StartDate, DateOnly? EndDate,
    bool IsActive, string? Notes, DateTime CreatedAt,
    string? ContractNumber = null, int RacksLoaned = 0, int BottlesLoaned = 0);

/// <summary>Alta y edición (PUT reemplaza todos los campos editables; el cliente de un contrato no se cambia).
/// DeliveryFrequency: daily | weekly | on_demand. BillingCycle: weekly | biweekly | monthly.</summary>
public record UpsertContractRequest(
    Guid CustomerId, string DeliveryFrequency, string BillingCycle, int CreditDays,
    JsonElement? AgreedPrices, DateOnly StartDate, DateOnly? EndDate, bool IsActive = true, string? Notes = null,
    string? ContractNumber = null, int RacksLoaned = 0, int BottlesLoaned = 0);

/// <summary>Pago o abono. InvoiceId null = abono a cuenta.</summary>
public record PaymentDto(
    Guid Id, Guid CustomerId, Guid? InvoiceId, string? InvoiceNumber, decimal Amount, string Method,
    DateTime PaidAt, string? Notes, string? RegisteredBy, DateTime CreatedAt);

/// <summary>Method: cash | transfer. IdempotencyKey: la genera el celular una vez por cobro; un reintento con la
/// misma clave devuelve el pago ya registrado en vez de duplicarlo.</summary>
public record RegisterPaymentRequest(
    Guid CustomerId, decimal Amount, string? Method = "cash", Guid? InvoiceId = null,
    DateTime? PaidAt = null, string? Notes = null, string? IdempotencyKey = null);

/// <summary>Duplicate = true si la clave de idempotencia ya existía (no se creó nada nuevo).
/// InvoiceMarkedPaid = true si este pago completó la factura ligada y la pasó a Paid.</summary>
public record RegisterPaymentResultDto(PaymentDto Payment, bool Duplicate, bool InvoiceMarkedPaid);
