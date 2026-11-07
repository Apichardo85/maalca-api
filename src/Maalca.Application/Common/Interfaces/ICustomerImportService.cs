using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Importación masiva de clientes. Solo crea: los duplicados (mismo teléfono, o mismo nombre si la fila no trae
/// teléfono) se reportan y se saltan, nunca se sobrescriben. ArgumentException = petición inválida (vacía o demasiado grande).</summary>
public interface ICustomerImportService
{
    Task<ImportCustomersResultDto> ImportAsync(Guid affiliateId, ImportCustomersRequest request, string? actorId, string? actorName);
}
