using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Maalca.Domain.Common.Interfaces;

namespace Maalca.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var sub = User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? AffiliateId
    {
        get
        {
            var value = User?.FindFirstValue("affiliateId");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirstValue(JwtRegisteredClaimNames.Email)
                         ?? User?.FindFirstValue(ClaimTypes.Email);

    public string? Role => User?.FindFirstValue("role")
                        ?? User?.FindFirstValue(ClaimTypes.Role);
}
