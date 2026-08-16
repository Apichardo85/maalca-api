using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _context;

    public AppointmentService(AppDbContext context) => _context = context;

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

        existing.CustomerId = appointment.CustomerId;
        existing.ServiceId = appointment.ServiceId;
        existing.Date = newDate;
        existing.Time = appointment.Time;
        existing.Status = appointment.Status;
        existing.Notes = appointment.Notes;
        existing.AssignedToId = appointment.AssignedToId;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<Appointment?> UpdateAppointmentStatusAsync(Guid affiliateId, Guid id, string status)
    {
        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.AffiliateId == affiliateId);
        if (appointment == null) return null;
        appointment.Status = status;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
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

    public async Task<PaginatedResponse<InventoryItem>> GetInventoryAsync(Guid affiliateId, string? category = null, string? status = null, int page = 1)
    {
        var query = _context.InventoryItems.Where(i => i.AffiliateId == affiliateId);
        if (!string.IsNullOrEmpty(category)) query = query.Where(i => i.Category == category);
        if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.Status == status);
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
        existing.Status = item.Status;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteInventoryItemAsync(Guid affiliateId, Guid id)
    {
        var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);
        if (item == null) return false;
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
}

public class QueueService : IQueueService
{
    private readonly AppDbContext _context;
    private readonly IQueueRealtimeNotifier _realtime;

    public QueueService(AppDbContext context, IQueueRealtimeNotifier realtime)
    {
        _context = context;
        _realtime = realtime;
    }

    public async Task<List<QueueEntry>> GetQueueAsync(Guid affiliateId)
        => await _context.QueueEntries.AsNoTracking()
            .Where(q => q.AffiliateId == affiliateId && q.Status == "waiting")
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

        _context.QueueEntries.Add(entry);
        await _context.SaveChangesAsync();
        await _realtime.NotifyQueueUpdatedAsync(affiliateId, await GetQueueAsync(affiliateId));
        return entry;
    }

    public async Task<QueueEntry?> UpdateQueueEntryAsync(Guid affiliateId, Guid id, string status, Guid? barberId = null)
    {
        var entry = await _context.QueueEntries.FirstOrDefaultAsync(q => q.Id == id && q.AffiliateId == affiliateId);
        if (entry == null) return null;
        entry.Status = status;
        if (barberId.HasValue) entry.AssignedToId = barberId;
        if (status == "in_service") entry.CalledAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _realtime.NotifyQueueUpdatedAsync(affiliateId, await GetQueueAsync(affiliateId));
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

    public InvoiceService(AppDbContext context) => _context = context;

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
        existing.DueDate = invoice.DueDate;
        existing.PaidDate = invoice.PaidDate;
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
