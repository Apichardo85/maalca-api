using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IOnboardingService
{
    Task<OnboardingResponse> OnboardAsync(string supabaseUserId, string email, OnboardingRequest request);

    /// <summary>
    /// Crea un afiliado de prueba desde /ops, sin ningún UserAffiliateMap asociado.
    /// El admin lo configura vía impersonación; el dueño real se asocia después
    /// invitándolo como Owner desde /space/{slug}/equipo (ya funciona sin cambios).
    /// </summary>
    Task<OnboardingResponse> CreateTrialAsync(OnboardingRequest request);
}
