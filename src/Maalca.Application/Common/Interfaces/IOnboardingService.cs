using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IOnboardingService
{
    Task<OnboardingResponse> OnboardAsync(string supabaseUserId, string email, OnboardingRequest request);
}
