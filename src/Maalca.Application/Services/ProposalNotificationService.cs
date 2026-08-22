using System.Text;
using System.Text.Json;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services;

/// <summary>
/// Ver IProposalNotificationService. Falla en silencio (log + return) — un email que no salió
/// nunca debe tumbar el "Enviar" de la propuesta, mismo criterio que InvoiceNotificationService.
/// </summary>
public class ProposalNotificationService : IProposalNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProposalNotificationService> _logger;

    public ProposalNotificationService(IHttpClientFactory httpClientFactory, ILogger<ProposalNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyProposalSentAsync(Proposal proposal, string businessName, string proposalLink)
    {
        if (string.IsNullOrWhiteSpace(proposal.CustomerEmail))
            return; // sin correo del cliente no hay a quien notificar — el link se copia/manda por WhatsApp igual

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[ProposalNotification] Skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var payload = new
            {
                customerEmail = proposal.CustomerEmail,
                customerName = proposal.CustomerName,
                businessName,
                title = proposal.Title,
                description = proposal.Description,
                amount = proposal.Amount,
                currency = proposal.Currency,
                expiresAt = proposal.ExpiresAt,
                proposalLink,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/proposal")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[ProposalNotification] Failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ProposalNotification] Threw");
        }
    }

    public async Task NotifyProposalAcceptedAsync(Proposal proposal, Affiliate affiliate)
    {
        if (string.IsNullOrWhiteSpace(affiliate.ContactEmail))
            return; // sin correo de contacto del negocio no hay a quien avisar

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[ProposalNotification] Accepted-notify skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var payload = new
            {
                businessEmail = affiliate.ContactEmail,
                businessName = affiliate.Name,
                title = proposal.Title,
                amount = proposal.Amount,
                currency = proposal.Currency,
                acceptedByName = proposal.AcceptedByName,
                acceptedAt = proposal.AcceptedAt,
                customerEmail = proposal.CustomerEmail,
                customerPhone = proposal.CustomerPhone,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/proposal-accepted")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[ProposalNotification] Accepted-notify failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ProposalNotification] Accepted-notify threw");
        }
    }
}
