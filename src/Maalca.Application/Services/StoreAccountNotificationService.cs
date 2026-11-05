using System.Text;
using System.Text.Json;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services;

/// <summary>Ver IStoreAccountNotificationService. Falla en silencio (log + return).</summary>
public class StoreAccountNotificationService : IStoreAccountNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StoreAccountNotificationService> _logger;

    public StoreAccountNotificationService(IHttpClientFactory httpClientFactory, ILogger<StoreAccountNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyApprovedAsync(StoreCustomer account, Affiliate affiliate, string? priceListName)
    {
        if (string.IsNullOrWhiteSpace(account.Email)) return;

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[StoreAccountNotification] Skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var payload = new
            {
                businessName = affiliate.Name,
                slug = affiliate.Slug,
                logoUrl = string.IsNullOrWhiteSpace(affiliate.LogoUrl) ? affiliate.Logo : affiliate.LogoUrl,
                brandColor = affiliate.PrimaryColor,
                language = affiliate.Language ?? "es",
                customerName = string.IsNullOrWhiteSpace(account.BusinessName) ? account.Name : account.BusinessName,
                customerEmail = account.Email,
                priceListName,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/store-account-approved")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[StoreAccountNotification] Failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[StoreAccountNotification] Threw");
        }
    }
}
