using System.Text.RegularExpressions;
using Maalca.Application.Common;

namespace Maalca.Api.Middleware;

/// <summary>
/// Aplica el perfil de acceso (chofer, cajero, almacén, contabilidad) a TODAS las rutas /api/affiliates/{id}/{módulo}:
/// un usuario con perfil solo puede llamar a los módulos de su lista (ver AccessProfiles); el resto responde 403.
/// Va después de SupabaseAuthMiddleware (que pone los claims). El Dueño y los usuarios sin perfil no se tocan.
/// Solo se evalúa cuando la ruta es del negocio activo del usuario: lo demás ya lo rechaza cada endpoint.
/// </summary>
public class ProfileAccessMiddleware
{
    private static readonly Regex Route = new(
        @"^/api/affiliates/(?<id>[0-9a-fA-F\-]{36})/(?<segment>[^/]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly RequestDelegate _next;

    public ProfileAccessMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var profile = context.User.FindFirst("profile")?.Value;
        var role = context.User.FindFirst("role")?.Value;

        if (!string.IsNullOrEmpty(profile) && role != "Owner")
        {
            var m = Route.Match(context.Request.Path.Value ?? string.Empty);
            var active = context.User.FindFirst("active_affiliate_id")?.Value;
            if (m.Success
                && string.Equals(m.Groups["id"].Value, active, StringComparison.OrdinalIgnoreCase)
                && !AccessProfiles.IsAllowed(profile, m.Groups["segment"].Value, context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new { code = "PROFILE_FORBIDDEN", message = "Tu perfil de acceso no incluye esta parte del sistema." },
                });
                return;
            }
        }

        await _next(context);
    }
}
