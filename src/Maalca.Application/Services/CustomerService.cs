using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;

    public CustomerService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResponse<Customer>> GetCustomersAsync(Guid affiliateId, int page = 1, int limit = 20, string? search = null, string? status = null)
    {
        var query = _context.Customers.Where(c => c.AffiliateId == affiliateId);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => c.Name.Contains(search) || (c.Email != null && c.Email.Contains(search)) || (c.Phone != null && c.Phone.Contains(search)));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var total = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)total / limit);

        var data = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        return new PaginatedResponse<Customer>
        {
            Data = data,
            Total = total,
            Page = page,
            TotalPages = totalPages
        };
    }

    public async Task<Customer?> GetCustomerAsync(Guid affiliateId, Guid id)
    {
        return await _context.Customers.FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId);
    }

    public async Task<Customer> CreateCustomerAsync(Guid affiliateId, Customer customer)
    {
        customer.AffiliateId = affiliateId;
        customer.Id = Guid.NewGuid();
        customer.CreatedAt = DateTime.UtcNow;
        
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        
        return customer;
    }

    public async Task<Customer?> UpdateCustomerAsync(Guid affiliateId, Guid id, Customer customer)
    {
        var existing = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId);
        if (existing == null) return null;

        existing.Name = customer.Name;
        existing.Email = customer.Email;
        existing.Phone = customer.Phone;
        existing.Notes = customer.Notes;
        existing.Status = customer.Status;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteCustomerAsync(Guid affiliateId, Guid id)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId);
        if (customer == null) return false;

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        return true;
    }
}
