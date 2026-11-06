using System.Security.Cryptography;
using System.Text;

namespace Maalca.Application.Common;

/// <summary>
/// Cifra secretos de terceros (API keys de agregadores) en la base de datos con AES-GCM. La llave se deriva de un
/// secreto que el API ya tiene (Jwt:Key), así no hay variables nuevas que configurar en Railway.
/// Formato: base64(nonce 12 B | tag 16 B | cifrado).
/// </summary>
public static class SecretBox
{
    private static byte[] Key(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes("maalca-carrier-v1|" + secret));

    public static string Protect(string plain, string secret)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[data.Length];
        using var aes = new AesGcm(Key(secret), 16);
        aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public static string? Unprotect(string? payload, string secret)
    {
        if (string.IsNullOrEmpty(payload)) return null;
        try
        {
            var raw = Convert.FromBase64String(payload);
            if (raw.Length < 28) return null;
            var nonce = raw[..12];
            var tag = raw[12..28];
            var cipher = raw[28..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(Key(secret), 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException) { return null; }
        catch (FormatException) { return null; }
    }
}
