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

        IQueryable<Appointment> query = baseQuery.Include(a => a.Customer).Include(a => a.Service);

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
        => await _context.Appointments.Include(a => a.Customer).Include(a => a.Service).FirstOrDefaultAsync(a => a.Id == id && a.AffiliateId == affiliateId);

    public async Task<Appointment> CreateAppointmentAsync(Guid affiliateId, Appointment appointment)
    {
        appointment.AffiliateId = affiliateId;
        appointment.Id = Guid.NewGuid();
        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();
        return appointment;
    }

    public async Task<Appointment?> UpdateAppointmentStatusAsync(Guid affiliateId, Guid id, string status)
    {
        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.AffiliateId == affiliateId);
        if (appointment == null) return null;
        appointment.Status = status;
        await _context.SaveChangesAsync();
        return appointment;
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

    public async Task<InventoryMovement> CreateMovementAsync(Guid affiliateId, InventoryMovement movement)
    {
        var item = await _context.InventoryItems.FindAsync(movement.InventoryItemId);
        if (item == null || item.AffiliateId != affiliateId)
            throw new InvalidOperationException("Inventory item not found");

        movement.Id = Guid.NewGuid();

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

    public QueueService(AppDbContext context) => _context = context;

    public async Task<List<QueueEntry>> GetQueueAsync(Guid affiliateId)
        => await _context.QueueEntries.Where(q => q.AffiliateId == affiliateId && q.Status == "waiting")
            .OrderBy(q => q.Position).ToListAsync();

    public async Task<QueueEntry> AddToQueueAsync(Guid affiliateId, QueueEntry entry)
    {
        var maxPosition = await _context.QueueEntries.Where(q => q.AffiliateId == affiliateId && q.Status == "waiting")
            .MaxAsync(q => (int?)q.Position) ?? 0;

        entry.AffiliateId = affiliateId;
        entry.Id = Guid.NewGuid();
        entry.Position = maxPosition + 1;
        entry.Status = "waiting";

        _context.QueueEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<QueueEntry?> UpdateQueueEntryAsync(Guid affiliateId, Guid id, string status, Guid? barberId = null)
    {
        var entry = await _context.QueueEntries.FirstOrDefaultAsync(q => q.Id == id && q.AffiliateId == affiliateId);
        if (entry == null) return null;
        entry.Status = status;
        if (barberId.HasValue) entry.AssignedToId = barberId;
        if (status == "in_service") entry.CalledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
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

    public async Task<Invoice> CreateInvoiceAsync(Guid affiliateId, Invoice invoice)
    {
        invoice.AffiliateId = affiliateId;
        invoice.Id = Guid.NewGuid();
        invoice.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }
}

public class GiftCardService : IGiftCardService
{
    private readonly AppDbContext _context;

    public GiftCardService(AppDbContext context) => _context = context;

    public async Task<List<GiftCard>> GetGiftCardsAsync(Guid affiliateId, string? status = null)
    {
        var query = _context.GiftCards.Where(g => g.AffiliateId == affiliateId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(g => g.Status == status);
        return await query.ToListAsync();
    }

    public async Task<GiftCard?> GetGiftCardAsync(Guid affiliateId, Guid id)
        => await _context.GiftCards.FirstOrDefaultAsync(g => g.Id == id && g.AffiliateId == affiliateId);

    public async Task<GiftCard> CreateGiftCardAsync(Guid affiliateId, GiftCard giftCard)
    {
        giftCard.AffiliateId = affiliateId;
        giftCard.Id = Guid.NewGuid();
        giftCard.Code = Guid.NewGuid().ToString("N").ToUpper()[..16];
        giftCard.Balance = giftCard.InitialAmount;
        giftCard.Status = "Active";
        _context.GiftCards.Add(giftCard);
        await _context.SaveChangesAsync();
        return giftCard;
    }
}

public class CampaignService : ICampaignService
{
    private readonly AppDbContext _context;

    public CampaignService(AppDbContext context) => _context = context;

    public async Task<List<Campaign>> GetCampaignsAsync(Guid affiliateId, string? status = null)
    {
        var query = _context.Campaigns.Where(c => c.AffiliateId == affiliateId);
        if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);
        return await query.ToListAsync();
    }

    public async Task<Campaign?> GetCampaignAsync(Guid affiliateId, Guid id)
        => await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId);

    public async Task<Campaign> CreateCampaignAsync(Guid affiliateId, Campaign campaign)
    {
        campaign.AffiliateId = affiliateId;
        campaign.Id = Guid.NewGuid();
        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();
        return campaign;
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
        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        return lead;
    }

    public async Task<Lead> CreateCirisonicLeadAsync(Lead lead)
    {
        lead.Id = Guid.NewGuid();
        lead.Source = "cirisonic";
        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        return lead;
    }
}
