using Maalca.Domain.Common.Interfaces;

namespace Maalca.Api.Filters;

public class AffiliateAuthorizationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

        if (!currentUser.IsAuthenticated)
            return Results.Unauthorized();

        var affiliateIdFromRoute = context.HttpContext.GetRouteValue("affiliateId")?.ToString();

        if (currentUser.AffiliateId.HasValue &&
            !string.IsNullOrEmpty(affiliateIdFromRoute) &&
            !string.Equals(currentUser.AffiliateId.Value.ToString(), affiliateIdFromRoute, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Forbid();
        }

        return await next(context);
    }
}
