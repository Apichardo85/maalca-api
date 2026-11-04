using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IProspectIntakeService
{
    Task<IReadOnlyList<ProspectIntakeSummaryDto>> GetAllAsync();
    Task<ProspectIntakeDto?> GetAsync(Guid id);

    /// <summary>Crea un levantamiento. ArgumentException si las preguntas o los datos no son válidos.</summary>
    Task<ProspectIntakeDto> CreateAsync(UpsertProspectIntakeRequest request, string adminEmail);

    /// <summary>Reemplaza los campos editables. null si no existe; ArgumentException si no es válido.</summary>
    Task<ProspectIntakeDto?> UpdateAsync(Guid id, UpsertProspectIntakeRequest request);

    /// <summary>Cambia el estado (reabrir, marcar revisado, cerrar). null si no existe.</summary>
    Task<ProspectIntakeDto?> SetStatusAsync(Guid id, string status);

    Task<bool> DeleteAsync(Guid id);

    /// <summary>Vista pública por token. null si no existe.</summary>
    Task<PublicProspectIntakeDto?> GetPublicAsync(Guid token);

    /// <summary>
    /// Guarda respuestas del prospecto. KeyNotFoundException si el token no existe,
    /// InvalidOperationException si ya no acepta respuestas, ArgumentException si faltan
    /// obligatorias (al enviar) o el formato no es válido.
    /// </summary>
    Task<SaveProspectIntakeAnswersResult> SaveAnswersAsync(Guid token, SaveProspectIntakeAnswersRequest request, string? ip);
}
