using System.Text;
using System.Text.Json;
using Maalca.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Creator;

/// <summary>
/// Correos del club de lectores. Igual que el resto de notificaciones: el API arma los datos y los
/// enlaces firmados, y la web (maalca-web, /api/internal/notifications/reader-*) los manda por Resend.
/// Si faltan MAALCA_WEB_URL o INTERNAL_NOTIFICATIONS_SECRET, no se manda y se devuelve false: el
/// llamador decide (el acceso sigue respondiendo "revisa tu correo"; la carta queda con error).
/// </summary>
public interface IReaderMailer
{
    /// <summary>kind: "confirm" (doble opt-in) | "login" (entrar).</summary>
    Task<bool> SendAccessAsync(Affiliate author, Reader reader, string kind, string url);

    /// <summary>Una carta a varios lectores en una sola llamada (la web la parte en lotes de Resend). Devuelve los ids que salieron.</summary>
    Task<IReadOnlyCollection<Guid>> SendLetterAsync(Affiliate author, ReaderLetter letter, IReadOnlyList<ReaderLetterRecipient> recipients);
}

public record ReaderLetterRecipient(Guid ReaderId, string Email, string? Name, string Language, string UnsubscribeUrl);

public sealed class ReaderMailer : IReaderMailer
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ReaderMailer> _logger;

    public ReaderMailer(IHttpClientFactory http, ILogger<ReaderMailer> logger)
    {
        _http = http;
        _logger = logger;
    }

    public static string? WebUrl => Environment.GetEnvironmentVariable("MAALCA_WEB_URL")?.TrimEnd('/');

    public async Task<bool> SendAccessAsync(Affiliate author, Reader reader, string kind, string url)
    {
        var payload = new
        {
            kind,
            url,
            email = reader.Email,
            name = reader.Name,
            language = reader.Language,
            author = Brand(author),
        };
        var res = await PostAsync("reader-access", payload);
        return res is not null;
    }

    public async Task<IReadOnlyCollection<Guid>> SendLetterAsync(Affiliate author, ReaderLetter letter, IReadOnlyList<ReaderLetterRecipient> recipients)
    {
        if (recipients.Count == 0) return Array.Empty<Guid>();
        var payload = new
        {
            subject = letter.Subject,
            bodyHtml = letter.BodyHtml ?? string.Empty,
            author = Brand(author),
            recipients = recipients.Select(r => new { readerId = r.ReaderId, email = r.Email, name = r.Name, language = r.Language, unsubscribeUrl = r.UnsubscribeUrl }),
        };
        var res = await PostAsync("reader-letter", payload);
        if (res is null) return Array.Empty<Guid>();
        try
        {
            using var doc = JsonDocument.Parse(res);
            if (doc.RootElement.TryGetProperty("sent", out var sent) && sent.ValueKind == JsonValueKind.Array)
                return sent.EnumerateArray()
                    .Select(e => Guid.TryParse(e.GetString(), out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[ReaderMailer] respuesta de reader-letter no es JSON");
        }
        return Array.Empty<Guid>();
    }

    private static object Brand(Affiliate a) => new
    {
        name = a.Name,
        slug = a.Slug,
        logoUrl = string.IsNullOrWhiteSpace(a.LogoUrl) ? a.Logo : a.LogoUrl,
        color = a.PrimaryColor,
        replyTo = a.ContactEmail,
    };

    private async Task<string?> PostAsync(string route, object payload)
    {
        var baseUrl = WebUrl;
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[ReaderMailer] Sin MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET: no se manda {Route}", route);
            return null;
        }

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/internal/notifications/{route}") { Content = content };
            request.Headers.Add("X-Internal-Secret", secret);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[ReaderMailer] {Route} → {Status}: {Body}", route, (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
                return null;
            }
            return body;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "[ReaderMailer] {Route} falló", route);
            return null;
        }
    }
}
