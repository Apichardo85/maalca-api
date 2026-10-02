using Maalca.Application.Common.DTOs;
using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

public interface ICommunitySignupService
{
    /// <summary>
    /// Alta pública. Lanza KeyNotFoundException (negocio no existe/no publicado) o
    /// ArgumentException (validación: el endpoint la convierte en 400 con el mensaje).
    /// </summary>
    Task<PublicSignupResultDto> CreatePublicAsync(string affiliateSlug, CreatePublicSignupRequest request);

    Task<List<CommunitySignup>> ListAsync(Guid affiliateId, string? kind, string? status);

    /// <summary>
    /// Cambia el estado (New / Confirmed / Cancelled). Devuelve null si no existe. Lanza
    /// ArgumentException si el estado no es válido e InvalidOperationException si reactivar una
    /// inscripción cancelada ya no cabe en el cupo del evento.
    /// </summary>
    Task<CommunitySignup?> UpdateStatusAsync(Guid affiliateId, Guid id, string status);

    Task<bool> DeleteAsync(Guid affiliateId, Guid id);

    /// <summary>Personas inscritas (no canceladas) por evento, para mostrar "quedan N lugares" en la vitrina.</summary>
    Task<Dictionary<Guid, int>> GetTakenByActivityAsync(IEnumerable<Guid> activityIds);
}

/// <summary>
/// Mismo patrón que IReservationNotificationService: maalca-api no manda correos, llama a un
/// endpoint interno de maalca-web (Resend vive allá) y crea el aviso del dueño (campana + push).
/// Todo best-effort: nunca lanza.
/// </summary>
public interface ICommunitySignupNotificationService
{
    /// <summary>Entró una inscripción pública: avisa al dueño y le escribe a quien se inscribió (si dejó correo).</summary>
    Task NotifySignupCreatedAsync(CommunitySignup signup, Affiliate affiliate, Maalca.Domain.Entities.Activity? activity);

    /// <summary>El negocio confirmó (voluntario) o canceló una inscripción; avisa a la persona si dejó correo.</summary>
    Task NotifySignupStatusAsync(CommunitySignup signup, Affiliate affiliate, Maalca.Domain.Entities.Activity? activity, string kind);
}
