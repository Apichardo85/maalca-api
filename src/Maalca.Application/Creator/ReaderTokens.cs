using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Maalca.Application.Creator;

/// <summary>
/// Tokens firmados (HMAC-SHA256) del club de lectores. Sin tabla: el token lleva propósito, lector,
/// autor y vencimiento, y la firma impide fabricarlos. Llave derivada de Jwt:Key con un rótulo propio,
/// así un token de lector nunca sirve como JWT del dashboard ni al revés.
///   - "session": sesión del lector en la página del autor (cookie httpOnly en la web), 90 días.
///   - "login":   enlace para entrar que llega por correo, 30 minutos.
///   - "unsub":   enlace para darse de baja de las cartas, no vence.
/// </summary>
public sealed class ReaderTokens
{
    public static readonly TimeSpan SessionLife = TimeSpan.FromDays(90);
    public static readonly TimeSpan LoginLife = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan UnsubLife = TimeSpan.FromDays(3650);

    private readonly byte[] _key;

    public ReaderTokens(IConfiguration config)
    {
        var secret = config["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
        _key = SHA256.HashData(Encoding.UTF8.GetBytes("maalca-reader-token-v1|" + secret));
    }

    public string Session(Guid readerId, Guid affiliateId) => Issue("session", readerId, affiliateId, SessionLife);
    public string Login(Guid readerId, Guid affiliateId) => Issue("login", readerId, affiliateId, LoginLife);
    public string Unsubscribe(Guid readerId, Guid affiliateId) => Issue("unsub", readerId, affiliateId, UnsubLife);

    /// <summary>null si la firma no cuadra, el propósito es otro o ya venció.</summary>
    public (Guid ReaderId, Guid AffiliateId)? Read(string? token, string purpose)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 400) return null;
        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return null;

        byte[] payload, sig;
        try
        {
            payload = FromB64Url(token[..dot]);
            sig = FromB64Url(token[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(_key, payload);
        if (!CryptographicOperations.FixedTimeEquals(expected, sig)) return null;

        var parts = Encoding.UTF8.GetString(payload).Split('|');
        if (parts.Length != 4 || parts[0] != purpose) return null;
        if (!Guid.TryParse(parts[1], out var readerId) || !Guid.TryParse(parts[2], out var affiliateId)) return null;
        if (!long.TryParse(parts[3], out var exp) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return null;
        return (readerId, affiliateId);
    }

    private string Issue(string purpose, Guid readerId, Guid affiliateId, TimeSpan life)
    {
        var exp = DateTimeOffset.UtcNow.Add(life).ToUnixTimeSeconds();
        var payload = Encoding.UTF8.GetBytes($"{purpose}|{readerId:N}|{affiliateId:N}|{exp}");
        var sig = HMACSHA256.HashData(_key, payload);
        return $"{ToB64Url(payload)}.{ToB64Url(sig)}";
    }

    private static string ToB64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromB64Url(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
        return Convert.FromBase64String(b64);
    }
}
