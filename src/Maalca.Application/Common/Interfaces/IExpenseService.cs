using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseDto>> GetAsync(Guid affiliateId, DateOnly? from, DateOnly? to, string? category);
    Task<ExpenseDto> CreateAsync(Guid affiliateId, UpsertExpenseRequest request, string? actorId);
    Task<ExpenseDto?> UpdateAsync(Guid affiliateId, Guid expenseId, UpsertExpenseRequest request, string? actorId);
    /// <summary>Desactiva el gasto (sale de los reportes). False si no existe.</summary>
    Task<bool> DeleteAsync(Guid affiliateId, Guid expenseId, string? actorId);
    Task<GenerateRecurringResultDto> GenerateRecurringAsync(Guid affiliateId, GenerateRecurringRequest request, string? actorId);
    Task<ProfitReportDto> GetProfitAsync(Guid affiliateId, string month);
}
