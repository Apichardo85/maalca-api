namespace Maalca.Application.Common;

/// <summary>
/// Mismo criterio que maalca-web/src/lib/phone.ts: EE.UU./Rep. Dominicana (plan NANP, 10 dígitos
/// con código de área 2-9) o internacional con "+" (8 a 15 dígitos). Acepta máscaras como
/// "(809) 555-1234" y el prefijo de país 1.
/// </summary>
public static class PhoneRules
{
    public static bool IsValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var v = phone.Trim();
        var digits = new string(v.Where(char.IsDigit).ToArray());
        if (v.StartsWith('+')) return digits.Length is >= 8 and <= 15;
        if (digits.Length == 11 && digits[0] == '1') digits = digits[1..];
        return digits.Length == 10 && digits[0] >= '2';
    }
}
