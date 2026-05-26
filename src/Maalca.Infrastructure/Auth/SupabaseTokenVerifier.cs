using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Maalca.Infrastructure.Auth;

public class SupabaseTokenVerifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly string _userEndpoint;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public SupabaseTokenVerifier(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;

        var projectUrl = configuration["Supabase:ProjectUrl"]
            ?? throw new InvalidOperationException("Supabase:ProjectUrl is not configured.");

        _userEndpoint = $"{projectUrl.TrimEnd('/')}/auth/v1/user";
    }

    /// <summary>
    /// Returns false if Supabase reports the token as revoked (HTTP 401).
    /// Positive results are cached for 60 s to avoid hammering Supabase.
    /// Network failures are treated as revoked (fail-closed).
    /// </summary>
    public async Task<bool> IsTokenActiveAsync(string token)
    {
        var cacheKey = $"supa-tok-{HashToken(token)}";

        if (_cache.TryGetValue(cacheKey, out bool _))
            return true;

        var client = _httpClientFactory.CreateClient();

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _userEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            response = await client.SendAsync(request, cts.Token);
        }
        catch
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return false;

        _cache.Set(cacheKey, true, CacheDuration);
        return true;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
