using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Maalca.Application.Common.Interfaces;
using Maalca.Infrastructure.Auth;
using Microsoft.IdentityModel.Tokens;

namespace Maalca.Api.Middleware;

public class SupabaseAuthMiddleware
{
    private readonly RequestDelegate _next;
    private const string SupabaseIssuer = "https://nyiocxrrbrphfczsbqpf.supabase.co/auth/v1";
    private const string InternalIssuer = "maalca-api";

    public SupabaseAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAffiliateMapService mapService,
        SupabaseJwksCache jwksCache)
    {
        var token = ExtractBearerToken(context);
        if (token == null)
        {
            await _next(context);
            return;
        }

        string issuer;
        try
        {
            var reader = new JwtSecurityTokenHandler();
            var jwt = reader.ReadJwtToken(token);
            issuer = jwt.Issuer;
        }
        catch
        {
            context.Response.StatusCode = 401;
            return;
        }

        if (issuer == InternalIssuer)
        {
            await _next(context);
            return;
        }

        if (issuer != SupabaseIssuer)
        {
            context.Response.StatusCode = 401;
            return;
        }

        // Validate Supabase JWT signature + claims
        JsonWebKeySet keySet;
        try
        {
            keySet = await jwksCache.GetKeysAsync();
        }
        catch
        {
            context.Response.StatusCode = 401;
            return;
        }

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = SupabaseIssuer,
                ValidateAudience = true,
                ValidAudience = "authenticated",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keySet.GetSigningKeys(),
            };
            principal = handler.ValidateToken(token, validationParams, out _);
        }
        catch
        {
            context.Response.StatusCode = 401;
            return;
        }

        var supabaseUserId = principal.FindFirst("sub")?.Value;
        var email = principal.FindFirst("email")?.Value;

        if (string.IsNullOrEmpty(supabaseUserId))
        {
            context.Response.StatusCode = 401;
            return;
        }

        var maps = await mapService.GetMapsForUserAsync(supabaseUserId);
        if (maps.Count == 0)
        {
            context.Response.Headers["X-Onboarding-Required"] = "true";
            context.Response.StatusCode = 401;
            return;
        }

        // Resolve active affiliate: use X-Affiliate-Id header if valid, else oldest by CreatedAt
        var requestedId = context.Request.Headers["X-Affiliate-Id"].FirstOrDefault();
        var activeMap = maps.FirstOrDefault(m => m.AffiliateId.ToString() == requestedId)
                     ?? maps.OrderBy(m => m.CreatedAt).First();

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", supabaseUserId),
                new Claim("email", email ?? string.Empty),
                new Claim("active_affiliate_id", activeMap.AffiliateId.ToString()),
                new Claim("role", activeMap.Role.ToString()),
                new Claim("affiliate_ids", string.Join(",", maps.Select(m => m.AffiliateId))),
            },
            authenticationType: "supabase");

        context.User = new ClaimsPrincipal(identity);
        await _next(context);
    }

    private static string? ExtractBearerToken(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        return authHeader["Bearer ".Length..].Trim();
    }
}
