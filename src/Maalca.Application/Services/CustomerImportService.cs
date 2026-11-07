using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class CustomerImportService : ICustomerImportService
{
    private const int MaxRows = 2000;

    private static readonly Dictionary<string, string> BillingCycles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weekly"] = "weekly", ["semanal"] = "weekly",
        ["biweekly"] = "biweekly", ["quincenal"] = "biweekly",
        ["monthly"] = "monthly", ["mensual"] = "monthly",
    };

    private static readonly Dictionary<string, string> DeliveryFrequencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["daily"] = "daily", ["diario"] = "daily", ["diaria"] = "daily",
        ["weekly"] = "weekly", ["semanal"] = "weekly",
        ["on_demand"] = "on_demand", ["por pedido"] = "on_demand", ["pedido"] = "on_demand", ["bajo pedido"] = "on_demand",
    };

    private static readonly Regex Spaces = new("\\s+", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;

    public CustomerImportService(AppDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ImportCustomersResultDto> ImportAsync(
        Guid affiliateId, ImportCustomersRequest request, string? actorId, string? actorName)
    {
        var rows = request.Rows;
        if (rows is null || rows.Count == 0) throw new ArgumentException("No hay filas para importar.");
        if (rows.Count > MaxRows) throw new ArgumentException($"Máximo {MaxRows} filas por importación.");

        // Clientes que el negocio ya tiene, para no duplicarlos.
        var existing = await _db.Customers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId)
            .Select(c => new { c.Name, c.Phone })
            .ToListAsync();
        var seenPhones = new HashSet<string>();
        var seenNames = new HashSet<string>();
        foreach (var c in existing)
        {
            var phone = PhoneKey(c.Phone);
            if (phone is not null) seenPhones.Add(phone);
            seenNames.Add(NameKey(c.Name));
        }

        var results = new List<ImportCustomerRowResultDto>(rows.Count);
        var toCreate = new List<Customer>();
        int newCount = 0, duplicates = 0, invalid = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var n = i + 1;
            var row = rows[i];
            var name = Clean(row.Name);
            var errors = new List<string>();

            if (name is null) errors.Add("falta el nombre");
            else if (name.Length > 200) errors.Add("el nombre es demasiado largo (máx. 200)");

            var phoneRaw = Clean(row.Phone);
            string? phone = null;
            if (phoneRaw is not null)
            {
                phone = PhoneKey(phoneRaw);
                if (phone is null) errors.Add("teléfono inválido (debe tener entre 7 y 15 dígitos)");
            }

            var email = Clean(row.Email);
            if (email is not null && (email.Length > 200 || !email.Contains('@') || email.Contains(' ')))
                errors.Add("correo inválido");

            var rncRaw = Clean(row.Rnc);
            string? rnc = null;
            if (rncRaw is not null)
            {
                rnc = new string(rncRaw.Where(char.IsDigit).ToArray());
                if (rnc.Length is not (9 or 11)) errors.Add("RNC o cédula inválido (9 u 11 dígitos)");
            }

            string? billing = null;
            var billingRaw = Clean(row.BillingCycle);
            if (billingRaw is not null && !BillingCycles.TryGetValue(billingRaw, out billing))
                errors.Add("ciclo de cobro no reconocido (semanal, quincenal o mensual)");

            string? delivery = null;
            var deliveryRaw = Clean(row.DeliveryFrequency);
            if (deliveryRaw is not null && !DeliveryFrequencies.TryGetValue(deliveryRaw, out delivery))
                errors.Add("frecuencia de entrega no reconocida (diaria, semanal o por pedido)");

            if (row.CreditDays is < 0 or > 365) errors.Add("días de crédito fuera de rango (0–365)");

            if (errors.Count > 0)
            {
                invalid++;
                results.Add(new ImportCustomerRowResultDto(n, "invalid", name, Capitalize(string.Join("; ", errors))));
                continue;
            }

            // Duplicado: mismo teléfono; o, si la fila no trae teléfono, mismo nombre.
            string? dupReason = null;
            if (phone is not null)
            {
                if (!seenPhones.Add(phone)) dupReason = "Ya existe un cliente con ese teléfono (o se repite en el archivo).";
            }
            else if (!seenNames.Add(NameKey(name!)))
            {
                dupReason = "Ya existe un cliente con ese nombre (o se repite en el archivo).";
            }
            if (dupReason is not null)
            {
                duplicates++;
                results.Add(new ImportCustomerRowResultDto(n, "duplicate", name, dupReason));
                continue;
            }
            seenNames.Add(NameKey(name!));

            newCount++;
            results.Add(new ImportCustomerRowResultDto(n, "new", name, null));
            toCreate.Add(new Customer
            {
                AffiliateId = affiliateId,
                Name = name!,
                Phone = phone,
                Email = email,
                Rnc = rnc,
                Address = Clean(row.Address),
                Notes = Clean(row.Notes),
                BillingCycle = billing,
                DeliveryFrequency = delivery,
                CreditDays = row.CreditDays,
                Status = "Active",
            });
        }

        var imported = 0;
        if (!request.DryRun && toCreate.Count > 0)
        {
            _db.Customers.AddRange(toCreate);
            await _db.SaveChangesAsync();
            imported = toCreate.Count;

            await _audit.LogAsync(affiliateId, "customers.imported", "Customer", null,
                $"Importación de clientes: {imported} nuevos, {duplicates} duplicados, {invalid} inválidos (de {rows.Count} filas)",
                actorId, actorName);
        }

        return new ImportCustomersResultDto(request.DryRun, rows.Count, newCount, duplicates, invalid, imported, results);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string? Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return Spaces.Replace(s.Trim(), " ");
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Solo dígitos; un número de 11 dígitos que empieza en 1 (código de país RD/US) se guarda sin ese 1.
    /// Null si no queda entre 7 y 15 dígitos. Es la forma en que se guarda y se compara el teléfono importado.</summary>
    private static string? PhoneKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '1') digits = digits[1..];
        return digits.Length is >= 7 and <= 15 ? digits : null;
    }

    /// <summary>Nombre en minúsculas, sin acentos ni espacios dobles, para comparar.</summary>
    private static string NameKey(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return Spaces.Replace(sb.ToString().Trim().ToLowerInvariant(), " ");
    }
}
