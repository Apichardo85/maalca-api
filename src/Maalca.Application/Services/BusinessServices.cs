using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

// Nota: este archivo deliberadamente NO tiene `using Stripe;`/`using Stripe.Checkout;` a nivel de
// archivo — Stripe.Invoice/Stripe.Product/Stripe.InvoiceItem colisionan (CS0104, ambiguous
// reference) con las entidades de dominio del mismo nombre que ya viven en TODO este archivo
// (ProductService, InvoiceService y compañía). Los tipos de Stripe usados en
// InvoiceService.CreateInvoiceCheckoutAsync están totalmente calificados (Stripe.XxxOptions) en
// vez de importados, a propósito.

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _context;
    private readonly ICustomerService _customerService;

    public AppointmentService(AppDbContext context, ICustomerService customerService)
    {
        _context = context;
        _customerService = customerService;
    }

    public async Task<PaginatedResponse<Appointment>> GetAppointmentsAsync(Guid affiliateId, DateTime? date = null, string? status = null, int page = 1)
    {
        var baseQuery = _context.Appointments.Where(a => a.AffiliateId == affiliateId);

        // AsNoTracking: sin esto, el change tracker hace "fix-up" automático de la navegación
        // inversa Service.Appointments con el mismo Appointment que se está devolviendo, creando
        // un ciclo (Appointment→Service→Appointments→mismo Appointment→...) que rompía la
        // serialización JSON a mitad de respuesta. Estos son endpoints de solo lectura — no hay
        // razón para trackear cambios.
        IQueryable<Appointment> query = baseQuery.AsNoTracking()
            .Include(a => a.Customer).Include(a => a.Service).Include(a => a.AssignedTo);

        if (date.HasValue)
            query = query.Where(a => a.Date.Date == date.Value.Date);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);

        var total = await query.CountAsync();
        var data = await query.OrderBy(a => a.Date).ThenBy(a => a.Time)
            .Skip((page - 1) * 20).Take(20).ToListAsync();

        return new PaginatedResponse<Appointment> { Data = data, Total = total, Page = page, TotalPages = (int)Math.Ceiling((double)total / 20) };
    }

    public async Task<Appointment?> GetAppointmentAsync(Guid affiliateId, Guid id)
        => await _context.Appointments.AsNoTracking()
            .Include(a => a.Customer).Include(a => a.Service).Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(a => a.Id == id && a.AffiliateId == affiliateId);

    public async Task<Appointment> CreateAppointmentAsync(Guid affiliateId, Appointment appointment)
    {
        appointment.AffiliateId = affiliateId;
        appointment.Id = Guid.NewGuid();
        appointment.CreatedAt = DateTime.UtcNow;
        // El body llega como fecha "bare" (yyyy-MM-dd), System.Text.Json la deserializa con
        // Kind=Unspecified. La columna es timestamp with time zone y Npgsql 8+ rechaza escribir
        // ahí un DateTime Unspecified. Es una fecha de calendario, no un instante real — forzamos
        // Kind=Utc en vez de tocar el tipo de columna.
        appointment.Date = DateTime.SpecifyKind(appointment.Date.Date, DateTimeKind.Utc);

        // Mismo chequeo de choque que ya existe en la reserva pública (PublicBookingService)
        // — el dashboard nunca lo tuvo y permitía doble-agendar al mismo barbero a la misma hora.
        if (appointment.AssignedToId is Guid assignedToId)
        {
            var conflict = await _context.Appointments.AnyAsync(a =>
                a.AffiliateId == affiliateId &&
                a.AssignedToId == assignedToId &&
                a.Date.Date == appointment.Date.Date &&
                a.Time == appointment.Time &&
                a.Status != "Cancelled");
            if (conflict)
                throw new InvalidOperationException("Ese horario ya no está disponible — elige otro.");
        }

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();
        return appointment;
    }

    public async Task<Appointment?> UpdateAppointmentAsync(Guid affiliateId, Guid id, Appointment appointment)
    {
        var existing = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.AffiliateId == affiliateId);
        if (existing == null) return null;

        var newDate = DateTime.SpecifyKind(appointment.Date.Date, DateTimeKind.Utc);

        // Mismo chequeo, excluyendo la propia cita que se está editando — para no bloquear
        // guardar una cita contra sí misma cuando no cambió nada relevante.
        if (appointment.AssignedToId is Guid assignedToId)
        {
            var conflict = await _context.Appointments.AnyAsync(a =>
                a.Id != id &&
                a.AffiliateId == affiliateId &&
                a.AssignedToId == assignedToId &&
                a.Date.Date == newDate.Date &&
                a.Time == appointment.Time &&
                a.Status != "Cancelled");
            if (conflict)
                throw new InvalidOperationException("Ese horario ya no está disponible — elige otro.");
        }

        var previousStatus = existing.Status;
        existing.CustomerId = appointment.CustomerId;
        existing.ServiceId = appointment.ServiceId;
        existing.Date = newDate;
        existing.Time = appointment.Time;
        existing.Status = appointment.Status;
        existing.Notes = appointment.Notes;
        existing.AssignedToId = appointment.AssignedToId;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Tarea #245 — solo al transicionar A Completed (no en cada guardado de una cita que ya
        // estaba Completed), para no inflar TotalVisits con cada edición menor.
        if (previousStatus != "Completed" && existing.Status == "Completed")
            await _customerService.MarkVisitCompletedAsync(existing.CustomerId);

        return existing;
    }

    public async Task<Appointment?> UpdateAppointmentStatusAsync(Guid affiliateId, Guid id, string status)
    {
        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.AffiliateId == affiliateId);
        if (appointment == null) return null;
        var previousStatus = appointment.Status;
        appointment.Status = status;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (previousStatus != "Completed" && appointment.Status == "Completed")
            await _customerService.MarkVisitCompletedAsync(appointment.CustomerId);

        return appointment;
    }

    public async Task<bool> DeleteAppointmentAsync(Guid affiliateId, Guid id)
    {
        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.AffiliateId == affiliateId);
        if (appointment == null) return false;
        _context.Appointments.Remove(appointment);
        await _context.SaveChangesAsync();
        return true;
    }
}

/// <summary>Task #192 — bloqueo manual de horario, ver TimeBlock.cs.</summary>
public class TimeBlockService : ITimeBlockService
{
    private readonly AppDbContext _context;

    public TimeBlockService(AppDbContext context) => _context = context;

    public async Task<List<TimeBlock>> GetTimeBlocksAsync(Guid affiliateId, DateTime? from = null, DateTime? to = null)
    {
        IQueryable<TimeBlock> query = _context.TimeBlocks.AsNoTracking()
            .Where(b => b.AffiliateId == affiliateId);

        if (from.HasValue)
            query = query.Where(b => b.Date.Date >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(b => b.Date.Date <= to.Value.Date);

        return await query.OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToListAsync();
    }

    public async Task<TimeBlock> CreateTimeBlockAsync(Guid affiliateId, TimeBlock block)
    {
        block.AffiliateId = affiliateId;
        block.Id = Guid.NewGuid();
        block.CreatedAt = DateTime.UtcNow;
        // Misma razón de siempre: fecha "bare" del front deserializa Kind=Unspecified, la
        // columna es timestamptz.
        block.Date = DateTime.SpecifyKind(block.Date.Date, DateTimeKind.Utc);

        _context.TimeBlocks.Add(block);
        await _context.SaveChangesAsync();
        return block;
    }

    public async Task<bool> DeleteTimeBlockAsync(Guid affiliateId, Guid id)
    {
        var block = await _context.TimeBlocks.FirstOrDefaultAsync(b => b.Id == id && b.AffiliateId == affiliateId);
        if (block is null) return false;
        _context.TimeBlocks.Remove(block);
        await _context.SaveChangesAsync();
        return true;
    }
}

/// <summary>Task #194 — propuestas de servicio con aceptación pública, ver Proposal.cs.</summary>
public class ProposalService : IProposalService
{
    private readonly AppDbContext _context;
    private readonly ICustomerService _customerService;
    private readonly IProposalNotificationService _notifications;

    public ProposalService(AppDbContext context, ICustomerService customerService, IProposalNotificationService notifications)
    {
        _context = context;
        _customerService = customerService;
        _notifications = notifications;
    }

    public async Task<List<Proposal>> GetProposalsAsync(Guid affiliateId)
        => await _context.Proposals.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<Proposal> CreateProposalAsync(Guid affiliateId, Proposal proposal)
    {
        proposal.AffiliateId = affiliateId;
        proposal.Id = Guid.NewGuid();
        proposal.Token = Guid.NewGuid();
        proposal.Status = "Draft";
        proposal.CreatedAt = DateTime.UtcNow;
        // Bug real: truncar a medianoche del dia elegido dejaba la propuesta expirada casi de
        // inmediato (si "hoy" ya paso la medianoche UTC, ExpiresAt < UtcNow desde el momento de
        // crearla). Fin del dia elegido, no el inicio.
        if (proposal.ExpiresAt.HasValue)
            proposal.ExpiresAt = DateTime.SpecifyKind(proposal.ExpiresAt.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        // Tarea #244 — si el negocio dejó un teléfono, vincula (o crea) el Customer para que,
        // si el prospecto convierte, su historial no arranque de cero.
        var customer = await _customerService.ResolveOrCreateCustomerAsync(affiliateId, proposal.CustomerName, proposal.CustomerPhone);
        proposal.CustomerId = customer?.Id;

        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();
        return proposal;
    }

    // "Enviar" marca el estado para que el cliente pueda aceptarla (antes de esto,
    // AcceptPublicProposalAsync la rechaza por seguir en Draft) y, si el cliente tiene correo,
    // dispara un email real con el link — antes solo quedaba en manos del dueño copiarlo y
    // mandarlo por su cuenta. Si no hay correo, el link se sigue pudiendo copiar/mandar por
    // WhatsApp igual que antes.
    public async Task<Proposal?> SendProposalAsync(Guid affiliateId, Guid id)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Affiliate)
            .FirstOrDefaultAsync(p => p.Id == id && p.AffiliateId == affiliateId);
        if (proposal is null) return null;
        proposal.Status = "Sent";
        proposal.SentAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var webUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL")?.TrimEnd('/') ?? "https://maalca.com";
        var proposalLink = $"{webUrl}/propuesta/{proposal.Token}";
        await _notifications.NotifyProposalSentAsync(proposal, proposal.Affiliate?.Name ?? "", proposalLink);

        return proposal;
    }

    public async Task<bool> DeleteProposalAsync(Guid affiliateId, Guid id)
    {
        var proposal = await _context.Proposals.FirstOrDefaultAsync(p => p.Id == id && p.AffiliateId == affiliateId);
        if (proposal is null) return false;
        _context.Proposals.Remove(proposal);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Proposal?> GetPublicProposalAsync(Guid token)
        => await _context.Proposals.AsNoTracking()
            .Include(p => p.Affiliate)
            .FirstOrDefaultAsync(p => p.Token == token);

    public async Task<Proposal> AcceptPublicProposalAsync(Guid token, string signedByName, string? ip, string? userAgent)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Affiliate)
            .FirstOrDefaultAsync(p => p.Token == token);
        if (proposal is null)
            throw new KeyNotFoundException();
        if (proposal.Status != "Sent")
            throw new InvalidOperationException("Esta propuesta ya no está disponible para aceptar.");
        if (proposal.ExpiresAt.HasValue && proposal.ExpiresAt.Value < DateTime.UtcNow)
        {
            proposal.Status = "Expired";
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Esta propuesta expiró.");
        }
        if (string.IsNullOrWhiteSpace(signedByName))
            throw new ArgumentException("El nombre es requerido para aceptar.");

        proposal.Status = "Accepted";
        proposal.AcceptedAt = DateTime.UtcNow;
        proposal.AcceptedByName = signedByName.Trim();
        proposal.AcceptedIp = ip;
        proposal.AcceptedUserAgent = userAgent;
        await _context.SaveChangesAsync();

        if (proposal.Affiliate is not null)
            await _notifications.NotifyProposalAcceptedAsync(proposal, proposal.Affiliate);

        return proposal;
    }
}

public class TableReservationService : ITableReservationService
{
    private readonly AppDbContext _context;
    private readonly ICustomerService _customerService;

    public TableReservationService(AppDbContext context, ICustomerService customerService)
    {
        _context = context;
        _customerService = customerService;
    }

    public async Task<PaginatedResponse<TableReservation>> GetReservationsAsync(Guid affiliateId, DateTime? date = null, string? status = null, int page = 1)
    {
        IQueryable<TableReservation> query = _context.TableReservations.AsNoTracking()
            .Where(r => r.AffiliateId == affiliateId);

        if (date.HasValue)
            query = query.Where(r => r.Date.Date == date.Value.Date);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        var total = await query.CountAsync();
        var data = await query.OrderBy(r => r.Date).ThenBy(r => r.Time)
            .Skip((page - 1) * 20).Take(20).ToListAsync();

        return new PaginatedResponse<TableReservation> { Data = data, Total = total, Page = page, TotalPages = (int)Math.Ceiling((double)total / 20) };
    }

    public async Task<TableReservation?> GetReservationAsync(Guid affiliateId, Guid id)
        => await _context.TableReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.AffiliateId == affiliateId);

    public async Task<TableReservation> CreateReservationAsync(Guid affiliateId, TableReservation reservation)
    {
        reservation.AffiliateId = affiliateId;
        reservation.Id = Guid.NewGuid();
        reservation.CreatedAt = DateTime.UtcNow;
        // Misma razón que Appointment.Date — fecha "bare" deserializa Kind=Unspecified, la columna
        // es timestamptz y Npgsql 8+ rechaza escribir ahí un DateTime Unspecified.
        reservation.Date = DateTime.SpecifyKind(reservation.Date.Date, DateTimeKind.Utc);
        if (reservation.PartySize < 1) reservation.PartySize = 1;

        // Tarea #244 — cubre la reserva creada por el staff desde el dashboard; el flujo público
        // (PublicBookingService) ya resuelve el suyo antes de llegar acá.
        if (reservation.CustomerId is null)
        {
            var customer = await _customerService.ResolveOrCreateCustomerAsync(affiliateId, reservation.CustomerName, reservation.CustomerPhone);
            reservation.CustomerId = customer?.Id;
        }

        _context.TableReservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task<TableReservation?> UpdateReservationAsync(Guid affiliateId, Guid id, TableReservation reservation)
    {
        var existing = await _context.TableReservations.FirstOrDefaultAsync(r => r.Id == id && r.AffiliateId == affiliateId);
        if (existing == null) return null;

        var previousStatus = existing.Status;
        existing.CustomerName = reservation.CustomerName;
        existing.CustomerPhone = reservation.CustomerPhone;
        existing.CustomerEmail = reservation.CustomerEmail;
        existing.Date = DateTime.SpecifyKind(reservation.Date.Date, DateTimeKind.Utc);
        existing.Time = reservation.Time;
        existing.PartySize = reservation.PartySize < 1 ? 1 : reservation.PartySize;
        existing.Status = reservation.Status;
        existing.Notes = reservation.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (previousStatus != "Completed" && existing.Status == "Completed" && existing.CustomerId.HasValue)
            await _customerService.MarkVisitCompletedAsync(existing.CustomerId.Value);

        return existing;
    }

    public async Task<TableReservation?> UpdateReservationStatusAsync(Guid affiliateId, Guid id, string status)
    {
        var reservation = await _context.TableReservations.FirstOrDefaultAsync(r => r.Id == id && r.AffiliateId == affiliateId);
        if (reservation == null) return null;
        var previousStatus = reservation.Status;
        reservation.Status = status;
        reservation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (previousStatus != "Completed" && reservation.Status == "Completed" && reservation.CustomerId.HasValue)
            await _customerService.MarkVisitCompletedAsync(reservation.CustomerId.Value);

        return reservation;
    }

    public async Task<bool> DeleteReservationAsync(Guid affiliateId, Guid id)
    {
        var reservation = await _context.TableReservations.FirstOrDefaultAsync(r => r.Id == id && r.AffiliateId == affiliateId);
        if (reservation == null) return false;
        _context.TableReservations.Remove(reservation);
        await _context.SaveChangesAsync();
        return true;
    }
}

public class ServiceService : IServiceService
{
    private readonly AppDbContext _context;

    public ServiceService(AppDbContext context) => _context = context;

    public async Task<List<Maalca.Domain.Entities.Service>> GetServicesAsync(Guid affiliateId, string? category = null, string? status = null)
    {
        var query = _context.Services.Where(s => s.AffiliateId == affiliateId);
        if (!string.IsNullOrEmpty(category)) query = query.Where(s => s.Category == category);
        if (!string.IsNullOrEmpty(status)) query = query.Where(s => s.Status == status);
        return await query.ToListAsync();
    }

    public async Task<Maalca.Domain.Entities.Service?> GetServiceAsync(Guid affiliateId, Guid id)
        => await _context.Services.FirstOrDefaultAsync(s => s.Id == id && s.AffiliateId == affiliateId);

    public async Task<Maalca.Domain.Entities.Service> CreateServiceAsync(Guid affiliateId, Maalca.Domain.Entities.Service service)
    {
        service.AffiliateId = affiliateId;
        service.Id = Guid.NewGuid();
        service.CreatedAt = DateTime.UtcNow;
        _context.Services.Add(service);
        await _context.SaveChangesAsync();
        return service;
    }

    public async Task<Maalca.Domain.Entities.Service?> UpdateServiceAsync(Guid affiliateId, Guid id, Maalca.Domain.Entities.Service service)
    {
        var existing = await _context.Services.FirstOrDefaultAsync(s => s.Id == id && s.AffiliateId == affiliateId);
        if (existing == null) return null;
        existing.Name = service.Name;
        existing.Description = service.Description;
        existing.Price = service.Price;
        existing.DurationMinutes = service.DurationMinutes;
        existing.Category = service.Category;
        existing.IsActive = service.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteServiceAsync(Guid affiliateId, Guid id)
    {
        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == id && s.AffiliateId == affiliateId);
        if (service == null) return false;
        _context.Services.Remove(service);
        await _context.SaveChangesAsync();
        return true;
    }
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;

    public InventoryService(AppDbContext context) => _context = context;

    public async Task<PaginatedResponse<InventoryItem>> GetInventoryAsync(Guid affiliateId, string? category = null, string? status = null, int page = 1, string? search = null, bool? lowStock = null)
    {
        var query = _context.InventoryItems.Where(i => i.AffiliateId == affiliateId);
        if (!string.IsNullOrEmpty(category)) query = query.Where(i => i.Category == category);
        if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.Status == status);
        // Búsqueda por nombre (case-insensitive) y filtro "solo stock bajo" — antes solo existían
        // category/status, así que un negocio con más de 20 items (una sola página) no tenía
        // forma de encontrar un item específico sin pasar de página a página.
        if (!string.IsNullOrEmpty(search)) query = query.Where(i => EF.Functions.ILike(i.Name, $"%{search}%"));
        if (lowStock == true) query = query.Where(i => i.Quantity <= i.MinStock);
        var total = await query.CountAsync();
        var data = await query.OrderBy(i => i.Name).Skip((page - 1) * 20).Take(20).ToListAsync();
        return new PaginatedResponse<InventoryItem> { Data = data, Total = total, Page = page, TotalPages = (int)Math.Ceiling((double)total / 20) };
    }

    public async Task<InventoryItem?> GetInventoryItemAsync(Guid affiliateId, Guid id)
        => await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);

    public async Task<InventoryItem> CreateInventoryItemAsync(Guid affiliateId, InventoryItem item)
    {
        item.AffiliateId = affiliateId;
        item.Id = Guid.NewGuid();
        item.CreatedAt = DateTime.UtcNow;
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<InventoryItem?> UpdateInventoryItemAsync(Guid affiliateId, Guid id, InventoryItem item)
    {
        var existing = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);
        if (existing == null) return null;
        existing.Name = item.Name;
        existing.Description = item.Description;
        existing.Category = item.Category;
        existing.Quantity = item.Quantity;
        existing.MinStock = item.MinStock;
        existing.UnitPrice = item.UnitPrice;
        existing.Unit = string.IsNullOrWhiteSpace(item.Unit) ? existing.Unit : item.Unit;
        existing.Status = item.Status;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>Borrar un ingrediente que está en la receta de uno o más platos dejaba el vínculo
    /// desaparecer en cascada sin avisar — el plato seguía vendiéndose pero dejaba de descontar
    /// ese ingrediente. Ahora se bloquea con un mensaje claro (qué platos lo usan) hasta que se
    /// quite de la receta primero.</summary>
    public async Task<bool> DeleteInventoryItemAsync(Guid affiliateId, Guid id)
    {
        var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);
        if (item == null) return false;

        var usedInDishes = await _context.ProductIngredients
            .Where(pi => pi.InventoryItemId == id)
            .Include(pi => pi.Product)
            .Where(pi => pi.Product != null)
            .Select(pi => pi.Product!.Name)
            .Distinct()
            .ToListAsync();
        if (usedInDishes.Count > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar \"{item.Name}\": está en la receta de {string.Join(", ", usedInDishes)}. Quítalo de esa receta primero.");

        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<InventoryMovement> CreateMovementAsync(Guid affiliateId, InventoryMovement movement)
    {
        var item = await _context.InventoryItems.FindAsync(movement.InventoryItemId);
        if (item == null || item.AffiliateId != affiliateId)
            throw new InvalidOperationException("Inventory item not found");

        movement.Id = Guid.NewGuid();
        movement.CreatedAt = DateTime.UtcNow;

        if (movement.Type == "in")
            item.Quantity += movement.Quantity;
        else
            item.Quantity = Math.Max(0, item.Quantity - movement.Quantity);

        _context.InventoryMovements.Add(movement);
        await _context.SaveChangesAsync();
        return movement;
    }

    // Tarea: historial de movimientos — antes se guardaban pero no había forma de volver a verlos.
    public async Task<PaginatedResponse<InventoryMovement>> GetMovementsAsync(Guid affiliateId, Guid itemId, int page = 1)
    {
        var itemBelongs = await _context.InventoryItems.AnyAsync(i => i.Id == itemId && i.AffiliateId == affiliateId);
        if (!itemBelongs) return new PaginatedResponse<InventoryMovement> { Data = new List<InventoryMovement>(), Total = 0, Page = page, TotalPages = 0 };

        var query = _context.InventoryMovements.Where(m => m.InventoryItemId == itemId);
        var total = await query.CountAsync();
        var data = await query.OrderByDescending(m => m.CreatedAt).Skip((page - 1) * 20).Take(20).ToListAsync();
        return new PaginatedResponse<InventoryMovement> { Data = data, Total = total, Page = page, TotalPages = (int)Math.Ceiling((double)total / 20) };
    }

    // Tarea: resumen (valor total + stock bajo) — usado por Inventario y por la alerta del Dashboard.
    public async Task<InventorySummaryDto> GetSummaryAsync(Guid affiliateId)
    {
        var items = await _context.InventoryItems.Where(i => i.AffiliateId == affiliateId).ToListAsync();
        var totalValue = items.Sum(i => i.Quantity * i.UnitPrice);
        var low = items.Where(i => i.Quantity <= i.MinStock).OrderBy(i => i.Name).ToList();
        return new InventorySummaryDto(
            totalValue,
            items.Count,
            low.Count,
            low.Select(i => new LowStockItemDto(i.Id, i.Name, i.Quantity, i.MinStock, i.Unit)).ToList());
    }

    // Tarea: exportar CSV — carga inicial/backup manual sin depender de agregar item por item.
    public async Task<string> ExportCsvAsync(Guid affiliateId)
    {
        var items = await _context.InventoryItems
            .Where(i => i.AffiliateId == affiliateId)
            .OrderBy(i => i.Name)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Nombre,Categoria,Cantidad,Unidad,StockMinimo,PrecioUnitario,Estado");
        foreach (var i in items)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(i.Name),
                CsvEscape(i.Category ?? ""),
                i.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CsvEscape(i.Unit),
                i.MinStock.ToString(System.Globalization.CultureInfo.InvariantCulture),
                i.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CsvEscape(i.Status)));
        }
        return sb.ToString();
    }

    // Tarea: importar CSV — misma cabecera que ExportCsvAsync produce; columnas de más se ignoran,
    // filas sin Nombre se saltan. No valida duplicados por nombre a propósito (permite reabastecer
    // agregando de nuevo con Quantity sumándose vía movimientos, no vía import).
    public async Task<InventoryCsvImportResultDto> ImportCsvAsync(Guid affiliateId, string csvContent)
    {
        var lines = csvContent.Replace("\r\n", "\n").Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        if (lines.Count == 0)
            return new InventoryCsvImportResultDto(0, 0, new List<string>());

        var errors = new List<string>();
        var created = 0;
        var now = DateTime.UtcNow;

        // Salta la primera línea si parece cabecera (contiene "Nombre" o "Name").
        var startIndex = (lines[0].Contains("Nombre", StringComparison.OrdinalIgnoreCase) || lines[0].Contains("Name", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;

        for (var lineIndex = startIndex; lineIndex < lines.Count; lineIndex++)
        {
            var cols = ParseCsvLine(lines[lineIndex]);
            var rowNum = lineIndex + 1;
            if (cols.Count == 0 || string.IsNullOrWhiteSpace(cols[0]))
            {
                errors.Add($"Fila {rowNum}: falta el nombre, se saltó.");
                continue;
            }

            var name = cols[0].Trim();
            var category = cols.Count > 1 ? cols[1].Trim() : null;
            var quantity = cols.Count > 2 && int.TryParse(cols[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0;
            var unit = cols.Count > 3 && !string.IsNullOrWhiteSpace(cols[3]) ? cols[3].Trim() : "unidad";
            var minStock = cols.Count > 4 && int.TryParse(cols[4], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 0;
            var unitPrice = cols.Count > 5 && decimal.TryParse(cols[5], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0m;
            var status = cols.Count > 6 && !string.IsNullOrWhiteSpace(cols[6]) ? cols[6].Trim() : "Active";

            _context.InventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                AffiliateId = affiliateId,
                Name = name,
                Category = string.IsNullOrWhiteSpace(category) ? null : category,
                Quantity = quantity,
                Unit = unit,
                MinStock = minStock,
                UnitPrice = unitPrice,
                Status = status,
                CreatedAt = now,
            });
            created++;
        }

        if (created > 0)
            await _context.SaveChangesAsync();

        return new InventoryCsvImportResultDto(created, errors.Count, errors);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    public async Task<List<RecipeItemDto>> GetRecipeAsync(Guid affiliateId, Guid productId)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && p.AffiliateId == affiliateId);
        if (product == null) return new List<RecipeItemDto>();

        return await _context.ProductIngredients
            .Where(pi => pi.ProductId == productId)
            .Include(pi => pi.InventoryItem)
            .Where(pi => pi.InventoryItem != null)
            .Select(pi => new RecipeItemDto(pi.InventoryItemId, pi.InventoryItem!.Name, pi.Quantity))
            .ToListAsync();
    }

    public async Task<List<RecipeItemDto>> SetRecipeAsync(Guid affiliateId, Guid productId, List<RecipeItemInput> items)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && p.AffiliateId == affiliateId);
        if (product == null)
            throw new InvalidOperationException("Product not found");

        // Validar que todos los InventoryItem pertenezcan al mismo afiliado — evita que un dueño
        // enlace, sea por error o manipulando el request, un ingrediente de otro negocio.
        var itemIds = items.Select(i => i.InventoryItemId).Distinct().ToList();
        var validIds = await _context.InventoryItems
            .Where(inv => inv.AffiliateId == affiliateId && itemIds.Contains(inv.Id))
            .Select(inv => inv.Id)
            .ToListAsync();
        var invalid = itemIds.Except(validIds).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException("One or more inventory items were not found for this affiliate");

        var existing = await _context.ProductIngredients.Where(pi => pi.ProductId == productId).ToListAsync();
        _context.ProductIngredients.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            _context.ProductIngredients.Add(new ProductIngredient
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                InventoryItemId = item.InventoryItemId,
                Quantity = item.Quantity,
                CreatedAt = now,
            });
        }

        await _context.SaveChangesAsync();
        return await GetRecipeAsync(affiliateId, productId);
    }
}

public class QueueService : IQueueService
{
    private readonly AppDbContext _context;
    private readonly IQueueRealtimeNotifier _realtime;
    private readonly ICustomerService _customerService;

    public QueueService(AppDbContext context, IQueueRealtimeNotifier realtime, ICustomerService customerService)
    {
        _context = context;
        _realtime = realtime;
        _customerService = customerService;
    }

    // Bug real reportado en producción (2026-08-19): "waiting" solo traía a los que esperan
    // turno, así que QueueContent.tsx nunca recibía las entradas "in_service" — la sección
    // "Atendiendo ahora" (con el botón Completar) nunca tenía datos para mostrar, tanto en el
    // fetch inicial como en el broadcast de SignalR (NotifyQueueUpdatedAsync llama a este mismo
    // método). Resultado: al tocar "Llamar" la persona simplemente desaparecía de la pantalla en
    // vez de pasar a "Atendiendo ahora". El frontend ya filtra por status para las dos secciones
    // (waiting/in_service) — solo hacía falta que el backend le mandara ambas.
    public async Task<List<QueueEntry>> GetQueueAsync(Guid affiliateId)
        => await _context.QueueEntries.AsNoTracking()
            .Where(q => q.AffiliateId == affiliateId && (q.Status == "waiting" || q.Status == "in_service"))
            .Include(q => q.Service).Include(q => q.AssignedTo)
            .OrderBy(q => q.Position).ToListAsync();

    public async Task<QueueEntry> AddToQueueAsync(Guid affiliateId, QueueEntry entry)
    {
        var maxPosition = await _context.QueueEntries.Where(q => q.AffiliateId == affiliateId && q.Status == "waiting")
            .MaxAsync(q => (int?)q.Position) ?? 0;

        entry.AffiliateId = affiliateId;
        entry.Id = Guid.NewGuid();
        entry.Position = maxPosition + 1;
        entry.Status = "waiting";
        entry.CreatedAt = DateTime.UtcNow;

        // Tarea #244 — cubre tanto el walk-in público (PublicBookingService) como el que agrega
        // el staff manualmente desde /space/{slug}/queue, ambos pasan por acá. Sin teléfono no
        // hay con qué deduplicar (Phone es opcional en QueueEntry).
        var customer = await _customerService.ResolveOrCreateCustomerAsync(affiliateId, entry.DisplayName, entry.Phone);
        entry.CustomerId = customer?.Id;

        _context.QueueEntries.Add(entry);
        await _context.SaveChangesAsync();
        await _realtime.NotifyQueueUpdatedAsync(affiliateId, await GetQueueAsync(affiliateId));
        return entry;
    }

    public async Task<QueueEntry?> UpdateQueueEntryAsync(Guid affiliateId, Guid id, string status, Guid? barberId = null)
    {
        var entry = await _context.QueueEntries.FirstOrDefaultAsync(q => q.Id == id && q.AffiliateId == affiliateId);
        if (entry == null) return null;
        var previousStatus = entry.Status;
        entry.Status = status;
        if (barberId.HasValue) entry.AssignedToId = barberId;
        if (status == "in_service") entry.CalledAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _realtime.NotifyQueueUpdatedAsync(affiliateId, await GetQueueAsync(affiliateId));

        if (previousStatus != "completed" && entry.Status == "completed" && entry.CustomerId.HasValue)
            await _customerService.MarkVisitCompletedAsync(entry.CustomerId.Value);

        return entry;
    }
}

public class TeamService : ITeamService
{
    private readonly AppDbContext _context;

    public TeamService(AppDbContext context) => _context = context;

    public async Task<List<TeamMember>> GetTeamAsync(Guid affiliateId, string? department = null, string? status = null)
    {
        var query = _context.TeamMembers.Where(t => t.AffiliateId == affiliateId);
        if (!string.IsNullOrEmpty(department)) query = query.Where(t => t.Department == department);
        if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.IsActive.ToString() == status);
        return await query.ToListAsync();
    }

    public async Task<TeamMember?> GetTeamMemberAsync(Guid affiliateId, Guid id)
        => await _context.TeamMembers.FirstOrDefaultAsync(t => t.Id == id && t.AffiliateId == affiliateId);

    public async Task<TeamMember> CreateTeamMemberAsync(Guid affiliateId, TeamMember member)
    {
        member.AffiliateId = affiliateId;
        member.Id = Guid.NewGuid();
        member.CreatedAt = DateTime.UtcNow;
        _context.TeamMembers.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    public async Task<TeamMember?> UpdateTeamMemberAsync(Guid affiliateId, Guid id, TeamMember member)
    {
        var existing = await _context.TeamMembers.FirstOrDefaultAsync(t => t.Id == id && t.AffiliateId == affiliateId);
        if (existing == null) return null;
        existing.Name = member.Name;
        existing.Email = member.Email;
        existing.Phone = member.Phone;
        existing.Role = member.Role;
        existing.Department = member.Department;
        existing.IsActive = member.IsActive;
        existing.PhotoUrl = member.PhotoUrl;
        existing.HourlyRate = member.HourlyRate;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteTeamMemberAsync(Guid affiliateId, Guid id)
    {
        var member = await _context.TeamMembers.FirstOrDefaultAsync(t => t.Id == id && t.AffiliateId == affiliateId);
        if (member == null) return false;
        _context.TeamMembers.Remove(member);
        await _context.SaveChangesAsync();
        return true;
    }
}

/// <summary>
/// Ponche de entrada/salida (kiosko público por PIN) + corrección manual + reporte de nómina.
/// El PIN es la única "autenticación" del ponche público — no hay login de empleado, mismo
/// criterio de simplicidad que el resto de los flujos públicos del proyecto.
/// </summary>
public class TimeClockService : ITimeClockService
{
    private readonly AppDbContext _context;

    public TimeClockService(AppDbContext context) => _context = context;

    public async Task<List<PonchePickerMemberDto>?> GetPonchePickerAsync(string slug)
    {
        var affiliate = await _context.Affiliates
            .Where(a => a.Slug == slug)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();
        if (affiliate is null) return null;

        return await _context.TeamMembers
            .Where(t => t.AffiliateId == affiliate.Id && t.IsActive && t.PinCode != null)
            .OrderBy(t => t.Name)
            .Select(t => new PonchePickerMemberDto(t.Id, t.Name, t.PhotoUrl))
            .ToListAsync();
    }

    public async Task<ClockResultDto> ClockAsync(string slug, Guid teamMemberId, string pin)
    {
        var affiliate = await _context.Affiliates
            .Where(a => a.Slug == slug)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();
        if (affiliate is null)
            throw new KeyNotFoundException();

        var member = await _context.TeamMembers
            .FirstOrDefaultAsync(t => t.Id == teamMemberId && t.AffiliateId == affiliate.Id && t.IsActive);
        if (member is null)
            throw new KeyNotFoundException();
        if (string.IsNullOrEmpty(member.PinCode) || member.PinCode != pin)
            throw new ArgumentException("PIN incorrecto.");

        var openEntry = await _context.TimeEntries
            .Where(e => e.TeamMemberId == teamMemberId && e.AffiliateId == affiliate.Id && e.ClockOut == null)
            .OrderByDescending(e => e.ClockIn)
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow;

        if (openEntry is not null)
        {
            openEntry.ClockOut = now;
            await _context.SaveChangesAsync();
            var hours = Math.Round((decimal)(now - openEntry.ClockIn).TotalHours, 2);
            return new ClockResultDto("ClockedOut", now, member.Name, hours);
        }

        var entry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliate.Id,
            TeamMemberId = teamMemberId,
            ClockIn = now,
            Source = "Kiosk",
            CreatedAt = now,
        };
        _context.TimeEntries.Add(entry);
        await _context.SaveChangesAsync();
        return new ClockResultDto("ClockedIn", now, member.Name, null);
    }

    public async Task<List<TimeEntry>> GetTimeEntriesAsync(Guid affiliateId, Guid? teamMemberId, DateTime? from, DateTime? to)
    {
        var query = _context.TimeEntries.AsNoTracking()
            .Include(e => e.TeamMember)
            .Where(e => e.AffiliateId == affiliateId);
        if (teamMemberId.HasValue) query = query.Where(e => e.TeamMemberId == teamMemberId.Value);
        if (from.HasValue) query = query.Where(e => e.ClockIn >= from.Value);
        if (to.HasValue) query = query.Where(e => e.ClockIn <= to.Value);
        return await query.OrderByDescending(e => e.ClockIn).ToListAsync();
    }

    public async Task<TimeEntry?> UpdateTimeEntryAsync(Guid affiliateId, Guid id, UpdateTimeEntryRequest request)
    {
        var entry = await _context.TimeEntries.FirstOrDefaultAsync(e => e.Id == id && e.AffiliateId == affiliateId);
        if (entry is null) return null;
        entry.ClockIn = DateTime.SpecifyKind(request.ClockIn, DateTimeKind.Utc);
        entry.ClockOut = request.ClockOut.HasValue ? DateTime.SpecifyKind(request.ClockOut.Value, DateTimeKind.Utc) : null;
        entry.Notes = request.Notes;
        entry.Source = "Manual";
        entry.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<bool> DeleteTimeEntryAsync(Guid affiliateId, Guid id)
    {
        var entry = await _context.TimeEntries.FirstOrDefaultAsync(e => e.Id == id && e.AffiliateId == affiliateId);
        if (entry is null) return false;
        _context.TimeEntries.Remove(entry);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PayrollReportDto> GetPayrollAsync(Guid affiliateId, DateTime from, DateTime to)
    {
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        // Solo turnos cerrados (ClockOut != null) — un turno abierto todavía no tiene horas
        // definitivas que pagar, se cuenta en el próximo período una vez se cierre.
        var entries = await _context.TimeEntries.AsNoTracking()
            .Include(e => e.TeamMember)
            .Where(e => e.AffiliateId == affiliateId && e.ClockOut != null && e.ClockIn >= fromUtc && e.ClockIn <= toUtc)
            .ToListAsync();

        // Agrupa por TeamMemberId (no por el objeto TeamMember): con AsNoTracking() cada fila
        // trae su propia instancia de TeamMember sin resolución de identidad, así que agrupar
        // por referencia de objeto partiría erróneamente a un mismo empleado en varios grupos.
        var members = entries
            .Where(e => e.TeamMember is not null)
            .GroupBy(e => e.TeamMemberId)
            .Select(g =>
            {
                var teamMember = g.First().TeamMember!;
                var totalHours = Math.Round((decimal)g.Sum(e => (e.ClockOut!.Value - e.ClockIn).TotalHours), 2);
                var totalPay = teamMember.HourlyRate.HasValue ? Math.Round(totalHours * teamMember.HourlyRate.Value, 2) : (decimal?)null;
                return new PayrollMemberDto(teamMember.Id, teamMember.Name, teamMember.HourlyRate, totalHours, totalPay);
            })
            .OrderBy(m => m.Name)
            .ToList();

        var grandTotal = members.Sum(m => m.TotalPay ?? 0);
        return new PayrollReportDto(fromUtc, toUtc, members, grandTotal);
    }

    public async Task<string> RegeneratePinAsync(Guid affiliateId, Guid teamMemberId)
    {
        var member = await _context.TeamMembers.FirstOrDefaultAsync(t => t.Id == teamMemberId && t.AffiliateId == affiliateId);
        if (member is null) throw new KeyNotFoundException();
        var pin = Random.Shared.Next(0, 10000).ToString("D4");
        member.PinCode = pin;
        member.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return pin;
    }
}

/// <summary>Tareas asignadas a miembros del equipo — tablero simple de tres estados.</summary>
public class StaffTaskService : IStaffTaskService
{
    private readonly AppDbContext _context;

    public StaffTaskService(AppDbContext context) => _context = context;

    public async Task<List<StaffTask>> GetTasksAsync(Guid affiliateId, Guid? teamMemberId, string? status)
    {
        var query = _context.StaffTasks.AsNoTracking()
            .Include(t => t.TeamMember)
            .Where(t => t.AffiliateId == affiliateId);
        if (teamMemberId.HasValue) query = query.Where(t => t.TeamMemberId == teamMemberId.Value);
        if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status == status);
        return await query.OrderBy(t => t.Status).ThenBy(t => t.DueDate).ToListAsync();
    }

    public async Task<StaffTask> CreateTaskAsync(Guid affiliateId, StaffTaskRequest request)
    {
        var task = new StaffTask
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            Title = request.Title,
            Description = request.Description,
            TeamMemberId = request.TeamMemberId,
            DueDate = request.DueDate,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
        };
        _context.StaffTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<StaffTask?> UpdateTaskAsync(Guid affiliateId, Guid id, StaffTaskRequest request)
    {
        var task = await _context.StaffTasks.FirstOrDefaultAsync(t => t.Id == id && t.AffiliateId == affiliateId);
        if (task is null) return null;
        task.Title = request.Title;
        task.Description = request.Description;
        task.TeamMemberId = request.TeamMemberId;
        task.DueDate = request.DueDate;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<StaffTask?> UpdateTaskStatusAsync(Guid affiliateId, Guid id, string status)
    {
        if (status is not ("Pending" or "InProgress" or "Done"))
            throw new ArgumentException("Estado inválido.");
        var task = await _context.StaffTasks.FirstOrDefaultAsync(t => t.Id == id && t.AffiliateId == affiliateId);
        if (task is null) return null;
        task.Status = status;
        task.CompletedAt = status == "Done" ? DateTime.UtcNow : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<bool> DeleteTaskAsync(Guid affiliateId, Guid id)
    {
        var task = await _context.StaffTasks.FirstOrDefaultAsync(t => t.Id == id && t.AffiliateId == affiliateId);
        if (task is null) return false;
        _context.StaffTasks.Remove(task);
        await _context.SaveChangesAsync();
        return true;
    }
}

public class ProductService : IProductService
{
    private readonly AppDbContext _context;

    public ProductService(AppDbContext context) => _context = context;

    public async Task<PaginatedResponse<Product>> GetProductsAsync(Guid affiliateId, string? category = null, string? status = null)
    {
        var query = _context.Products.Where(p => p.AffiliateId == affiliateId);
        if (!string.IsNullOrEmpty(category)) query = query.Where(p => p.Category == category);
        if (!string.IsNullOrEmpty(status)) query = query.Where(p => p.Status == status);
        var total = await query.CountAsync();
        var data = await query.OrderBy(p => p.Name).ToListAsync();
        return new PaginatedResponse<Product> { Data = data, Total = total, Page = 1, TotalPages = 1 };
    }

    public async Task<Product?> GetProductAsync(Guid affiliateId, Guid id)
        => await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.AffiliateId == affiliateId);

    public async Task<Product> CreateProductAsync(Guid affiliateId, Product product)
    {
        product.AffiliateId = affiliateId;
        product.Id = Guid.NewGuid();
        product.CreatedAt = DateTime.UtcNow;
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<Product?> UpdateProductAsync(Guid affiliateId, Guid id, Product product)
    {
        var existing = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.AffiliateId == affiliateId);
        if (existing == null) return null;
        existing.Name = product.Name;
        existing.Description = product.Description;
        existing.Category = product.Category;
        existing.Price = product.Price;
        existing.Stock = product.Stock;
        existing.ImageUrl = product.ImageUrl;
        existing.Status = product.Status;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteProductAsync(Guid affiliateId, Guid id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.AffiliateId == affiliateId);
        if (product == null) return false;
        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }
}

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _context;
    private readonly IInvoiceNotificationService _notifications;

    public InvoiceService(AppDbContext context, IInvoiceNotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    public async Task<PaginatedResponse<Invoice>> GetInvoicesAsync(Guid affiliateId, string? status = null, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        var baseQuery = _context.Invoices.Where(i => i.AffiliateId == affiliateId);
        
        IQueryable<Invoice> query = baseQuery.Include(i => i.Customer);

        if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.Status == status);
        if (dateFrom.HasValue) query = query.Where(i => i.IssueDate >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(i => i.IssueDate <= dateTo.Value);
        
        var total = await query.CountAsync();
        var data = await query.OrderByDescending(i => i.IssueDate).ToListAsync();
        return new PaginatedResponse<Invoice> { Data = data, Total = total, Page = 1, TotalPages = 1 };
    }

    public async Task<Invoice?> GetInvoiceAsync(Guid affiliateId, Guid id)
        => await _context.Invoices.Include(i => i.Customer).Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);

    public async Task<Invoice> CreateInvoiceAsync(Guid affiliateId, Invoice invoice, List<InvoiceItem>? items = null)
    {
        invoice.AffiliateId = affiliateId;
        invoice.Id = Guid.NewGuid();
        invoice.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";
        invoice.IssueDate = invoice.IssueDate == default ? DateTime.UtcNow : invoice.IssueDate;
        invoice.CreatedAt = DateTime.UtcNow;

        // Mismo motivo de siempre (Appointment.Date, TableReservation.Date, Proposal.ExpiresAt): el
        // <input type="date"> del front manda una fecha "bare" (sin hora/zona) que deserializa con
        // DateTimeKind.Unspecified, y la columna es timestamptz -- Npgsql rechaza escribir ahí un
        // DateTime Unspecified y el guardado fallaba en silencio cada vez que se ponía vencimiento.
        if (invoice.DueDate.HasValue)
            invoice.DueDate = DateTime.SpecifyKind(invoice.DueDate.Value, DateTimeKind.Utc);
        if (invoice.PaidDate.HasValue)
            invoice.PaidDate = DateTime.SpecifyKind(invoice.PaidDate.Value, DateTimeKind.Utc);

        // Las líneas llegan sueltas (sin InvoiceId todavía, el cliente no lo conoce hasta que
        // se crea la factura) — el total nunca se confía al request, se recalcula acá para que
        // no se pueda mandar un Total arbitrario que no cuadre con las líneas reales.
        if (items is { Count: > 0 })
        {
            invoice.Items = items.Select(i => new InvoiceItem
            {
                Id = Guid.NewGuid(),
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Total = i.Quantity * i.UnitPrice,
            }).ToList();
            invoice.Subtotal = invoice.Items.Sum(i => i.Total);
            invoice.Total = invoice.Subtotal + invoice.Tax;
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    public async Task<Invoice?> UpdateInvoiceAsync(Guid affiliateId, Guid id, Invoice invoice)
    {
        var existing = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);
        if (existing == null) return null;
        existing.CustomerId = invoice.CustomerId;
        existing.Subtotal = invoice.Subtotal;
        existing.Tax = invoice.Tax;
        existing.Total = invoice.Total;
        existing.Status = invoice.Status;
        existing.DueDate = invoice.DueDate.HasValue ? DateTime.SpecifyKind(invoice.DueDate.Value, DateTimeKind.Utc) : null;
        existing.PaidDate = invoice.PaidDate.HasValue ? DateTime.SpecifyKind(invoice.PaidDate.Value, DateTimeKind.Utc) : null;
        existing.Notes = invoice.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteInvoiceAsync(Guid affiliateId, Guid id)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);
        if (invoice == null) return false;
        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync();
        return true;
    }

    // Mismo patrón que OrderService.CreatePosCheckoutAsync: Checkout Session en modo "payment",
    // ejecutada con el header Stripe-Account de la cuenta conectada del afiliado (direct charge —
    // el dinero entra directo a la cuenta del afiliado, MaalCa nunca la toca). Las líneas salen de
    // Invoice.Items (ya recalculados y persistidos en CreateInvoiceAsync), nunca de un total suelto
    // que mande el cliente.
    public async Task<string?> CreateInvoiceCheckoutAsync(Guid affiliateId, Guid invoiceId, string successUrl, string cancelUrl)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Affiliate)
            .Include(i => i.Customer)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.AffiliateId == affiliateId);
        if (invoice is null || invoice.Affiliate is null) return null;

        var affiliate = invoice.Affiliate;
        if (!affiliate.StripeConnectChargesEnabled || string.IsNullOrEmpty(affiliate.StripeConnectAccountId))
            throw new InvalidOperationException("Conecta Stripe en Configuración antes de cobrar esta factura.");

        var currency = string.IsNullOrWhiteSpace(affiliate.Currency) ? "USD" : affiliate.Currency.ToUpperInvariant();

        Stripe.StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var requestOptions = new Stripe.RequestOptions { StripeAccount = affiliate.StripeConnectAccountId };

        List<Stripe.Checkout.SessionLineItemOptions> lineItems;
        if (invoice.Items is { Count: > 0 })
        {
            lineItems = invoice.Items.Select(i => new Stripe.Checkout.SessionLineItemOptions
            {
                Quantity = i.Quantity,
                PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                {
                    Currency = currency.ToLowerInvariant(),
                    UnitAmount = (long)Math.Round(i.UnitPrice * 100),
                    ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions { Name = i.Description },
                },
            }).ToList();

            if (invoice.Tax > 0)
            {
                lineItems.Add(new Stripe.Checkout.SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmount = (long)Math.Round(invoice.Tax * 100),
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions { Name = "Tax" },
                    },
                });
            }
        }
        else
        {
            // Factura sin líneas detalladas (dato viejo o editada a mano) — un solo renglón con el
            // Total ya calculado, para no dejar el cobro sin poder generarse.
            lineItems = new List<Stripe.Checkout.SessionLineItemOptions>
            {
                new Stripe.Checkout.SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmount = (long)Math.Round(invoice.Total * 100),
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions { Name = $"Factura {invoice.InvoiceNumber}" },
                    },
                },
            };
        }

        var session = await new Stripe.Checkout.SessionService().CreateAsync(new Stripe.Checkout.SessionCreateOptions
        {
            Mode = "payment",
            LineItems = lineItems,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = invoice.Id.ToString(),
        }, requestOptions);

        invoice.StripeCheckoutSessionId = session.Id;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (invoice.Customer is not null)
            await _notifications.NotifyInvoicePaymentLinkAsync(invoice, invoice.Customer, affiliate.Name, currency, session.Url);

        return session.Url;
    }

    public async Task ConfirmFromWebhookAsync(string checkoutSessionId, string? paymentIntentId)
    {
        // Busca por StripeCheckoutSessionId, no por Id de factura — el webhook solo trae el id de
        // la Session de Stripe (ver StripeConnectService.HandleWebhookEventAsync).
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.StripeCheckoutSessionId == checkoutSessionId);
        // Solo actúa si sigue pendiente de cobro — evita pisar un "Marcar pagada" manual ya
        // aplicado, o reprocesar una factura ya cancelada.
        if (invoice is null || (invoice.Status != "Pending" && invoice.Status != "Overdue")) return;

        invoice.Status = "Paid";
        invoice.PaidDate = DateTime.UtcNow;
        invoice.StripePaymentIntentId = paymentIntentId;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

public class MetricsService : IMetricsService
{
    private readonly AppDbContext _context;

    public MetricsService(AppDbContext context) => _context = context;

    public async Task<object> GetMetricsAsync(Guid affiliateId)
    {
        var revenue = await _context.Invoices.Where(i => i.AffiliateId == affiliateId && i.Status == "Paid").SumAsync(i => i.Total);
        var appointments = await _context.Appointments.CountAsync(a => a.AffiliateId == affiliateId);
        var customers = await _context.Customers.CountAsync(c => c.AffiliateId == affiliateId);
        var inventoryValue = await _context.InventoryItems.Where(i => i.AffiliateId == affiliateId).SumAsync(i => i.Quantity * i.UnitPrice);
        var queueLength = await _context.QueueEntries.CountAsync(q => q.AffiliateId == affiliateId && q.Status == "waiting");

        return new { revenue, appointments, customers, inventoryValue, queueLength };
    }
}

public class LeadService : ILeadService
{
    private readonly AppDbContext _context;

    public LeadService(AppDbContext context) => _context = context;

    public async Task<object> GetOverviewMetricsAsync()
    {
        var activeProjects = await _context.Affiliates.CountAsync(a => a.IsActive);
        var collaborators = await _context.TeamMembers.CountAsync();
        var customers = await _context.Customers.CountAsync();
        return new { activeProjects, collaborators, customers, yearsExperience = 5 };
    }

    public async Task<Lead> CreatePropertyLeadAsync(Lead lead)
    {
        lead.Id = Guid.NewGuid();
        lead.Source = "properties";
        lead.CreatedAt = DateTime.UtcNow;
        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead> CreateCirisonicLeadAsync(Lead lead)
    {
        lead.Id = Guid.NewGuid();
        lead.Source = "cirisonic";
        lead.CreatedAt = DateTime.UtcNow;
        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        return lead;
    }
}
