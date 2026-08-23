using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>
/// Reserva pública (sin login) para el widget de agenda en las plantillas públicas. Mismo
/// patrón que OrderService: resuelve el afiliado por slug + Published, valida con
/// ArgumentException (el endpoint en Program.cs lo convierte en 400), null = afiliado no
/// existe/no publicado (el endpoint lo convierte en 404).
///
/// Prevención de doble-booking: un mismo miembro del personal no puede tener dos citas
/// (no canceladas) en la misma fecha+hora. Sin AssignedToId no hay conflicto que chequear —
/// el negocio decide luego quién la atiende.
/// </summary>
public class PublicBookingService : IPublicBookingService
{
    private readonly AppDbContext _db;
    private readonly IQueueService _queueService;
    private readonly ICustomerService _customerService;
    private readonly IAppointmentNotificationService _appointmentNotifications;

    public PublicBookingService(AppDbContext db, IQueueService queueService, ICustomerService customerService, IAppointmentNotificationService appointmentNotifications)
    {
        _db = db;
        _queueService = queueService;
        _customerService = customerService;
        _appointmentNotifications = appointmentNotifications;
    }

    public async Task<List<PublicTeamMemberDto>?> GetPublicTeamAsync(string affiliateSlug)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == affiliateSlug && a.Published)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();
        if (affiliate is null) return null;

        return await _db.TeamMembers
            .Where(t => t.AffiliateId == affiliate.Id && t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new PublicTeamMemberDto(t.Id, t.Name, t.Role, t.PhotoUrl))
            .ToListAsync();
    }

    public async Task<List<PublicServiceDto>?> GetPublicServicesAsync(string affiliateSlug)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == affiliateSlug && a.Published)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();
        if (affiliate is null) return null;

        return await _db.Services
            .Where(s => s.AffiliateId == affiliate.Id && s.IsActive && s.Status == "Active")
            .OrderBy(s => s.SortOrder)
            // Agenda necesita un número para calcular slots aunque el dueño no haya fijado
            // duración en el catálogo (donde null = oculto) — 30 min es el mismo fallback
            // que ya se usaba como default histórico. No afecta lo que se guarda en Service.
            .Select(s => new PublicServiceDto(s.Id, s.Name, s.Description, s.Price, s.DurationMinutes ?? 30, s.Modality.ToString()))
            .ToListAsync();
    }

    /// <summary>
    /// Task #189 — el front pedía esto recién al confirmar (409 "Ese horario ya no está
    /// disponible"), obligando al cliente a rellenar todo el form para descubrir el choque.
    /// Ahora el front puede pedir esto apenas se elige fecha+profesional y ocultar los slots
    /// ya tomados del grid de horas, igual que ya oculta los que caen fuera del horario del
    /// negocio (generateTimeSlots).
    /// </summary>
    public async Task<PublicBusyTimesDto?> GetPublicBusyTimesAsync(string affiliateSlug, DateTime date)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == affiliateSlug && a.Published)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();
        if (affiliate is null) return null;

        // Misma razón de siempre: la fecha "bare" que llega por query string deserializa con
        // Kind=Unspecified y la columna es timestamptz.
        var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        var busy = await _db.Appointments
            .Where(a =>
                a.AffiliateId == affiliate.Id &&
                a.Date.Date == dateUtc &&
                a.Status != "Cancelled" &&
                a.AssignedToId != null)
            .Select(a => new { StaffId = a.AssignedToId!.Value, a.Time })
            .ToListAsync();

        var busyByStaff = busy
            .GroupBy(a => a.StaffId)
            .ToDictionary(g => g.Key.ToString(), g => g.Select(a => a.Time).Distinct().ToList());

        // Task #192 — bloqueos manuales de horario (almuerzo, ausencias) se mezclan en el mismo
        // dict de "ocupado" que las citas reales, para que el front no tenga que saber que
        // existen dos fuentes distintas de indisponibilidad — un slot bloqueado se ve exactamente
        // igual que uno con cita ya agendada.
        var blocks = await _db.TimeBlocks
            .Where(b => b.AffiliateId == affiliate.Id && b.Date.Date == dateUtc)
            .Select(b => new { b.StaffId, b.StartTime, b.EndTime })
            .ToListAsync();

        if (blocks.Count > 0)
        {
            var staffWideBlocks = blocks.Where(b => b.StaffId is null).ToList();
            List<Guid> allStaffIds = new();
            if (staffWideBlocks.Count > 0)
            {
                allStaffIds = await _db.TeamMembers
                    .Where(t => t.AffiliateId == affiliate.Id && t.IsActive)
                    .Select(t => t.Id)
                    .ToListAsync();
            }

            foreach (var block in blocks)
            {
                var slots = ExpandBlockToSlots(block.StartTime, block.EndTime);
                var targetStaffIds = block.StaffId.HasValue ? new List<Guid> { block.StaffId.Value } : allStaffIds;
                foreach (var staffId in targetStaffIds)
                {
                    var key = staffId.ToString();
                    if (!busyByStaff.TryGetValue(key, out var list))
                    {
                        list = new List<string>();
                        busyByStaff[key] = list;
                    }
                    foreach (var slot in slots)
                        if (!list.Contains(slot)) list.Add(slot);
                }
            }
        }

        return new PublicBusyTimesDto(busyByStaff);
    }

    // Convierte un rango HH:mm-HH:mm en la misma grilla de 30 min que usa generateTimeSlots()
    // del lado del front, para que un bloqueo de "12:00 a 13:00" tache exactamente los slots
    // "12:00" y "12:30" — sin esto, comparar el rango contra un solo string de hora exacta
    // dejaría pasar reservas que caen dentro del bloqueo pero no coinciden con StartTime.
    private static List<string> ExpandBlockToSlots(string startTime, string endTime)
    {
        var slots = new List<string>();
        if (!TryParseHm(startTime, out var startMins) || !TryParseHm(endTime, out var endMins))
            return slots;
        for (var mins = startMins - (startMins % 30); mins < endMins; mins += 30)
        {
            if (mins < 0) continue;
            var h = mins / 60;
            var m = mins % 60;
            slots.Add($"{h:D2}:{m:D2}");
        }
        return slots;
    }

    private static bool TryParseHm(string value, out int minutes)
    {
        minutes = 0;
        var parts = value.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
        minutes = h * 60 + m;
        return true;
    }

    public async Task<PublicAppointmentResultDto> CreatePublicAppointmentAsync(string affiliateSlug, CreatePublicAppointmentRequest request)
    {
        var affiliate = await _db.Affiliates
            .FirstOrDefaultAsync(a => a.Slug == affiliateSlug && a.Published);
        if (affiliate is null)
            throw new KeyNotFoundException();

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("El nombre es requerido.");
        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            throw new ArgumentException("El teléfono es requerido.");
        if (string.IsNullOrWhiteSpace(request.Time))
            throw new ArgumentException("La hora es requerida.");
        if (request.Date.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("La fecha no puede ser en el pasado.");

        var service = await _db.Services
            .FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.AffiliateId == affiliate.Id && s.IsActive);
        if (service is null)
            throw new ArgumentException("Servicio no encontrado.");

        // Tarea #405 — la modalidad real de la cita nunca se confía ciegamente al cliente: se
        // deriva de Service.Modality. Solo cuando el servicio admite ambas (Both) el cliente
        // decide (WantsVirtual); si el servicio es puramente InPerson o Virtual, se fuerza ese
        // valor server-side sin importar lo que haya mandado el front.
        var isVirtual = service.Modality switch
        {
            Domain.Enums.ServiceModality.Virtual => true,
            Domain.Enums.ServiceModality.InPerson => false,
            Domain.Enums.ServiceModality.Both => request.WantsVirtual ?? false,
            _ => false,
        };
        if (isVirtual && string.IsNullOrWhiteSpace(affiliate.ZoomLink))
            throw new ArgumentException("Este negocio todavía no configuró su link de reuniones virtuales.");

        if (request.AssignedToId is Guid assignedToId)
        {
            var validStaff = await _db.TeamMembers
                .AnyAsync(t => t.Id == assignedToId && t.AffiliateId == affiliate.Id && t.IsActive);
            if (!validStaff)
                throw new ArgumentException("El miembro del personal seleccionado no está disponible.");

            // request.Date llega con Kind=Unspecified (fecha "bare" del front) — Npgsql 8+ no
            // permite compararlo contra la columna timestamptz sin forzar Kind=Utc primero,
            // igual que en el Create de abajo. Sin esto el chequeo de conflicto tronaba con
            // ArgumentException y CADA reserva pública de cita devolvía 400. Bug real reportado
            // en producción 2026-08-17 — bloqueaba toda la Agenda pública (Barbería/Servicios).
            var requestDateUtc = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);
            var conflict = await _db.Appointments.AnyAsync(a =>
                a.AffiliateId == affiliate.Id &&
                a.AssignedToId == assignedToId &&
                a.Date.Date == requestDateUtc &&
                a.Time == request.Time &&
                a.Status != "Cancelled");
            if (conflict)
                throw new InvalidOperationException("Ese horario ya no está disponible — elige otro.");
        }

        // Reusa el Customer si ya existe uno con el mismo teléfono para este afiliado (evita
        // duplicar clientes recurrentes que reservan varias veces), si no lo crea. Tarea #244:
        // ahora centralizado en CustomerService, reusado también por Queue/Reservations/Proposals.
        var customer = await _customerService.ResolveOrCreateCustomerAsync(affiliate.Id, request.CustomerName, request.CustomerPhone);

        var appointment = new Appointment
        {
            AffiliateId = affiliate.Id,
            CustomerId = customer!.Id,
            ServiceId = service.Id,
            AssignedToId = request.AssignedToId,
            // El front manda una fecha "bare" (yyyy-MM-dd, sin hora/offset), que System.Text.Json
            // deserializa con Kind=Unspecified. La columna es timestamp with time zone y Npgsql 8+
            // rechaza escribir un DateTime Unspecified ahí (throws en SaveChangesAsync). Como esto
            // es una fecha de calendario, no un instante real, forzamos Kind=Utc explícitamente en
            // vez de cambiar el tipo de columna.
            Date = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc),
            Time = request.Time,
            Status = "Scheduled",
            Notes = request.Notes,
            IsVirtual = isVirtual,
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        // Tarea #247 — si el visitante dejó su email en el widget público, se le envía la
        // confirmación con el link de autogestión. CustomerEmail no se guarda en la cita (el
        // dato "oficial" de contacto vive en Customer) — se lee del request y, si Customer no
        // tenía email todavía, se lo completamos ahora (no pisa uno que ya existía).
        if (!string.IsNullOrWhiteSpace(request.CustomerEmail) && string.IsNullOrWhiteSpace(customer.Email))
        {
            customer.Email = request.CustomerEmail;
            await _db.SaveChangesAsync();
        }

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            string? staffName = null;
            if (request.AssignedToId is Guid staffId)
                staffName = (await _db.TeamMembers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == staffId))?.Name;

            // Se espera (no fire-and-forget) — tarea #135 ya mostró que un fire-and-forget se
            // corta a mitad de camino en el runtime serverless de Railway. El servicio de abajo
            // ya atrapa sus propios errores y nunca tumba la reserva si el correo falla.
            await _appointmentNotifications.NotifyAppointmentBookedAsync(appointment, customer, affiliate.Name, affiliateSlug, service.Name, staffName, affiliate.ZoomLink);
        }

        return new PublicAppointmentResultDto(appointment.Id, appointment.Date, appointment.Time, appointment.Status, appointment.Token, appointment.IsVirtual, isVirtual ? affiliate.ZoomLink : null);
    }

    public async Task<PublicTableReservationResultDto> CreatePublicTableReservationAsync(string affiliateSlug, CreatePublicTableReservationRequest request)
    {
        var affiliate = await _db.Affiliates
            .FirstOrDefaultAsync(a => a.Slug == affiliateSlug && a.Published);
        if (affiliate is null)
            throw new KeyNotFoundException();

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("El nombre es requerido.");
        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            throw new ArgumentException("El teléfono es requerido.");
        if (string.IsNullOrWhiteSpace(request.Time))
            throw new ArgumentException("La hora es requerida.");
        if (request.Date.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("La fecha no puede ser en el pasado.");
        if (request.PartySize < 1)
            throw new ArgumentException("El número de personas debe ser al menos 1.");

        // Tarea #244 — mismo dedup por teléfono que Appointment, para que un comensal recurrente
        // acumule sus reservas en el mismo Customer que sus citas/visitas a la fila.
        var reservationCustomer = await _customerService.ResolveOrCreateCustomerAsync(affiliate.Id, request.CustomerName, request.CustomerPhone);

        var reservation = new TableReservation
        {
            AffiliateId = affiliate.Id,
            CustomerId = reservationCustomer?.Id,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerEmail = request.CustomerEmail,
            // Misma razón que en CreatePublicAppointmentAsync — fecha "bare" deserializa
            // Kind=Unspecified, la columna es timestamptz.
            Date = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc),
            Time = request.Time,
            PartySize = request.PartySize,
            Status = "Requested",
            Notes = request.Notes,
        };
        _db.TableReservations.Add(reservation);
        await _db.SaveChangesAsync();

        return new PublicTableReservationResultDto(reservation.Id, reservation.Date, reservation.Time, reservation.PartySize, reservation.Status);
    }

    public async Task<PublicQueueEntryResultDto> CreatePublicQueueEntryAsync(string affiliateSlug, CreatePublicQueueEntryRequest request)
    {
        // AsNoTracking() — bug real en producción (2026-08-17): sin esto, este Affiliate queda
        // trackeado en el mismo AppDbContext donde QueueService.AddToQueueAsync hace
        // _context.QueueEntries.Add(entry); EF hace fix-up automático (entry.Affiliate = affiliate,
        // affiliate.QueueEntries incluye entry) creando un ciclo vivo. GetQueueAsync().Include(Service)
        // hace que ese QueueEntry (con Service.Affiliate poblado indirectamente) se mande tal cual
        // por SignalR a NotifyQueueUpdatedAsync — que a diferencia del pipeline HTTP normal (protegido
        // por ReferenceHandler.IgnoreCycles en Program.cs) usa el serializador default de SignalR, que
        // no ignora ciclos y tira JsonException a mitad de request → 500 sin body, "no pudimos
        // agregarte a la fila" genérico en el front. Los demás métodos públicos de este archivo ya
        // proyectan a DTOs (nunca cargan la entidad completa) por la misma razón.
        var affiliate = await _db.Affiliates
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == affiliateSlug && a.Published);
        if (affiliate is null)
            throw new KeyNotFoundException();

        // La Fila hoy solo existe para Barbería (módulo "queue", ver ModuleCatalog) — un walk-in
        // público de otro tipo de negocio no tendría dónde aterrizar en el dashboard.
        if (affiliate.BusinessType != Domain.Enums.BusinessType.Barber)
            throw new ArgumentException("La fila de espera solo está disponible para barbería.");

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("El nombre es requerido.");

        if (request.ServiceId is Guid serviceId)
        {
            var validService = await _db.Services
                .AnyAsync(s => s.Id == serviceId && s.AffiliateId == affiliate.Id && s.IsActive);
            if (!validService)
                throw new ArgumentException("Servicio no encontrado.");
        }

        // Reusa AddToQueueAsync (mismo cálculo de Position + notificación realtime al dashboard)
        // en vez de duplicar la lógica — el walk-in público y el que agrega el staff manualmente
        // desde /space/{slug}/queue terminan en la misma fila, mismo orden.
        var entry = await _queueService.AddToQueueAsync(affiliate.Id, new QueueEntry
        {
            DisplayName = request.CustomerName.Trim(),
            Phone = request.CustomerPhone?.Trim(),
            ServiceId = request.ServiceId,
            Notes = request.Notes,
            Channel = "web",
        });

        return new PublicQueueEntryResultDto(entry.Id, entry.Position, entry.Status);
    }

    // ── Tarea #246: "gestiona tu cita" sin login, por Token ──────────────────────────────
    // Mismo criterio que el resto de este archivo: AsNoTracking + proyección a DTO (nunca la
    // entidad completa), por la misma razón de ciclos de serialización documentada arriba.

    public async Task<PublicAppointmentManageDto?> GetPublicAppointmentByTokenAsync(Guid token)
    {
        var appt = await _db.Appointments.AsNoTracking()
            .Include(a => a.Affiliate).Include(a => a.Service).Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(a => a.Token == token);
        if (appt is null) return null;

        return new PublicAppointmentManageDto(
            appt.Token, appt.Affiliate?.Name ?? "", appt.Service?.Name ?? "",
            appt.AssignedTo?.Name, appt.Date, appt.Time, appt.Status,
            appt.IsVirtual, appt.IsVirtual ? appt.Affiliate?.ZoomLink : null);
    }

    public async Task<PublicAppointmentManageDto> ConfirmPublicAppointmentAsync(Guid token)
    {
        var appt = await _db.Appointments.Include(a => a.Affiliate).Include(a => a.Service).Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(a => a.Token == token);
        if (appt is null) throw new KeyNotFoundException();
        if (appt.Status != "Scheduled")
            throw new InvalidOperationException("Esta cita ya no se puede confirmar.");

        appt.Status = "Confirmed";
        appt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new PublicAppointmentManageDto(appt.Token, appt.Affiliate?.Name ?? "", appt.Service?.Name ?? "", appt.AssignedTo?.Name, appt.Date, appt.Time, appt.Status, appt.IsVirtual, appt.IsVirtual ? appt.Affiliate?.ZoomLink : null);
    }

    public async Task<PublicAppointmentManageDto> CancelPublicAppointmentAsync(Guid token)
    {
        var appt = await _db.Appointments.Include(a => a.Affiliate).Include(a => a.Service).Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(a => a.Token == token);
        if (appt is null) throw new KeyNotFoundException();
        if (appt.Status is "Completed" or "Cancelled")
            throw new InvalidOperationException("Esta cita ya no se puede cancelar.");

        appt.Status = "Cancelled";
        appt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new PublicAppointmentManageDto(appt.Token, appt.Affiliate?.Name ?? "", appt.Service?.Name ?? "", appt.AssignedTo?.Name, appt.Date, appt.Time, appt.Status, appt.IsVirtual, appt.IsVirtual ? appt.Affiliate?.ZoomLink : null);
    }

    public async Task<PublicAppointmentManageDto> ReschedulePublicAppointmentAsync(Guid token, DateTime date, string time)
    {
        var appt = await _db.Appointments.Include(a => a.Affiliate).Include(a => a.Service).Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(a => a.Token == token);
        if (appt is null) throw new KeyNotFoundException();
        if (appt.Status is "Completed" or "Cancelled")
            throw new InvalidOperationException("Esta cita ya no se puede reagendar.");
        if (string.IsNullOrWhiteSpace(time))
            throw new ArgumentException("La hora es requerida.");
        if (date.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("La fecha no puede ser en el pasado.");

        var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        if (appt.AssignedToId is Guid assignedToId)
        {
            var conflict = await _db.Appointments.AnyAsync(a =>
                a.Id != appt.Id &&
                a.AffiliateId == appt.AffiliateId &&
                a.AssignedToId == assignedToId &&
                a.Date.Date == dateUtc &&
                a.Time == time &&
                a.Status != "Cancelled");
            if (conflict)
                throw new InvalidOperationException("Ese horario ya no está disponible — elige otro.");
        }

        appt.Date = dateUtc;
        appt.Time = time;
        appt.Status = "Scheduled"; // vuelve a pedir confirmación tras reagendar
        appt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new PublicAppointmentManageDto(appt.Token, appt.Affiliate?.Name ?? "", appt.Service?.Name ?? "", appt.AssignedTo?.Name, appt.Date, appt.Time, appt.Status, appt.IsVirtual, appt.IsVirtual ? appt.Affiliate?.ZoomLink : null);
    }
}
