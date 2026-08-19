using Maalca.Application.Common.DTOs;
using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<RefreshTokenResponse?> RefreshTokenAsync(RefreshTokenRequest request);
}

public interface IAffiliateService
{
    Task<AffiliateDto?> GetAffiliateAsync(Guid affiliateId);
    Task<AffiliatePublicProfileDto?> UpdateProfileAsync(Guid affiliateId, UpdateAffiliateProfileRequest request);
    Task<AffiliateContentDto?> UpdateContentAsync(Guid affiliateId, UpdateAffiliateContentRequest request);
}

public interface ICustomerService
{
    Task<PaginatedResponse<Customer>> GetCustomersAsync(Guid affiliateId, int page = 1, int limit = 20, string? search = null, string? status = null);
    Task<Customer?> GetCustomerAsync(Guid affiliateId, Guid id);
    Task<Customer> CreateCustomerAsync(Guid affiliateId, Customer customer);
    Task<Customer?> UpdateCustomerAsync(Guid affiliateId, Guid id, Customer customer);
    Task<bool> DeleteCustomerAsync(Guid affiliateId, Guid id);

    /// <summary>
    /// Tarea #244 — dedup por AffiliateId+Phone, mismo patrón que ya usaba
    /// PublicBookingService.CreatePublicAppointmentAsync antes de esta tarea (ahora centralizado
    /// acá para que Queue/Reservations/Proposals lo reusen también). Sin teléfono no hay forma de
    /// deduplicar, así que devuelve null.
    /// </summary>
    Task<Customer?> ResolveOrCreateCustomerAsync(Guid affiliateId, string name, string? phone);

    /// <summary>Tarea #245 — incrementa TotalVisits y marca LastVisit cuando una cita/fila/reserva
    /// vinculada a este cliente llega a "Completed"/"completed".</summary>
    Task MarkVisitCompletedAsync(Guid customerId);
}

public interface IAppointmentService
{
    Task<PaginatedResponse<Appointment>> GetAppointmentsAsync(Guid affiliateId, DateTime? date = null, string? status = null, int page = 1);
    Task<Appointment?> GetAppointmentAsync(Guid affiliateId, Guid id);
    Task<Appointment> CreateAppointmentAsync(Guid affiliateId, Appointment appointment);
    Task<Appointment?> UpdateAppointmentAsync(Guid affiliateId, Guid id, Appointment appointment);
    Task<Appointment?> UpdateAppointmentStatusAsync(Guid affiliateId, Guid id, string status);
    Task<bool> DeleteAppointmentAsync(Guid affiliateId, Guid id);
}

public interface IServiceService
{
    Task<List<Maalca.Domain.Entities.Service>> GetServicesAsync(Guid affiliateId, string? category = null, string? status = null);
    Task<Maalca.Domain.Entities.Service?> GetServiceAsync(Guid affiliateId, Guid id);
    Task<Maalca.Domain.Entities.Service> CreateServiceAsync(Guid affiliateId, Maalca.Domain.Entities.Service service);
    Task<Maalca.Domain.Entities.Service?> UpdateServiceAsync(Guid affiliateId, Guid id, Maalca.Domain.Entities.Service service);
    Task<bool> DeleteServiceAsync(Guid affiliateId, Guid id);
}

public interface IInventoryService
{
    Task<PaginatedResponse<InventoryItem>> GetInventoryAsync(Guid affiliateId, string? category = null, string? status = null, int page = 1);
    Task<InventoryItem?> GetInventoryItemAsync(Guid affiliateId, Guid id);
    Task<InventoryItem> CreateInventoryItemAsync(Guid affiliateId, InventoryItem item);
    Task<InventoryItem?> UpdateInventoryItemAsync(Guid affiliateId, Guid id, InventoryItem item);
    Task<bool> DeleteInventoryItemAsync(Guid affiliateId, Guid id);
    Task<InventoryMovement> CreateMovementAsync(Guid affiliateId, InventoryMovement movement);
}

public interface IQueueService
{
    Task<List<QueueEntry>> GetQueueAsync(Guid affiliateId);
    Task<QueueEntry> AddToQueueAsync(Guid affiliateId, QueueEntry entry);
    Task<QueueEntry?> UpdateQueueEntryAsync(Guid affiliateId, Guid id, string status, Guid? barberId = null);
}

public interface ITeamService
{
    Task<List<TeamMember>> GetTeamAsync(Guid affiliateId, string? department = null, string? status = null);
    Task<TeamMember?> GetTeamMemberAsync(Guid affiliateId, Guid id);
    Task<TeamMember> CreateTeamMemberAsync(Guid affiliateId, TeamMember member);
    Task<TeamMember?> UpdateTeamMemberAsync(Guid affiliateId, Guid id, TeamMember member);
    Task<bool> DeleteTeamMemberAsync(Guid affiliateId, Guid id);
}

public interface IProductService
{
    Task<PaginatedResponse<Product>> GetProductsAsync(Guid affiliateId, string? category = null, string? status = null);
    Task<Product?> GetProductAsync(Guid affiliateId, Guid id);
    Task<Product> CreateProductAsync(Guid affiliateId, Product product);
    Task<Product?> UpdateProductAsync(Guid affiliateId, Guid id, Product product);
    Task<bool> DeleteProductAsync(Guid affiliateId, Guid id);
}

public interface IInvoiceService
{
    Task<PaginatedResponse<Invoice>> GetInvoicesAsync(Guid affiliateId, string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null);
    Task<Invoice?> GetInvoiceAsync(Guid affiliateId, Guid id);
    Task<Invoice> CreateInvoiceAsync(Guid affiliateId, Invoice invoice, List<InvoiceItem>? items = null);
    Task<Invoice?> UpdateInvoiceAsync(Guid affiliateId, Guid id, Invoice invoice);
    Task<bool> DeleteInvoiceAsync(Guid affiliateId, Guid id);
}

public interface IMetricsService
{
    Task<object> GetMetricsAsync(Guid affiliateId);
}

/// <summary>
/// Reserva pública (sin login) — usada por el widget de agenda en las plantillas públicas
/// (restaurant/barber/service). Resuelve el afiliado por slug (igual que IPublicCatalogService)
/// en vez de por Guid, porque el visitante nunca tiene un affiliateId ni un JWT.
/// </summary>
public interface IPublicBookingService
{
    Task<List<PublicTeamMemberDto>?> GetPublicTeamAsync(string affiliateSlug);
    Task<List<PublicServiceDto>?> GetPublicServicesAsync(string affiliateSlug);
    Task<PublicBusyTimesDto?> GetPublicBusyTimesAsync(string affiliateSlug, DateTime date);
    Task<PublicAppointmentResultDto> CreatePublicAppointmentAsync(string affiliateSlug, CreatePublicAppointmentRequest request);
    Task<PublicTableReservationResultDto> CreatePublicTableReservationAsync(string affiliateSlug, CreatePublicTableReservationRequest request);
    /// <summary>Walk-in "Ahora mismo" desde la página pública — crea un QueueEntry (Channel="web"),
    /// no un Appointment. Solo tiene sentido para Barbería hoy (única con módulo "queue").</summary>
    Task<PublicQueueEntryResultDto> CreatePublicQueueEntryAsync(string affiliateSlug, CreatePublicQueueEntryRequest request);

    /// <summary>Tarea #246 — "gestiona tu cita" sin login, por Appointment.Token. Mismo patrón
    /// que Proposal.Token/GetPublicProposalAsync.</summary>
    Task<PublicAppointmentManageDto?> GetPublicAppointmentByTokenAsync(Guid token);
    Task<PublicAppointmentManageDto> ConfirmPublicAppointmentAsync(Guid token);
    Task<PublicAppointmentManageDto> CancelPublicAppointmentAsync(Guid token);
    Task<PublicAppointmentManageDto> ReschedulePublicAppointmentAsync(Guid token, DateTime date, string time);
}

/// <summary>
/// CRUD de reservas de mesa para el dashboard — separado de IAppointmentService a propósito.
/// Ver TableReservation.cs.
/// </summary>
/// <summary>Task #192 — bloqueo manual de horario, ver TimeBlock.cs.</summary>
public interface ITimeBlockService
{
    Task<List<TimeBlock>> GetTimeBlocksAsync(Guid affiliateId, DateTime? from = null, DateTime? to = null);
    Task<TimeBlock> CreateTimeBlockAsync(Guid affiliateId, TimeBlock block);
    Task<bool> DeleteTimeBlockAsync(Guid affiliateId, Guid id);
}

/// <summary>Task #194 — propuestas de servicio con aceptación pública, ver Proposal.cs.</summary>
public interface IProposalService
{
    Task<List<Proposal>> GetProposalsAsync(Guid affiliateId);
    Task<Proposal> CreateProposalAsync(Guid affiliateId, Proposal proposal);
    Task<Proposal?> SendProposalAsync(Guid affiliateId, Guid id);
    Task<bool> DeleteProposalAsync(Guid affiliateId, Guid id);
    Task<Proposal?> GetPublicProposalAsync(Guid token);
    Task<Proposal> AcceptPublicProposalAsync(Guid token, string signedByName);
}

public interface ITableReservationService
{
    Task<PaginatedResponse<TableReservation>> GetReservationsAsync(Guid affiliateId, DateTime? date = null, string? status = null, int page = 1);
    Task<TableReservation?> GetReservationAsync(Guid affiliateId, Guid id);
    Task<TableReservation> CreateReservationAsync(Guid affiliateId, TableReservation reservation);
    Task<TableReservation?> UpdateReservationAsync(Guid affiliateId, Guid id, TableReservation reservation);
    Task<TableReservation?> UpdateReservationStatusAsync(Guid affiliateId, Guid id, string status);
    Task<bool> DeleteReservationAsync(Guid affiliateId, Guid id);
}

public interface ILeadService
{
    Task<object> GetOverviewMetricsAsync();
    Task<Lead> CreatePropertyLeadAsync(Lead lead);
    Task<Lead> CreateCirisonicLeadAsync(Lead lead);
}
