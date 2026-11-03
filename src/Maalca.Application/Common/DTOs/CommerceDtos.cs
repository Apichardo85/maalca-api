namespace Maalca.Application.Common.DTOs;

/// <summary>
/// Atributos de tienda de un InventoryItem (Retail). Viaja como un solo objeto en el catálogo (dashboard y
/// público) y en Create/Update: en un update, null = no tocar; con objeto = se reemplazan todos sus campos.
/// Hazmat: null | "limited_quantity". ChokingWarning: null | "small_parts" | "small_ball" | "marble" | "balloon".
/// </summary>
public record RetailAttributesDto(
    string? Brand = null,
    decimal? WeightOz = null,
    decimal? LengthIn = null,
    decimal? WidthIn = null,
    decimal? HeightIn = null,
    string? Hazmat = null,
    string? ChokingWarning = null,
    string? VariantGroup = null,
    string? VariantLabel = null,
    int? MinOrderQty = null,
    int? OrderQtyMultiple = null,
    int? MaxPerOrder = null);

// ── Listas de precios ─────────────────────────────────────────────────────────

public record PriceListItemDto(Guid ItemId, decimal Price);

public record PriceListDto(
    Guid Id,
    string Name,
    string? Description,
    decimal DiscountPercent,
    decimal? MinOrderAmount,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<PriceListItemDto> Items);

public record UpsertPriceListRequest(
    string Name,
    string? Description,
    decimal DiscountPercent,
    decimal? MinOrderAmount,
    bool IsActive = true,
    int SortOrder = 0,
    IReadOnlyList<PriceListItemDto>? Items = null);

/// <summary>Vista previa: precio público vs precio con la lista, calculado con el mismo motor que el pedido.</summary>
public record PriceListPreviewLineDto(Guid ItemId, string Name, string? VariantLabel, decimal PublicPrice, decimal? FixedPrice, decimal FinalPrice);

// ── Envíos ────────────────────────────────────────────────────────────────────

public record ShippingMethodDto(
    Guid Id,
    string Name,
    string? NameEn,
    decimal Price,
    decimal? FreeOverAmount,
    int? MinDays,
    int? MaxDays,
    bool AllowsHazmat,
    bool IsActive,
    int SortOrder);

public record UpsertShippingMethodRequest(
    string Name,
    string? NameEn,
    decimal Price,
    decimal? FreeOverAmount,
    int? MinDays,
    int? MaxDays,
    bool AllowsHazmat,
    bool IsActive = true,
    int SortOrder = 0);

/// <summary>Lo que ve el checkout público (solo métodos activos).</summary>
public record PublicShippingMethodDto(
    Guid Id, string Name, string? NameEn, decimal Price, decimal? FreeOverAmount, int? MinDays, int? MaxDays, bool AllowsHazmat);

public record ShippingAddressDto(string? Name, string Line1, string? Line2, string City, string State, string Zip);

/// <summary>Despacho manual: paquetería (usps | ups | fedex | dhl | other) + número de rastreo.</summary>
public record MarkShippedRequest(string Carrier, string TrackingNumber);

// ── Cuentas de clientes de la tienda (miembros/mayoristas) ───────────────────

/// <summary>
/// Estado del cliente con sesión en una tienda. Status: "unavailable" (la tienda no tiene cuentas) | "none" (no ha
/// pedido membresía) | "pending" | "approved" | "rejected". El precio de su lista llega aparte (GET .../prices).
/// </summary>
public record StoreMeDto(
    string Status,
    string SignupMode,
    string? SignupNote,
    string? Name,
    string? Email,
    string? PriceListName,
    decimal? DiscountPercent,
    decimal? MinOrderAmount);

public record JoinStoreRequest(string Name, string? Phone, string? BusinessName, string? TaxId, string? Message);

public record MemberPriceDto(Guid ItemId, decimal Price);

public record StoreOrderSummaryDto(
    Guid Id, DateTime CreatedAt, string Status, decimal Total, string Currency, int ItemCount,
    string? FulfillmentType, string? TrackingToken, string? PriceListName);

public record StoreCustomerDto(
    Guid Id, string Name, string Email, string? Phone, string? BusinessName, string? TaxId, string? Message,
    string Status, Guid? PriceListId, string? PriceListName, string? Notes, DateTime CreatedAt, DateTime? ApprovedAt,
    int OrderCount);

/// <summary>Dueño: aprobar/rechazar, asignar lista y notas. Null = no tocar.</summary>
public record UpdateStoreCustomerRequest(string? Status, Guid? PriceListId, bool ClearPriceList = false, string? Notes = null);

public record StoreSettingsDto(string SignupMode, Guid? DefaultPriceListId, string? SignupNote);
