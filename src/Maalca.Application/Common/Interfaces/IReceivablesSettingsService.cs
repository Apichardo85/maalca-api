using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IReceivablesSettingsService
{
    /// <summary>Los ajustes del negocio, o los valores por defecto si nunca los configuró.</summary>
    Task<ReceivablesSettingsDto> GetAsync(Guid affiliateId);
    Task<ReceivablesSettingsDto> UpdateAsync(Guid affiliateId, ReceivablesSettingsDto request);
}
