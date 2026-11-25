namespace Maalca.Domain.Common;

/// <summary>
/// Llave canónica de un contacto. Reconocer al mismo cliente dos veces no puede depender de cómo
/// escribió su teléfono: "(908) 560-9244", "908-560-9244", "9085609244" y "+1 908 560 9244" son la
/// MISMA persona, y comparar el texto tal cual creaba un cliente nuevo cada vez.
///
/// IMPORTANTE: la misma regla vive en SQL, en las columnas generadas Customers."PhoneKey" y
/// Customers."EmailKey" (migración AddCustomerContactKeys) y en el relleno de Orders."CustomerId"
/// de esa migración. Si cambia una, hay que cambiar las dos o los dos lados dejan de coincidir.
/// </summary>
public static class ContactKey
{
    /// <summary>
    /// Solo los dígitos y, si quedan 10 o más, los últimos 10 — eso quita el 1 de país de EE.UU./RD
    /// (ambos son NANP, 10 dígitos reales). Menos de 10 dígitos se deja tal cual: no hay forma de
    /// saber qué falta, y un número corto sigue sirviendo para reconocer al cliente de ese negocio.
    /// Null cuando no hay ningún dígito (un "n/a" escrito a mano no es una identidad).
    /// </summary>
    public static string? Phone(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var digits = new string(raw.Where(c => c is >= '0' and <= '9').ToArray());
        if (digits.Length == 0) return null;
        return digits.Length >= 10 ? digits[^10..] : digits;
    }

    /// <summary>Minúsculas y sin espacios al borde. Null si no hay correo.</summary>
    public static string? Email(string? raw)
    {
        var trimmed = raw?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
