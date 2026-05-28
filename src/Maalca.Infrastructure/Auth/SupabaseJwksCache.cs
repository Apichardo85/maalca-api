using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Maalca.Infrastructure.Auth;

public class SupabaseJwksCache
{
    private readonly IMemoryCache _cache;
    private const string JwksUrl = "https://nyiocxrrbrphfczsbqpf.supabase.co/auth/v1/.well-known/jwks.json";
    private const string CacheKey = "supabase-jwks";

    public SupabaseJwksCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<JsonWebKeySet> GetKeysAsync()
    {
        if (_cache.TryGetValue(CacheKey, out JsonWebKeySet? cached) && cached is not null)
            return cached;

        string json;
        try
        {
            using var client = new HttpClient();
            var apiKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? "";
            client.DefaultRequestHeaders.Add("apikey", apiKey);
            var response = await client.GetAsync(JwksUrl);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Supabase JWKS fetch failed: HTTP {(int)response.StatusCode} from {JwksUrl}");
            json = await response.Content.ReadAsStringAsync();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to reach Supabase JWKS endpoint at {JwksUrl}: {ex.Message}", ex);
        }

        var keySet = new JsonWebKeySet(json);
        _cache.Set(CacheKey, keySet, TimeSpan.FromHours(24));
        return keySet;
    }
}
