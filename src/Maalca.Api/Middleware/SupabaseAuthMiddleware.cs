using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Maalca.Application.Common.Interfaces;
using Maalca.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Maalca.Api.Middleware;

public class SupabaseAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SupabaseAuthMiddleware> _logger;
    private const string SupabaseIssuer = "https://nyiocxrrbrphfczsbqpf.supabase.co/auth/v1";
    private const string InternalIssuer = "maalca-api";

    public SupabaseAuthMiddleware(RequestDelegate next, ILogger<SupabaseAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAffiliateMapService mapService,
        SupabaseJwksCache jwksCache,
        SupabaseTokenVerifier tokenVerifier)
    {
        var token = ExtractBearerToken(context);
        _logger.LogInformation("Auth: token extracted, length {Len}", token?.Length ?? 0);
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
        catch (Exception ex)
        {
            _logger.LogWarning("Auth: JWT validation FAILED: {Reason}", ex.Message);
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

        // Validate Supabase JWT signature using ES256 (ECDSA) with JWKS
        JsonWebKeySet keySet;
        try
        {
            keySet = await jwksCache.GetKeysAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Auth: JWT validation FAILED: {Reason}", ex.Message);
            context.Response.StatusCode = 401;
            return;
        }

        var signingKeys = keySet.GetSigningKeys();

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = true,
                ValidIssuer = SupabaseIssuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha256 },
                IssuerSigningKeyResolver = (_, _, kid, _) =>
                {
                    var matched = signingKeys.Where(k => k.KeyId == kid).ToList();
                    return matched.Any() ? matched : signingKeys;
                },
            };
            principal = handler.ValidateToken(token, validationParams, out _);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Auth: JWT validation FAILED: {Reason}", ex.Message);
            context.Response.StatusCode = 401;
            return;
        }

        _logger.LogInformation("Auth: JWT signature valid for sub {Sub}", principal.FindFirst("sub")?.Value);

        // Verify the token is still active server-side (catches post-logout tokens)
        _logger.LogInformation("Auth: calling tokenVerifier for token");
        if (!await tokenVerifier.IsTokenActiveAsync(token))
        {
            _logger.LogWarning("Auth: tokenVerifier returned INACTIVE, rejecting 401");
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

        // Fase 8 — dashboard multiusuario con roles: si el dueño de otro negocio invitó a este
        // correo antes de que esta persona tuviera cuenta, acá es donde se engancha — DEBE ir
        // antes de GetMapsForUserAsync, porque si no, alguien recién invitado que se acaba de
        // registrar seguiría teniendo 0 mapas en su primer login y esta misma función lo
        // mandaría por error a onboarding (rama de abajo) en vez de dejarlo entrar al negocio
        // al que lo invitaron.
        await mapService.ClaimPendingInvitesAsync(supabaseUserId, email ?? "");

        var maps = await mapService.GetMapsForUserAsync(supabaseUserId);
        _logger.LogInformation("Auth: affiliate map lookup for sub {Sub} returned {Count}", supabaseUserId, maps.Count);
        if (maps.Count == 0)
        {
            // Allow new users to reach the onboarding endpoint (no affiliates yet)
            if (context.Request.Path.StartsWithSegments("/api/onboarding", StringComparison.OrdinalIgnoreCase))
            {
                var partialIdentity = new ClaimsIdentity(
                    new[]
                    {
                        new Claim("sub", supabaseUserId),
                        new Claim("email", email ?? string.Empty),
                    },
                    authenticationType: "supabase");
                context.User = new ClaimsPrincipal(partialIdentity);
                await _next(context);
                return;
            }
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
