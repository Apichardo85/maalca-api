using Maalca.Application.Common;
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
/// (no canceladas) en la misma fecha+hora. Sin AssignedToId el criterio depende de si el
/// negocio tiene personal activo:
///  - CON personal: no hay conflicto que chequear, el negocio decide luego quién la atiende
///    (caso barbería — tres barberos pueden tomar tres clientes en la misma hora).
///  - SIN personal: la agenda es de una sola persona, así que dos citas a la misma hora se
///    pisan de verdad. Antes no se chequeaba nada acá y GetPublicBusyTimesAsync descartaba
///    las citas sin asignar, así que un consultor solo mostraba TODOS los slots libres siempre
///    y aceptaba reservas ilimitadas en la misma hora — con sala virtual fija, los clientes
///    caían en la misma reunión.
/// </summary>
public class PublicBookingService : IPublicBookingService
{
    /// <summary>
    /// Clave reservada de PublicBusyTimesDto.BusyByStaff para las citas sin profesional
    /// asignado. No colisiona con un Guid, así que convive con las claves por staffId.
    /// El front la lee cuando el negocio no tiene personal (ver PublicBookingSection).
    /// </summary>
    public const string UnassignedStaffKey = "unassigned";

    /// <summary>Token del módulo de agenda en ModulosActivos — el interruptor del motor de citas.</summary>
    private const string AppointmentsModule = "appointments";

    private readonly AppDbContext _db;
    private readonly IQueueService _queueService;
    private readonly ICustomerService _customerService;
    private readonly IAppointmentNotificationService _appointmentNotifications;
    private readonly IReservationNotificationService _reservationNotifications;

    public PublicBookingService(AppDbContext db, IQueueService queueService, ICustomerService customerService, IAppointmentNotificationService appointmentNotifications, IReservationNotificationService reservationNotifications)
    {
        _reservationNotifications = reservationNotifications;
        _db = db;
        _queueService = queueService;
        _customerService = customerService;
        _appointmentNotifications = appointmentNotifications;
    }

    /// <summary>
    /// El afiliado tal como lo ve la reserva pública de CITAS: activo y con el módulo de agenda
    /// prendido. A propósito NO mira Published.
    ///
    /// Published significaba dos cosas a la vez — "mi página pública se ve" y "mi motor de
    /// reservas responde" — y eso ya se había roto una vez: los negocios "Solo pantallas" tienen
    /// Published=false pero sus TVs necesitan el catálogo, y se parchó con (Published ||
    /// ScreensOnly) en PublicCatalogService. El segundo caso es MaalCa, que agenda videollamadas
    /// sin querer una página de negocio aparte de su vitrina.
    ///
    /// Así que los dos significados viven separados: Published gobierna la página (viaja como
    /// PagePublished en el catálogo y la apaga con 404), y el interruptor del motor es el módulo
    /// `appointments`, que el dueño ya prende y apaga desde su panel. Un negocio que quiera dejar
    /// de recibir citas apaga el módulo, no despublica la página.
    ///
    /// IsActive sigue mandando sobre todo: es la suspensión desde /ops.
    /// </summary>
    private async Task<Affiliate?> GetBookableAffiliateAsync(string affiliateSlug)
    {
        var affiliate = await _db.Affiliates
            .FirstOrDefaultAsync(a => a.Slug == affiliateSlug && a.IsActive);
        if (affiliate is null) return null;

        var modules = await ModuleAccess.GetActiveModulesAsync(_db, affiliate);
        return modules.Contains(AppointmentsModule) ? affiliate : null;
    }

    public async Task<List<PublicTeamMemberDto>?> GetPublicTeamAsync(string affiliateSlug)
    {
        var affiliate = await GetBookableAffiliateAsync(affiliateSlug);
        if (affiliate is null) return null;

        return await _db.TeamMembers
            .Where(t => t.AffiliateId == affiliate.Id && t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new PublicTeamMemberDto(t.Id, t.Name, t.Role, t.PhotoUrl))
            .ToListAsync();
    }

    public async Task<List<PublicServiceDto>?> GetPublicServicesAsync(string affiliateSlug)
    {
        var affiliate = await GetBookableAffiliateAsync(affiliateSlug);
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
        var affiliate = await GetBookableAffiliateAsync(affiliateSlug);
        if (affiliate is null) return null;

        // Misma razón de siempre: la fecha "bare" que llega por query string deserializa con
        // Kind=Unspecified y la columna es timestamptz.
        var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // Antes esto filtraba `a.AssignedToId != null`, así que las citas sin profesional
        // asignado no salían como ocupadas en ningún lado. En un negocio de una sola persona
        // (sin TeamMembers) eso significaba que el grid de horas nunca tachaba nada.
        var busy = await _db.Appointments
            .Where(a =>
                a.AffiliateId == affiliate.Id &&
                a.Date.Date == dateUtc &&
                a.Status != "Cancelled")
            .Select(a => new { a.AssignedToId, a.Time, Duration = a.Service!.DurationMinutes })
            .ToListAsync();

        // Se reporta cada slot que la cita OCUPA, no solo su hora de inicio: una cita de 60 min
        // a las 09:00 tacha 09:00 y 09:30. Antes solo viajaba la hora exacta, así que un servicio
        // largo dejaba vendible el slot de la mitad (ver AppointmentSchedule).
        var busyByStaff = new Dictionary<string, List<string>>();
        foreach (var appt in busy)
        {
            var staffKey = appt.AssignedToId?.ToString() ?? UnassignedStaffKey;
            if (!busyByStaff.TryGetValue(staffKey, out var taken))
            {
                taken = new List<string>();
                busyByStaff[staffKey] = taken;
            }
            foreach (var slot in AppointmentSchedule.CoveredSlots(appt.Time, appt.Duration))
                if (!taken.Contains(slot)) taken.Add(slot);
        }

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
                // Un bloqueo de TODO el personal (StaffId null) también tapa la línea "sin
                // asignar": si no, en un negocio de una sola persona allStaffIds va vacío y el
                // bloqueo no tapaba absolutamente nada (almuerzo o vacaciones invisibles).
                var targetKeys = block.StaffId.HasValue
                    ? new List<string> { block.StaffId.Value.ToString() }
                    : allStaffIds.Select(id => id.ToString()).Append(UnassignedStaffKey).ToList();
                foreach (var key in targetKeys)
                {
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

    /// <summary>Atajo sobre AppointmentOverlapQuery, que es donde vive el criterio compartido.</summary>
    private Task<bool> HasOverlapAsync(Guid affiliateId, Guid? staffId, DateTime dateUtc, string time, int? durationMinutes, Guid? excludeAppointmentId = null) =>
        AppointmentOverlapQuery.HasOverlapAsync(_db.Appointments, affiliateId, staffId, dateUtc, time, durationMinutes, excludeAppointmentId);

    // Convierte un rango HH:mm-HH:mm en la misma grilla de 30 min que usa generateTimeSlots()
    // del lado del front, para que un bloqueo de "12:00 a 13:00" tache exactamente los slots
    // "12:00" y "12:30" — sin esto, comparar el rango contra un solo string de hora exacta
    // dejaría pasar reservas que caen dentro del bloqueo pero no coinciden con StartTime.
    private static List<string> ExpandBlockToSlots(string startTime, string endTime)
    {
        if (!AppointmentSchedule.TryParseHm(startTime, out var startMins) ||
            !AppointmentSchedule.TryParseHm(endTime, out var endMins) ||
            endMins <= startMins)
        {
            // Rango vacío o invertido (dato malo, o un bloqueo que cruza medianoche, que el resto
            // de la agenda tampoco contempla): no tapa nada. El guard es explícito porque
            // CoveredSlots trataría una duración <= 0 como "sin fijar" y caería al default de 30.
            return new List<string>();
        }

        // Un bloqueo es un intervalo igual que una cita, así que reusa la misma expansión.
        return AppointmentSchedule.CoveredSlots(startTime, endMins - startMins);
    }

    public async Task<PublicAppointmentResultDto> CreatePublicAppointmentAsync(string affiliateSlug, CreatePublicAppointmentRequest request)
    {
        var affiliate = await GetBookableAffiliateAsync(affiliateSlug);
        if (affiliate is null)
            throw new KeyNotFoundException();

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("El nombre es requerido.");
        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            throw new ArgumentException("El teléfono es requerido.");
        if (!Maalca.Application.Common.PhoneRules.IsValid(request.CustomerPhone))
            throw new ArgumentException("El teléfono no es válido: escribe 10 dígitos.");
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

        // request.Date llega con Kind=Unspecified (fecha "bare" del front) — Npgsql 8+ no
        // permite compararlo contra la columna timestamptz sin forzar Kind=Utc primero, ni al
        // comparar ni al escribir. Sin esto el chequeo de conflicto tronaba con
        // ArgumentException y CADA reserva pública de cita devolvía 400. Bug real reportado
        // en producción 2026-08-17 — bloqueaba toda la Agenda pública (Barbería/Servicios).
        var requestDateUtc = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);

        if (request.AssignedToId is Guid assignedToId)
        {
            var validStaff = await _db.TeamMembers
                .AnyAsync(t => t.Id == assignedToId && t.AffiliateId == affiliate.Id && t.IsActive);
            if (!validStaff)
                throw new ArgumentException("El miembro del personal seleccionado no está disponible.");

            if (await HasOverlapAsync(affiliate.Id, assignedToId, requestDateUtc, request.Time, service.DurationMinutes))
                throw new InvalidOperationException("Ese horario ya no está disponible — elige otro.");
        }
        else
        {
            // Sin profesional elegido el criterio depende de si hay personal (ver doc de clase):
            // con personal activo el negocio asigna después y no hay nada que chequear; sin
            // personal la agenda es de una sola persona y dos citas solapadas se pisan de verdad.
            var hasActiveStaff = await _db.TeamMembers
                .AnyAsync(t => t.AffiliateId == affiliate.Id && t.IsActive);
            if (!hasActiveStaff &&
                await HasOverlapAsync(affiliate.Id, null, requestDateUtc, request.Time, service.DurationMinutes))
                throw new InvalidOperationException("Ese horario ya no está disponible — elige otro.");
        }

        // Bloqueos manuales de horario (TimeBlock, task #192). Hasta ahora solo servían para
        // tachar slots en el grid del front, así que un POST directo al endpoint público los
        // ignoraba por completo: el almuerzo o las vacaciones del dueño no detenían una reserva.
        // Se filtran en memoria porque son pocas filas por día y porque comparar StaffId nullable
        // contra un parámetro nullable en EF es justo el tipo de traducción que sorprende.
        var dayBlocks = await _db.TimeBlocks
            .Where(b => b.AffiliateId == affiliate.Id && b.Date.Date == requestDateUtc)
            .Select(b => new { b.StaffId, b.StartTime, b.EndTime })
            .ToListAsync();

        if (dayBlocks.Count > 0)
        {
            var blocksForThisBooking = dayBlocks
                .Where(b => b.StaffId is null || b.StaffId == request.AssignedToId);

            foreach (var block in blocksForThisBooking)
            {
                if (!AppointmentSchedule.TryParseHm(block.StartTime, out var from) ||
                    !AppointmentSchedule.TryParseHm(block.EndTime, out var to) ||
                    to <= from)
                    continue; // mismo criterio que ExpandBlockToSlots: rango inválido no tapa nada

                // Intervalo contra intervalo, igual que entre citas: una asesoría de 60 min a las
                // 11:30 cae dentro de un bloqueo de 12:00 a 13:00 aunque no empiece dentro de él.
                if (AppointmentSchedule.Overlaps(request.Time, service.DurationMinutes, block.StartTime, to - from))
                    throw new InvalidOperationException("Ese horario no está disponible — elige otro.");
            }
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
            // Es una fecha de calendario, no un instante real: se guarda con Kind=Utc forzado
            // (ver requestDateUtc arriba) en vez de cambiar el tipo de columna.
            Date = requestDateUtc,
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
            await _appointmentNotifications.NotifyAppointmentBookedAsync(appointment, customer, affiliate.Name, affiliateSlug, service.Name, staffName, affiliate.ZoomLink, service.DurationMinutes, affiliate.Timezone);
        }

        return new PublicAppointmentResultDto(appointment.Id, appointment.Date, appointment.Time, appointment.Status, appointment.Token, appointment.IsVirtual, isVirtual ? affiliate.ZoomLink : null);
    }

    private const int MaxPartySize = 20;
    private const int MaxDaysAhead = 30;

    // Mismos tokens (español, sin acento) que DiaSemanaTokens / Affiliate.Horario.
    private static string DiaTokenFor(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "lunes",
        DayOfWeek.Tuesday => "martes",
        DayOfWeek.Wednesday => "miercoles",
        DayOfWeek.Thursday => "jueves",
        DayOfWeek.Friday => "viernes",
        DayOfWeek.Saturday => "sabado",
        _ => "domingo",
    };

    // "HH:mm" -> minutos desde medianoche.
    private readonly record struct TimeOfDay(int Minutes)
    {
        public static bool TryParse(string? value, out TimeOfDay result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var parts = value.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
            if (h is < 0 or > 23 || m is < 0 or > 59) return false;
            result = new TimeOfDay(h * 60 + m);
            return true;
        }
    }

    public async Task<PublicTableReservationResultDto> CreatePublicTableReservationAsync(string affiliateSlug, CreatePublicTableReservationRequest request)
    {
        var affiliate = await _db.Affiliates
            .FirstOrDefaultAsync(a => a.Slug == affiliateSlug && a.Published);
        if (affiliate is null)
            throw new KeyNotFoundException();

        // Reservas solo si el módulo está activo para este afiliado (mismo criterio que el
        // dashboard: ModulosActivos explícito manda, null cae al default por tipo de negocio).
        var activeModules = await ModuleAccess.GetActiveModulesAsync(_db, affiliate);
        if (!activeModules.Contains("reservations", StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Este negocio no acepta reservas en línea.");

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("El nombre es requerido.");
        if (request.CustomerName.Trim().Length > 100)
            throw new ArgumentException("El nombre es demasiado largo.");
        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            throw new ArgumentException("El teléfono es requerido.");
        if (!Maalca.Application.Common.PhoneRules.IsValid(request.CustomerPhone))
            throw new ArgumentException("El teléfono no es válido: escribe 10 dígitos.");
        if (request.CustomerPhone.Trim().Length > 30)
            throw new ArgumentException("El teléfono no es válido.");
        if (request.CustomerEmail?.Length > 200 || request.Notes?.Length > 500)
            throw new ArgumentException("Email o notas demasiado largos.");
        if (string.IsNullOrWhiteSpace(request.Time))
            throw new ArgumentException("La hora es requerida.");
        if (!TimeOfDay.TryParse(request.Time, out var requestedTime))
            throw new ArgumentException("La hora debe tener formato HH:mm.");
        if (request.PartySize < 1)
            throw new ArgumentException("El número de personas debe ser al menos 1.");
        if (request.PartySize > MaxPartySize)
            throw new ArgumentException($"Para grupos de más de {MaxPartySize} personas, contacta directamente al restaurante.");

        // "Hoy" y "ahora" se miden en la zona horaria del NEGOCIO, no en UTC: a las 9pm de Nueva
        // York ya es "mañana" en UTC y la reserva de hoy se rechazaba como "en el pasado".
        var tz = TimeZoneInfo.Utc;
        if (!string.IsNullOrWhiteSpace(affiliate.Timezone))
        {
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(affiliate.Timezone); }
            catch (Exception) { /* zona inválida: cae a UTC, mismo criterio que CommunityService */ }
        }
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var requestedDate = request.Date.Date;
        if (requestedDate < localNow.Date)
            throw new ArgumentException("La fecha no puede ser en el pasado.");
        if (requestedDate > localNow.Date.AddDays(MaxDaysAhead))
            throw new ArgumentException($"Solo se puede reservar con hasta {MaxDaysAhead} días de anticipación.");
        if (requestedDate == localNow.Date && requestedTime.Minutes <= localNow.Hour * 60 + localNow.Minute)
            throw new ArgumentException("Esa hora ya pasó. Elige una hora posterior.");

        // Horario configurado en Identidad. Sin entrada para ese día = no se restringe (negocio
        // que nunca lo configuró); con entrada, se respeta cerrado y la ventana abre–cierra.
        var diaToken = DiaTokenFor(requestedDate.DayOfWeek);
        var hoy = JsonArrayField.Parse<HorarioEntryDto>(affiliate.Horario)
            .FirstOrDefault(h => string.Equals(h.Dia, diaToken, StringComparison.OrdinalIgnoreCase));
        if (hoy is not null)
        {
            if (hoy.Cerrado)
                throw new ArgumentException("El restaurante está cerrado ese día. Elige otra fecha.");
            if (TimeOfDay.TryParse(hoy.Abre, out var abre) && TimeOfDay.TryParse(hoy.Cierra, out var cierra)
                && (requestedTime.Minutes < abre.Minutes || requestedTime.Minutes >= cierra.Minutes))
                throw new ArgumentException($"Ese día atendemos de {hoy.Abre} a {hoy.Cierra}.");
        }

        // Doble envío (doble click, reintento): mismo teléfono + misma fecha/hora ya activa.
        var phone = request.CustomerPhone.Trim();
        var requestedDateUtc = DateTime.SpecifyKind(requestedDate, DateTimeKind.Utc);
        var duplicate = await _db.TableReservations.AnyAsync(r =>
            r.AffiliateId == affiliate.Id && r.CustomerPhone == phone
            && r.Date == requestedDateUtc && r.Time == request.Time
            && r.Status != "Cancelled" && r.Status != "NoShow");
        if (duplicate)
            throw new ArgumentException("Ya tienes una reserva para esa fecha y hora.");

        // Tarea #244 — mismo dedup por teléfono que Appointment, para que un comensal recurrente
        // acumule sus reservas en el mismo Customer que sus citas/visitas a la fila.
        var reservationCustomer = await _customerService.ResolveOrCreateCustomerAsync(affiliate.Id, request.CustomerName, request.CustomerPhone);

        var reservation = new TableReservation
        {
            AffiliateId = affiliate.Id,
            CustomerId = reservationCustomer?.Id,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = phone,
            CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail.Trim(),
            // Misma razón que en CreatePublicAppointmentAsync — fecha "bare" deserializa
            // Kind=Unspecified, la columna es timestamptz.
            Date = requestedDateUtc,
            Time = request.Time,
            PartySize = request.PartySize,
            Status = "Requested",
            Notes = request.Notes,
        };
        _db.TableReservations.Add(reservation);
        await _db.SaveChangesAsync();

        // Se espera (no fire-and-forget): ver nota en CreatePublicAppointmentAsync sobre el runtime
        // de Railway. El servicio atrapa sus errores; nunca tumba la reserva ya guardada.
        await _reservationNotifications.NotifyReservationRequestedAsync(reservation, affiliate);

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
        // Customer se incluye para poder avisar por correo al final — antes no hacía falta porque
        // reagendar no notificaba a nadie.
        var appt = await _db.Appointments.Include(a => a.Affiliate).Include(a => a.Service).Include(a => a.AssignedTo).Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Token == token);
        if (appt is null) throw new KeyNotFoundException();
        if (appt.Status is "Completed" or "Cancelled")
            throw new InvalidOperationException("Esta cita ya no se puede reagendar.");
        if (string.IsNullOrWhiteSpace(time))
            throw new ArgumentException("La hora es requerida.");
        if (date.Date < DateTime.UtcNow.Date)
            throw new ArgumentException("La fecha no puede ser en el pasado.");

        var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // Mismo criterio que CreatePublicAppointmentAsync (ver doc de clase): con personal se
        // chequea contra el profesional asignado; sin personal activo, contra las demás citas
        // sin asignar, porque la agenda es de una sola persona. Antes esta rama no existía y el
        // cliente podía reagendarse encima de otra cita desde /cita/{token}.
        bool conflict;
        if (appt.AssignedToId is Guid assignedToId)
        {
            conflict = await HasOverlapAsync(appt.AffiliateId, assignedToId, dateUtc, time, appt.Service?.DurationMinutes, appt.Id);
        }
        else
        {
            var hasActiveStaff = await _db.TeamMembers
                .AnyAsync(t => t.AffiliateId == appt.AffiliateId && t.IsActive);
            conflict = !hasActiveStaff &&
                await HasOverlapAsync(appt.AffiliateId, null, dateUtc, time, appt.Service?.DurationMinutes, appt.Id);
        }
        if (conflict)
            throw new InvalidOperationException("Ese horario ya no está disponible — elige otro.");

        appt.Date = dateUtc;
        appt.Time = time;
        appt.Status = "Scheduled"; // vuelve a pedir confirmación tras reagendar
        appt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Reagendar no avisaba a nadie: el dueño no se enteraba y el cliente se quedaba con la
        // hora vieja agendada en su calendario (desde que la confirmación manda .ics). El correo
        // es el mismo de la reserva porque el estado vuelve a ser "Scheduled"; el .ics viaja con
        // el mismo UID y un SEQUENCE mayor, así que actualiza el evento en vez de duplicarlo.
        // Se espera, no fire-and-forget, por lo mismo que en Create (tarea #135).
        if (appt.Customer is not null)
        {
            await _appointmentNotifications.NotifyAppointmentBookedAsync(
                appt, appt.Customer, appt.Affiliate?.Name ?? "", appt.Affiliate?.Slug ?? "",
                appt.Service?.Name ?? "", appt.AssignedTo?.Name,
                appt.IsVirtual ? appt.Affiliate?.ZoomLink : null,
                appt.Service?.DurationMinutes, appt.Affiliate?.Timezone, rescheduled: true);
        }

        return new PublicAppointmentManageDto(appt.Token, appt.Affiliate?.Name ?? "", appt.Service?.Name ?? "", appt.AssignedTo?.Name, appt.Date, appt.Time, appt.Status, appt.IsVirtual, appt.IsVirtual ? appt.Affiliate?.ZoomLink : null);
    }
}
