using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Maalca.Infrastructure.Auth;

public class SupabaseJwksCache
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly string _jwksUrl;
    private const string CacheKey = "supabase-jwks";

    public SupabaseJwksCache(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;

        var projectUrl = configuration["Supabase:ProjectUrl"];
        var jwksPath = configuration["Supabase:JwksPath"];

        if (string.IsNullOrWhiteSpace(projectUrl))
            throw new InvalidOperationException(
                "Supabase:ProjectUrl is not configured. Add it to appsettings.json or environment variables.");

        if (string.IsNullOrWhiteSpace(jwksPath))
            throw new InvalidOperationException(
                "Supabase:JwksPath is not configured. Add it to appsettings.json or environment variables.");

        _jwksUrl = $"{projectUrl.TrimEnd('/')}{jwksPath}";
    }

    // TODO: race condition mitigation con SemaphoreSlim si bajo
    //       carga descubrimos N fetches concurrentes al inicio
    public async Task<JsonWebKeySet> GetKeysAsync()
    {
        if (_cache.TryGetValue(CacheKey, out JsonWebKeySet? cached) && cached is not null)
            return cached;

        var client = _httpClientFactory.CreateClient();

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(_jwksUrl);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to reach Supabase JWKS endpoint at {_jwksUrl}: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Supabase JWKS fetch failed: HTTP {(int)response.StatusCode} from {_jwksUrl}");

        var json = await response.Content.ReadAsStringAsync();
        var keySet = new JsonWebKeySet(json);

        _cache.Set(CacheKey, keySet, TimeSpan.FromHours(24));
        return keySet;
    }
}
