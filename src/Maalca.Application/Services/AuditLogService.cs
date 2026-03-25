using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _context;

    public AuditLogService(AppDbContext context) => _context = context;

    public async Task<PaginatedResponse<AuditLog>> GetAuditLogsAsync(
        Guid? affiliateId = null,
        string? entityType = null,
        string? entityId = null,
        string? userId = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int limit = 50)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrEmpty(entityId))
            query = query.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrEmpty(userId))
            query = query.Where(a => a.UserId == userId);
        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to.Value);

        var total = await query.CountAsync();
        var data = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        return new PaginatedResponse<AuditLog>
        {
            Data = data,
            Total = total,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)total / limit)
        };
    }
}
