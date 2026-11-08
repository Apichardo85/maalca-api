namespace Maalca.Application.Common.DTOs;

// Portal del cliente (/{slug}/cuenta): lo que un cliente con contrato ve de SU cuenta. Todo sale de la ficha de Cliente
// enlazada a su cuenta de tienda por correo; nunca se manda nada de otros clientes.

/// <summary>Contrato activo del cliente. Accepted = ya lo aceptó Y el texto no cambió desde entonces.
/// DocumentHash = huella del texto actual: el cliente la devuelve al aceptar para probar qué versión leyó.</summary>
public record PortalContractDto(
    Guid Id, string? ContractNumber, string DeliveryFrequency, string BillingCycle, int CreditDays,
    DateOnly StartDate, DateOnly? EndDate, int RacksLoaned, int BottlesLoaned,
    bool Accepted, DateTime? AcceptedAt, string? AcceptedByName, bool ChangedSinceAccepted, string DocumentHash);

public record PortalAnaquelDto(string? Label, string? Location, string Status);

public record PortalPaymentDto(DateTime PaidAt, decimal Amount, string Method);

public record PortalDeliveryDto(DateTime DeliveredAt, IReadOnlyList<DeliveryItemDto> Items, decimal Total);

/// <summary>Próxima visita: la primera parada pendiente del cliente en una ruta de hoy en adelante.
/// OnTheWay = la ruta ya salió; StopsBefore = paradas pendientes antes que la suya (solo con la ruta en curso).</summary>
public record PortalNextVisitDto(DateOnly Date, string RouteStatus, bool OnTheWay, int? StopsBefore);

/// <summary>Linked = false: la cuenta está aprobada pero aún no hay una ficha de Cliente con su correo (nada más que mostrar).</summary>
public record CustomerPortalDto(
    bool Linked, string CustomerName, string? Address,
    PortalContractDto? Contract,
    IReadOnlyList<ContainerBalanceItemDto> Containers,
    IReadOnlyList<PortalAnaquelDto> Anaqueles,
    decimal Balance, decimal CreditOnAccount, IReadOnlyList<OpenInvoiceDto> OpenInvoices,
    IReadOnlyList<PortalPaymentDto> RecentPayments,
    IReadOnlyList<PortalDeliveryDto> RecentDeliveries,
    PortalNextVisitDto? NextVisit,
    string? DeliveryFrequency);

/// <summary>Aceptar el contrato: nombre escrito como firma, casilla marcada y la huella del texto que leyó.</summary>
public record AcceptContractRequest(string Name, bool Agree, string DocumentHash);
