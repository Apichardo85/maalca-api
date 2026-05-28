using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Maalca.Infrastructure.Auth;

public class SupabaseTokenVerifier
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SupabaseTokenVerifier> _logger;
    private const string UserEndpoint = "https://nyiocxrrbrphfczsbqpf.supabase.co/auth/v1/user";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public SupabaseTokenVerifier(IMemoryCache cache, ILogger<SupabaseTokenVerifier> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsTokenActiveAsync(string token)
    {
        var cacheKey = $"supa-tok-{HashToken(token)}";
        if (_cache.TryGetValue(cacheKey, out bool _))
            return true;

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("apikey",
                Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? "");

            using var request = new HttpRequestMessage(HttpMethod.Get, UserEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await client.SendAsync(request, cts.Token);

            _logger.LogInformation("TokenVerifier: Supabase returned {Status}", response.StatusCode);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return false;

            _cache.Set(cacheKey, true, CacheDuration);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("TokenVerifier: exception {Msg}", ex.Message);
            return false;
        }
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
