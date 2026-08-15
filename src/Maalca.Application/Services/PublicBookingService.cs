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

    public PublicBookingService(AppDbContext db)
    {
        _db = db;
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
            .Select(s => new PublicServiceDto(s.Id, s.Name, s.Description, s.Price, s.DurationMinutes ?? 30))
            .ToListAsync();
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

        if (request.AssignedToId is Guid assignedToId)
        {
            var validStaff = await _db.TeamMembers
                .AnyAsync(t => t.Id == assignedToId && t.AffiliateId == affiliate.Id && t.IsActive);
            if (!validStaff)
                throw new ArgumentException("El miembro del personal seleccionado no está disponible.");

            var conflict = await _db.Appointments.AnyAsync(a =>
                a.AffiliateId == affiliate.Id &&
                a.AssignedToId == assignedToId &&
                a.Date.Date == request.Date.Date &&
                a.Time == request.Time &&
                a.Status != "Cancelled");
            if (conflict)
                throw new InvalidOperationException("Ese horario ya no está disponible — elige otro.");
        }

        // Reusa el Customer si ya existe uno con el mismo teléfono para este afiliado (evita
        // duplicar clientes recurrentes que reservan varias veces), si no lo crea.
        var customer = await _db.Customers.FirstOrDefaultAsync(c =>
            c.AffiliateId == affiliate.Id && c.Phone == request.CustomerPhone);
        if (customer is null)
        {
            customer = new Customer
            {
                AffiliateId = affiliate.Id,
                Name = request.CustomerName,
                Phone = request.CustomerPhone,
            };
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }

        var appointment = new Appointment
        {
            AffiliateId = affiliate.Id,
            CustomerId = customer.Id,
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
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        return new PublicAppointmentResultDto(appointment.Id, appointment.Date, appointment.Time, appointment.Status);
    }
}
