using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Common;
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

        // Campos de cliente por contrato (Fase 1). Solo se tocan si llegan con valor: el formulario de edición
        // anterior no los manda y no debe borrarlos al guardar. Para vaciar un texto se manda "" (cadena vacía).
        // BillingCycle / DeliveryFrequency / CreditDays NO se editan acá: los fija el contrato activo (ContractService).
        if (customer.Rnc is not null) existing.Rnc = string.IsNullOrWhiteSpace(customer.Rnc) ? null : customer.Rnc.Trim();
        if (customer.Address is not null) existing.Address = string.IsNullOrWhiteSpace(customer.Address) ? null : customer.Address.Trim();
        if (customer.GpsLat.HasValue) existing.GpsLat = customer.GpsLat;
        if (customer.GpsLng.HasValue) existing.GpsLng = customer.GpsLng;
        if (customer.RouteId.HasValue) existing.RouteId = customer.RouteId;
        if (customer.PriceListId.HasValue) existing.PriceListId = customer.PriceListId;

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

    // Tarea #244 — centraliza el patrón que antes vivía inline en
    // PublicBookingService.CreatePublicAppointmentAsync (dedup por AffiliateId+Phone). Ahora lo
    // reusan Queue/Reservations/Proposals y también Pedidos (OrderService.LinkCustomerAsync, que
    // antes tenía su propia copia de esta lógica), para que las visitas de un mismo cliente, sin
    // importar por qué módulo entraron, se acumulen en el MISMO registro.
    //
    // El dedup va por LLAVE de contacto (Customer.PhoneKey/EmailKey, columnas generadas), no por el
    // texto que escribió el cliente: antes "(908) 560-9244" y "9085609244" creaban dos clientes.
    public Task<Customer?> ResolveOrCreateCustomerAsync(Guid affiliateId, string name, string? phone)
        => ResolveOrCreateCustomerAsync(affiliateId, name, phone, null);

    public async Task<Customer?> ResolveOrCreateCustomerAsync(Guid affiliateId, string name, string? phone, string? email)
    {
        var phoneKey = ContactKey.Phone(phone);
        var emailKey = ContactKey.Email(email);
        // Sin teléfono ni correo no hay forma de reconocerlo la próxima vez: no se crea nada.
        if (phoneKey is null && emailKey is null) return null;

        var customer = await FindByKeyAsync(affiliateId, phoneKey, emailKey);
        if (customer is not null)
        {
            // Completa el dato que falte: quien antes solo dejó teléfono y ahora deja correo queda
            // con los dos, y la próxima vez lo reconocemos por cualquiera de ellos.
            var changed = false;
            if (string.IsNullOrWhiteSpace(customer.Phone) && !string.IsNullOrWhiteSpace(phone)) { customer.Phone = phone.Trim(); changed = true; }
            if (string.IsNullOrWhiteSpace(customer.Email) && !string.IsNullOrWhiteSpace(email)) { customer.Email = email.Trim(); changed = true; }
            if (changed)
            {
                customer.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return customer;
        }

        customer = new Customer
        {
            AffiliateId = affiliateId,
            Name = string.IsNullOrWhiteSpace(name) ? (phone?.Trim() ?? email!.Trim()) : name.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
        };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    public Task<Customer?> FindCustomerAsync(Guid affiliateId, string? phone, string? email)
        => FindByKeyAsync(affiliateId, ContactKey.Phone(phone), ContactKey.Email(email));

    /// <summary>
    /// Dos búsquedas por índice en vez de un OR: el teléfono manda. Si un registro coincide por
    /// teléfono y otro distinto por correo, gana el del teléfono — es la llave que usan todos los
    /// módulos desde la tarea #244, y así el resultado no depende del orden de las filas.
    /// </summary>
    private async Task<Customer?> FindByKeyAsync(Guid affiliateId, string? phoneKey, string? emailKey)
    {
        if (phoneKey is not null)
        {
            var byPhone = await _context.Customers.FirstOrDefaultAsync(c => c.AffiliateId == affiliateId && c.PhoneKey == phoneKey);
            if (byPhone is not null) return byPhone;
        }
        if (emailKey is not null)
            return await _context.Customers.FirstOrDefaultAsync(c => c.AffiliateId == affiliateId && c.EmailKey == emailKey);
        return null;
    }

    // Tarea #245 — sin esto, TotalVisits/LastVisit se quedan en 0/null para siempre y la
    // pantalla de Clientes no dice nada que el negocio no supiera ya.
    public async Task MarkVisitCompletedAsync(Guid customerId)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer is null) return;
        customer.TotalVisits += 1;
        customer.LastVisit = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
