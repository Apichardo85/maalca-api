using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IOwnerBoardService
{
    /// <summary>Cobros y entregas de hoy. Solo lectura.</summary>
    Task<OwnerBoardTodayDto> GetTodayAsync(Guid affiliateId);
}
