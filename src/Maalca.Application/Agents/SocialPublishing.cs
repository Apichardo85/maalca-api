using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Maalca.Application.Common;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Agents;

// Fase 4 del estudio de contenido: publicar en redes lo que un humano aprobó.
//
// Facebook (Graph API de Meta):
//   1. El staff pulsa "Conectar Facebook" en /ops/negocios/{id}/marca. El API arma la URL de autorización de Meta
//      con un "state" firmado (negocio + hora + HMAC) para que nadie pueda colar otra cuenta en el callback.
//   2. Meta devuelve el código a /api/integrations/meta/callback. El API lo cambia por un token de usuario de larga
//      duración y lo guarda CIFRADO en IntegrationConnections (Provider = "facebook", Status = "pending_page").
//   3. El staff elige la página. El token de página que se saca de un token de usuario de larga duración no vence:
//      se guarda cifrado, con el id y el nombre de la página, y la conexión queda "connected".
//   4. SocialPublishWorker publica cada minuto las piezas aprobadas cuya fecha ya llegó.
// Nada se publica sin pasar por Approved, y Published solo lo pone este código cuando Meta confirma el post.

public record SocialConnectionDto(string Provider, string Status, string? PageId, string? PageName, string? LastError,
    DateTime? ConnectedAt, bool AppConfigured);

public record SocialPageDto(string Id, string Name, string? Category);

public record SelectPageRequest(string PageId);

public record AuthorizeUrlDto(string Url);

public interface ISocialConnectService
{
    Task<SocialConnectionDto> GetFacebookAsync(Guid affiliateId);
    Task<AuthorizeUrlDto> BuildFacebookAuthorizeUrlAsync(Guid affiliateId);
    /// <summary>Procesa el regreso de Meta. Devuelve la URL de la web a la que hay que mandar al navegador.</summary>
    Task<string> HandleFacebookCallbackAsync(string? code, string? state, string? error);
    Task<List<SocialPageDto>> ListFacebookPagesAsync(Guid affiliateId);
    Task<SocialConnectionDto> SelectFacebookPageAsync(Guid affiliateId, SelectPageRequest request, string? by);
    Task DisconnectFacebookAsync(Guid affiliateId);
}

public interface ISocialPublisher
{
    /// <summary>Publica las piezas aprobadas cuya fecha ya llegó. Devuelve cuántas salieron.</summary>
    Task<int> PublishDueAsync(CancellationToken ct);
    /// <summary>Publica una pieza aprobada ahora mismo, sin esperar su fecha.</summary>
    Task<ContentItemDto> PublishNowAsync(Guid contentItemId, string? by, CancellationToken ct);
}

/// <summary>Configuración de la app de Meta. Variables en Railway: Meta__AppId, Meta__AppSecret, Meta__RedirectUri.</summary>
public sealed class MetaSettings
{
    public const string HttpClientName = "meta-graph";
    public string AppId { get; }
    public string AppSecret { get; }
    public string RedirectUri { get; }
    public string GraphVersion { get; }
    public string WebUrl { get; }
    public string StateSecret { get; }

    public MetaSettings(IConfiguration config)
    {
        AppId = config["Meta:AppId"] ?? string.Empty;
        AppSecret = config["Meta:AppSecret"] ?? string.Empty;
        RedirectUri = config["Meta:RedirectUri"] ?? string.Empty;
        GraphVersion = string.IsNullOrWhiteSpace(config["Meta:GraphVersion"]) ? "v23.0" : config["Meta:GraphVersion"]!;
        WebUrl = (Environment.GetEnvironmentVariable("MAALCA_WEB_URL") ?? config["MAALCA_WEB_URL"] ?? "https://maalca.com").TrimEnd('/');
        StateSecret = config["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
    }

    public bool IsConfigured => AppId.Length > 0 && AppSecret.Length > 0 && RedirectUri.Length > 0;
    public string Graph => $"https://graph.facebook.com/{GraphVersion}";
}

internal static class FacebookProvider
{
    public const string Name = "facebook";
    public const string Kind = "social";
    public const string StatusPendingPage = "pending_page";
    public const string StatusConnected = "connected";
    public const string StatusError = "error";

    // Lo mínimo para listar las páginas del usuario y publicar en ellas.
    public const string Scopes = "pages_show_list,pages_read_engagement,pages_manage_posts";

    /// <summary>Credenciales cifradas: un JSON con el token de usuario y, cuando ya se eligió, el de la página.</summary>
    public record Credentials(string? UserToken, string? PageToken);
}

public class SocialConnectService : ISocialConnectService
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(20);

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly MetaSettings _meta;
    private readonly ILogger<SocialConnectService> _logger;

    public SocialConnectService(AppDbContext db, IHttpClientFactory http, MetaSettings meta, ILogger<SocialConnectService> logger)
    {
        _db = db;
        _http = http;
        _meta = meta;
        _logger = logger;
    }

    public async Task<SocialConnectionDto> GetFacebookAsync(Guid affiliateId)
    {
        await EnsureAffiliateAsync(affiliateId);
        var c = await _db.IntegrationConnections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == FacebookProvider.Name);
        return ToDto(c);
    }

    public async Task<AuthorizeUrlDto> BuildFacebookAuthorizeUrlAsync(Guid affiliateId)
    {
        await EnsureAffiliateAsync(affiliateId);
        if (!_meta.IsConfigured)
            throw new ArgumentException("La app de Meta no está configurada en el API (Meta__AppId, Meta__AppSecret, Meta__RedirectUri).");

        var state = SignState(affiliateId, DateTimeOffset.UtcNow);
        var url = "https://www.facebook.com/" + _meta.GraphVersion + "/dialog/oauth"
                  + "?client_id=" + Uri.EscapeDataString(_meta.AppId)
                  + "&redirect_uri=" + Uri.EscapeDataString(_meta.RedirectUri)
                  + "&state=" + Uri.EscapeDataString(state)
                  + "&scope=" + Uri.EscapeDataString(FacebookProvider.Scopes)
                  + "&response_type=code";
        return new AuthorizeUrlDto(url);
    }

    public async Task<string> HandleFacebookCallbackAsync(string? code, string? state, string? error)
    {
        if (!TryReadState(state, out var affiliateId))
            return $"{_meta.WebUrl}/ops?facebook=invalid_state";

        var back = $"{_meta.WebUrl}/ops/negocios/{affiliateId}/marca";
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            return back + "?facebook=cancelled";

        try
        {
            var client = _http.CreateClient(MetaSettings.HttpClientName);

            // Código → token de usuario de corta duración.
            var shortToken = await GetTokenAsync(client, _meta.Graph + "/oauth/access_token"
                + "?client_id=" + Uri.EscapeDataString(_meta.AppId)
                + "&redirect_uri=" + Uri.EscapeDataString(_meta.RedirectUri)
                + "&client_secret=" + Uri.EscapeDataString(_meta.AppSecret)
                + "&code=" + Uri.EscapeDataString(code));

            // Corta → larga duración (~60 días). Los tokens de página que salen de este no vencen.
            var longToken = await GetTokenAsync(client, _meta.Graph + "/oauth/access_token?grant_type=fb_exchange_token"
                + "&client_id=" + Uri.EscapeDataString(_meta.AppId)
                + "&client_secret=" + Uri.EscapeDataString(_meta.AppSecret)
                + "&fb_exchange_token=" + Uri.EscapeDataString(shortToken));

            var c = await _db.IntegrationConnections
                .FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == FacebookProvider.Name);
            if (c is null)
            {
                c = new IntegrationConnection { Id = Guid.NewGuid(), AffiliateId = affiliateId, Provider = FacebookProvider.Name, Kind = FacebookProvider.Kind };
                _db.IntegrationConnections.Add(c);
            }
            c.CredentialsEnc = Protect(new FacebookProvider.Credentials(longToken, null));
            c.CredentialsLast4 = longToken.Length >= 4 ? longToken[^4..] : null;
            c.ExternalId = null;
            c.ExternalName = null;
            c.Status = FacebookProvider.StatusPendingPage;
            c.LastError = null;
            c.IsActive = true;
            c.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return back + "?facebook=choose_page";
        }
        catch (MetaApiException ex)
        {
            _logger.LogWarning("[Meta] callback falló para {Affiliate}: {Error}", affiliateId, ex.Message);
            return back + "?facebook=error";
        }
    }

    public async Task<List<SocialPageDto>> ListFacebookPagesAsync(Guid affiliateId)
    {
        var c = await RequireConnectionAsync(affiliateId);
        var creds = Unprotect(c);
        if (string.IsNullOrEmpty(creds?.UserToken))
            throw new ArgumentException("Vuelve a conectar Facebook: falta la autorización del usuario.");

        var pages = await FetchPagesAsync(creds.UserToken);
        return pages.Select(p => new SocialPageDto(p.Id, p.Name, p.Category)).ToList();
    }

    public async Task<SocialConnectionDto> SelectFacebookPageAsync(Guid affiliateId, SelectPageRequest request, string? by)
    {
        var c = await RequireConnectionAsync(affiliateId);
        var creds = Unprotect(c);
        if (string.IsNullOrEmpty(creds?.UserToken))
            throw new ArgumentException("Vuelve a conectar Facebook: falta la autorización del usuario.");

        var pages = await FetchPagesAsync(creds.UserToken);
        var page = pages.FirstOrDefault(p => p.Id == request.PageId)
            ?? throw new ArgumentException("Esa página no está entre las que administra la cuenta conectada.");
        if (string.IsNullOrEmpty(page.AccessToken))
            throw new ArgumentException("Meta no entregó permiso de publicación para esa página. Revisa que seas administrador de la página.");

        c.CredentialsEnc = Protect(new FacebookProvider.Credentials(creds.UserToken, page.AccessToken));
        c.CredentialsLast4 = page.AccessToken.Length >= 4 ? page.AccessToken[^4..] : null;
        c.ExternalId = page.Id;
        c.ExternalName = page.Name;
        c.Status = FacebookProvider.StatusConnected;
        c.LastError = null;
        c.LastSyncAt = DateTime.UtcNow;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("[Meta] {Affiliate} conectó la página {Page} ({By})", affiliateId, page.Name, by);
        return ToDto(c);
    }

    public async Task DisconnectFacebookAsync(Guid affiliateId)
    {
        var c = await _db.IntegrationConnections
            .FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == FacebookProvider.Name);
        if (c is null) return;
        _db.IntegrationConnections.Remove(c);
        await _db.SaveChangesAsync();
    }

    // ---------- Meta ----------

    private record GraphPage(string Id, string Name, string? Category, string? AccessToken);

    private async Task<List<GraphPage>> FetchPagesAsync(string userToken)
    {
        var client = _http.CreateClient(MetaSettings.HttpClientName);
        var url = _meta.Graph + "/me/accounts?fields=id,name,category,access_token&limit=100&access_token=" + Uri.EscapeDataString(userToken);
        using var doc = await MetaGraph.GetJsonAsync(client, url);
        var list = new List<GraphPage>();
        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            foreach (var p in data.EnumerateArray())
            {
                list.Add(new GraphPage(
                    p.GetProperty("id").GetString() ?? string.Empty,
                    p.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                    p.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                    p.TryGetProperty("access_token", out var t) ? t.GetString() : null));
            }
        }
        return list;
    }

    private static async Task<string> GetTokenAsync(HttpClient client, string url)
    {
        using var doc = await MetaGraph.GetJsonAsync(client, url);
        return doc.RootElement.TryGetProperty("access_token", out var t) && t.GetString() is { Length: > 0 } token
            ? token
            : throw new MetaApiException("Meta no devolvió un token.", null);
    }

    // ---------- state firmado ----------

    private string SignState(Guid affiliateId, DateTimeOffset at)
    {
        var payload = $"{affiliateId:N}.{at.ToUnixTimeSeconds()}";
        return payload + "." + Hmac(payload);
    }

    private bool TryReadState(string? state, out Guid affiliateId)
    {
        affiliateId = Guid.Empty;
        var parts = state?.Split('.');
        if (parts is not { Length: 3 }) return false;
        var payload = parts[0] + "." + parts[1];
        var expected = Hmac(payload);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2]))) return false;
        if (!long.TryParse(parts[1], out var unix)) return false;
        if (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix) > StateTtl) return false;
        return Guid.TryParseExact(parts[0], "N", out affiliateId);
    }

    private string Hmac(string payload)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("maalca-meta-state-v1|" + _meta.StateSecret));
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    // ---------- utilidades ----------

    private async Task EnsureAffiliateAsync(Guid affiliateId)
    {
        if (!await _db.Affiliates.AnyAsync(a => a.Id == affiliateId)) throw new KeyNotFoundException("Ese negocio no existe.");
    }

    private async Task<IntegrationConnection> RequireConnectionAsync(Guid affiliateId)
    {
        await EnsureAffiliateAsync(affiliateId);
        return await _db.IntegrationConnections
                   .FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == FacebookProvider.Name)
               ?? throw new ArgumentException("Este negocio no tiene Facebook conectado.");
    }

    private string Protect(FacebookProvider.Credentials creds) =>
        SecretBox.Protect(JsonSerializer.Serialize(creds), _meta.StateSecret);

    private FacebookProvider.Credentials? Unprotect(IntegrationConnection c) =>
        SocialCredentials.Read(c, _meta.StateSecret);

    private SocialConnectionDto ToDto(IntegrationConnection? c) => c is null
        ? new SocialConnectionDto(FacebookProvider.Name, "disconnected", null, null, null, null, _meta.IsConfigured)
        : new SocialConnectionDto(FacebookProvider.Name, c.Status, c.ExternalId, c.ExternalName, c.LastError,
            c.UpdatedAt ?? c.CreatedAt, _meta.IsConfigured);
}

internal static class SocialCredentials
{
    public static FacebookProvider.Credentials? Read(IntegrationConnection c, string secret)
    {
        var json = SecretBox.Unprotect(c.CredentialsEnc, secret);
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<FacebookProvider.Credentials>(json); }
        catch (JsonException) { return null; }
    }
}

/// <summary>Error que devuelve la Graph API, con su código (190 = token inválido o vencido).</summary>
public class MetaApiException : Exception
{
    public int? Code { get; }
    public MetaApiException(string message, int? code) : base(message) => Code = code;
    public bool IsAuthError => Code is 190 or 102 or 10 or 200;
}

internal static class MetaGraph
{
    public static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url)
    {
        using var res = await client.GetAsync(url);
        return await ReadAsync(res);
    }

    public static async Task<JsonDocument> PostFormAsync(HttpClient client, string url, Dictionary<string, string> form)
    {
        using var res = await client.PostAsync(url, new FormUrlEncodedContent(form));
        return await ReadAsync(res);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body); }
        catch (JsonException) { throw new MetaApiException($"Respuesta inesperada de Meta (HTTP {(int)res.StatusCode}).", null); }

        if (doc.RootElement.TryGetProperty("error", out var err))
        {
            var msg = err.TryGetProperty("message", out var m) ? m.GetString() : null;
            int? code = err.TryGetProperty("code", out var c) && c.TryGetInt32(out var ci) ? ci : null;
            doc.Dispose();
            throw new MetaApiException(msg ?? $"Error de Meta (HTTP {(int)res.StatusCode}).", code);
        }
        if (!res.IsSuccessStatusCode)
        {
            doc.Dispose();
            throw new MetaApiException($"Meta respondió HTTP {(int)res.StatusCode}.", null);
        }
        return doc;
    }
}

public class SocialPublisher : ISocialPublisher
{
    /// <summary>Tras este número de fallos seguidos, el worker deja la pieza quieta hasta que el staff pulse "Publicar ahora".</summary>
    public const int MaxAttempts = 3;
    private const int BatchSize = 10;

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly MetaSettings _meta;
    private readonly ILogger<SocialPublisher> _logger;

    public SocialPublisher(AppDbContext db, IHttpClientFactory http, MetaSettings meta, ILogger<SocialPublisher> logger)
    {
        _db = db;
        _http = http;
        _meta = meta;
        _logger = logger;
    }

    public async Task<int> PublishDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // Solo Facebook por ahora; las demás redes esperan su conector y se quedan en Approved.
        var due = await _db.ContentItems
            .Where(c => c.Status == ContentStatus.Approved && c.Network == FacebookProvider.Name
                        && c.ScheduledFor <= now && c.PublishAttempts < MaxAttempts)
            .OrderBy(c => c.ScheduledFor)
            .Take(BatchSize)
            .ToListAsync(ct);

        var published = 0;
        foreach (var item in due)
        {
            if (ct.IsCancellationRequested) break;
            if (await TryPublishAsync(item, ct)) published++;
        }
        return published;
    }

    public async Task<ContentItemDto> PublishNowAsync(Guid contentItemId, string? by, CancellationToken ct)
    {
        var item = await _db.ContentItems.FirstOrDefaultAsync(c => c.Id == contentItemId, ct)
            ?? throw new KeyNotFoundException("Esa pieza no existe.");
        if (item.Status == ContentStatus.Published) throw new ArgumentException("Esa pieza ya está publicada.");
        if (item.Status != ContentStatus.Approved) throw new ArgumentException("Solo se publica una pieza aprobada.");
        if (item.Network != FacebookProvider.Name)
            throw new ArgumentException($"Todavía no hay conector para publicar en {item.Network}. Por ahora solo Facebook.");

        // Publicar a mano reinicia los reintentos: es la salida cuando el worker se rindió.
        item.PublishAttempts = 0;
        item.UpdatedBy = by;
        await TryPublishAsync(item, ct);

        var name = await _db.Affiliates.AsNoTracking().Where(a => a.Id == item.AffiliateId).Select(a => a.Name).FirstOrDefaultAsync(ct) ?? "—";
        return ContentStudioService.ToDtoPublic(item, name);
    }

    private async Task<bool> TryPublishAsync(ContentItem item, CancellationToken ct)
    {
        var conn = await _db.IntegrationConnections
            .FirstOrDefaultAsync(x => x.AffiliateId == item.AffiliateId && x.Provider == FacebookProvider.Name && x.IsActive, ct);
        var creds = conn is null ? null : SocialCredentials.Read(conn, _meta.StateSecret);

        if (conn is null || conn.Status != FacebookProvider.StatusConnected || string.IsNullOrEmpty(creds?.PageToken) || string.IsNullOrEmpty(conn.ExternalId))
        {
            // Sin página conectada no es un fallo de la pieza: no gasta intentos, solo deja el aviso.
            const string msg = "El negocio no tiene una página de Facebook conectada. Conéctala en Marca y marketing.";
            if (item.PublishError != msg)
            {
                item.PublishError = msg;
                item.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            return false;
        }

        item.LastPublishAttemptAt = DateTime.UtcNow;
        try
        {
            var postId = await PostToFacebookAsync(conn.ExternalId!, creds!.PageToken!, item, ct);
            item.Status = ContentStatus.Published;
            item.PublishedAt = DateTime.UtcNow;
            item.ExternalPostId = postId;
            item.ExternalUrl = $"https://www.facebook.com/{postId}";
            item.PublishError = null;
            item.UpdatedAt = DateTime.UtcNow;
            conn.LastSyncAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("[SocialPublisher] publicado {Item} en {Page} como {Post}", item.Id, conn.ExternalName, postId);
            return true;
        }
        catch (MetaApiException ex)
        {
            item.PublishAttempts++;
            item.PublishError = ex.IsAuthError
                ? "Facebook rechazó el permiso de la página (token vencido o revocado). Vuelve a conectar Facebook."
                : $"Facebook no aceptó la publicación: {ex.Message}";
            item.UpdatedAt = DateTime.UtcNow;
            if (ex.IsAuthError)
            {
                conn.Status = FacebookProvider.StatusError;
                conn.LastError = ex.Message;
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("[SocialPublisher] {Item} falló (intento {N}): {Error}", item.Id, item.PublishAttempts, ex.Message);
            return false;
        }
        catch (HttpRequestException ex)
        {
            item.PublishAttempts++;
            item.PublishError = "No se pudo contactar a Facebook. Se reintenta en el próximo ciclo.";
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning(ex, "[SocialPublisher] {Item} sin red con Meta", item.Id);
            return false;
        }
    }

    private async Task<string> PostToFacebookAsync(string pageId, string pageToken, ContentItem item, CancellationToken ct)
    {
        var client = _http.CreateClient(MetaSettings.HttpClientName);
        var text = string.IsNullOrWhiteSpace(item.Hashtags) ? item.Caption : item.Caption + "\n\n" + item.Hashtags;
        var hasMedia = !string.IsNullOrWhiteSpace(item.MediaUrl);

        string url;
        var form = new Dictionary<string, string> { ["access_token"] = pageToken };
        if (item.MediaKind == "image" && hasMedia)
        {
            url = $"{_meta.Graph}/{pageId}/photos";
            form["url"] = item.MediaUrl!;
            form["caption"] = text;
        }
        else if (item.MediaKind == "video" && hasMedia)
        {
            url = $"{_meta.Graph}/{pageId}/videos";
            form["file_url"] = item.MediaUrl!;
            form["description"] = text;
        }
        else
        {
            if (item.MediaKind is "image" or "video")
                throw new MetaApiException($"La pieza pide {(item.MediaKind == "image" ? "imagen" : "video")} pero no tiene el archivo (MediaUrl).", null);
            url = $"{_meta.Graph}/{pageId}/feed";
            form["message"] = text;
        }

        using var doc = await MetaGraph.PostFormAsync(client, url, form);
        var root = doc.RootElement;
        // /feed devuelve id; /photos devuelve id (de la foto) y post_id; /videos devuelve id del video.
        if (root.TryGetProperty("post_id", out var pid) && pid.GetString() is { Length: > 0 } postId) return postId;
        if (root.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } rawId) return rawId;
        throw new MetaApiException("Meta no devolvió el id del post.", null);
    }
}
