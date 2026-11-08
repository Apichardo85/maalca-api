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
    int? MaxPerOrder = null,
    // Precio por cantidad: desde MinQty unidades del producto, cada una cuesta Price. null = sin escalones.
    IReadOnlyList<VolumeTierDto>? VolumeTiers = null);

public record VolumeTierDto(int MinQty, decimal Price);

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
    IReadOnlyList<PriceListItemDto> Items,
    bool TaxExempt = false);

public record UpsertPriceListRequest(
    string Name,
    string? Description,
    decimal DiscountPercent,
    decimal? MinOrderAmount,
    bool IsActive = true,
    int SortOrder = 0,
    IReadOnlyList<PriceListItemDto>? Items = null,
    bool TaxExempt = false);

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
    int SortOrder,
    string? WeightRates = null,
    string? States = null);

public record UpsertShippingMethodRequest(
    string Name,
    string? NameEn,
    decimal Price,
    decimal? FreeOverAmount,
    int? MinDays,
    int? MaxDays,
    bool AllowsHazmat,
    bool IsActive = true,
    int SortOrder = 0,
    string? WeightRates = null,
    string? States = null);

/// <summary>Lo que ve el checkout público (solo métodos activos).</summary>
public record PublicShippingMethodDto(
    Guid Id, string Name, string? NameEn, decimal Price, decimal? FreeOverAmount, int? MinDays, int? MaxDays, bool AllowsHazmat,
    string? WeightRates = null, string? States = null);

public record ShippingAddressDto(string? Name, string Line1, string? Line2, string City, string State, string Zip);

/// <summary>Despacho manual: paquetería (usps | ups | fedex | dhl | other) + número de rastreo.</summary>
public record MarkShippedRequest(string Carrier, string TrackingNumber);

// ── Etiquetas con el agregador del negocio (United Bridge) ───────────────────

/// <summary>Cuenta de etiquetas. La API key nunca sale del servidor: solo HasKey + últimos 4.</summary>
public record CarrierAccountDto(
    bool HasKey, string? KeyLast4, string Service, string Package,
    decimal LengthIn, decimal WidthIn, decimal HeightIn, decimal BoxWeightOz,
    string? FromName, string? FromPhone, string? FromAddress1, string? FromAddress2,
    string? FromCity, string? FromState, string? FromZip, bool IsActive);

/// <summary>ApiKey null o vacía = conservar la que ya está guardada.</summary>
public record UpsertCarrierAccountRequest(
    string? ApiKey, string Service, string Package,
    decimal LengthIn, decimal WidthIn, decimal HeightIn, decimal BoxWeightOz,
    string? FromName, string? FromPhone, string? FromAddress1, string? FromAddress2,
    string? FromCity, string? FromState, string? FromZip, bool IsActive = true);

/// <summary>WeightOz opcional: peso real del paquete en onzas (sin la caja). Null = suma de los productos del pedido.</summary>
public record LabelRequest(decimal? WeightOz = null);

public record LabelQuoteDto(string Service, decimal Total, decimal ShipmentRate, string Currency, decimal? AccountBalance, decimal WeightOz, decimal Charged);

public record BuyLabelResultDto(OrderDto Order, string LabelUrl, decimal Cost, decimal? AccountBalance);

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
    decimal? MinOrderAmount,
    bool TaxExempt = false,
    // Datos que dio al pedir la cuenta ("Mi cuenta" los muestra y deja corregirlos).
    string? Phone = null,
    string? BusinessName = null,
    string? TaxId = null);

/// <summary>"Mi cuenta": el cliente corrige sus datos. Null = no se toca.</summary>
public record UpdateMyStoreProfileRequest(string? Name, string? Phone, string? BusinessName, string? TaxId);

public record JoinStoreRequest(string Name, string? Phone, string? BusinessName, string? TaxId, string? Message,
    string? Address = null, decimal? GpsLat = null, decimal? GpsLng = null, string? DeliveryFrequency = null, bool WantsCredit = false);

public record MemberPriceDto(Guid ItemId, decimal Price);

public record StoreOrderSummaryDto(
    Guid Id, DateTime CreatedAt, string Status, decimal Total, string Currency, int ItemCount,
    string? FulfillmentType, string? TrackingToken, string? PriceListName,
    // Líneas del pedido para "Comprar de nuevo": el carrito las vuelve a cargar y el API recalcula todo al pedir.
    IReadOnlyList<StoreOrderLineDto>? Items = null,
    string? ShippingCarrier = null,
    string? TrackingNumber = null,
    // Cuenta como compra (cobrada). Mismo criterio que "Gastado" en la ficha del dueño.
    bool Counted = true);

// ImageUrl: foto actual del producto en el catálogo (miniatura en "Mi cuenta"); null si ya no existe o no tiene.
public record StoreOrderLineDto(string ItemId, string Name, int Qty, decimal UnitPrice, string? ImageUrl = null);

public record StoreCustomerDto(
    Guid Id, string Name, string Email, string? Phone, string? BusinessName, string? TaxId, string? Message,
    string Status, Guid? PriceListId, string? PriceListName, string? Notes, DateTime CreatedAt, DateTime? ApprovedAt,
    int OrderCount,
    string? Address = null, decimal? GpsLat = null, decimal? GpsLng = null, string? DeliveryFrequency = null, bool WantsCredit = false);

/// <summary>Dueño: aprobar/rechazar, asignar lista y notas. Null = no tocar.</summary>
public record UpdateStoreCustomerRequest(string? Status, Guid? PriceListId, bool ClearPriceList = false, string? Notes = null);

public record StoreSettingsDto(string SignupMode, Guid? DefaultPriceListId, string? SignupNote);

/// <summary>Impuesto de venta del negocio. Rate en % (null = sin configurar).</summary>
public record TaxSettingsDto(decimal? Rate, bool TaxShipping, string? State = null);

// ── Ficha del cliente (panel del dueño) ─────────────────────────────────────────────────────────
/// <summary>Cuenta de tienda (mayorista) ligada al cliente por correo; null si no tiene.</summary>
public record CustomerStoreAccountDto(Guid Id, string Status, string? BusinessName, string? TaxId, Guid? PriceListId, string? PriceListName);

/// <summary>Compras = pedidos no cancelados; en pago en el local solo cuentan los ya cobrados.</summary>
public record CustomerStatsDto(
    int OrderCount, int PurchaseCount, decimal Spent, decimal AvgTicket,
    DateTime? FirstOrderAt, DateTime? LastOrderAt, double? AvgDaysBetween);

public record CustomerMonthDto(string Month, decimal Total, int Orders);
public record CustomerTopProductDto(string Name, int Qty, decimal Amount, DateTime LastAt);
public record CustomerOrderLineDto(string Name, int Qty, decimal Price);
public record CustomerOrderDetailDto(Guid Id, DateTime CreatedAt, string Status, decimal Total, bool Counted, IReadOnlyList<CustomerOrderLineDto> Items);

public record CustomerInsightsDto(
    CustomerStoreAccountDto? Account,
    CustomerStatsDto Stats,
    IReadOnlyList<CustomerMonthDto> Monthly,
    IReadOnlyList<CustomerTopProductDto> TopProducts,
    IReadOnlyList<CustomerOrderDetailDto> Orders,
    // Marcas "No vino" del cliente: si el negocio las usa, bloquean pedir pagando al recoger.
    int NoShows = 0);

/// <summary>Lista de precios de un cliente para el POS: precio por producto ya resuelto. Null si no tiene lista activa.</summary>
public record CustomerPriceListDto(string Name, decimal? MinOrderAmount, bool TaxExempt, IReadOnlyList<MemberPriceDto> Prices);

/// <summary>Reporte de ventas por cliente en un rango de fechas.</summary>
public record CustomerSalesRowDto(Guid CustomerId, string Name, string? Email, string? Phone, string? BusinessName,
    int Orders, decimal Spent, decimal AvgTicket, DateTime? LastOrderAt);
