using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class ContractService : IContractService
{
    private static readonly string[] DeliveryFrequencies = { "daily", "weekly", "on_demand" };
    private static readonly string[] BillingCycles = { "weekly", "biweekly", "monthly" };

    private readonly AppDbContext _db;

    public ContractService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ContractDto>> GetAllAsync(Guid affiliateId, Guid? customerId, bool? active)
    {
        var q = _db.Contracts.AsNoTracking().Include(c => c.Customer).Where(c => c.AffiliateId == affiliateId);
        if (customerId.HasValue) q = q.Where(c => c.CustomerId == customerId.Value);
        if (active.HasValue) q = q.Where(c => c.IsActive == active.Value);
        var list = await q.OrderByDescending(c => c.StartDate).ThenByDescending(c => c.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ContractDto?> GetAsync(Guid affiliateId, Guid contractId)
    {
        var c = await _db.Contracts.AsNoTracking().Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == contractId && x.AffiliateId == affiliateId);
        return c is null ? null : ToDto(c);
    }

    public async Task<ContractDto> CreateAsync(Guid affiliateId, UpsertContractRequest request, string? actorId)
    {
        var agreed = Validate(request);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.AffiliateId == affiliateId)
            ?? throw new ArgumentException("El cliente no existe en este negocio.");
        if (request.IsActive) await EnsureNoOtherActiveAsync(affiliateId, request.CustomerId, null);

        var contract = new Contract
        {
            AffiliateId = affiliateId,
            CustomerId = request.CustomerId,
            CreatedBy = actorId,
        };
        Apply(contract, request, agreed);
        contract.ContractNumber = await ResolveNumberAsync(affiliateId, request.ContractNumber, null);
        _db.Contracts.Add(contract);
        if (contract.IsActive) SyncCustomer(customer, contract);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dos altas a la vez tomaron el mismo folio automático: el índice único dejó pasar solo una.
            throw new InvalidOperationException("Ese folio de contrato ya existe; intenta de nuevo.");
        }

        contract.Customer = customer;
        return ToDto(contract);
    }

    public async Task<ContractDto?> UpdateAsync(Guid affiliateId, Guid contractId, UpsertContractRequest request, string? actorId)
    {
        var agreed = Validate(request);
        var contract = await _db.Contracts.Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == contractId && c.AffiliateId == affiliateId);
        if (contract is null) return null;
        if (request.CustomerId != contract.CustomerId)
            throw new ArgumentException("No se puede cambiar el cliente de un contrato; crea uno nuevo.");
        if (request.IsActive) await EnsureNoOtherActiveAsync(affiliateId, contract.CustomerId, contract.Id);

        Apply(contract, request, agreed);
        if (!string.IsNullOrWhiteSpace(request.ContractNumber))
            contract.ContractNumber = await ResolveNumberAsync(affiliateId, request.ContractNumber, contract.Id);
        contract.UpdatedAt = DateTime.UtcNow;
        contract.UpdatedBy = actorId;
        if (contract.IsActive && contract.Customer is not null) SyncCustomer(contract.Customer, contract);
        await _db.SaveChangesAsync();
        return ToDto(contract);
    }

    public async Task<ContractDto?> DeactivateAsync(Guid affiliateId, Guid contractId, string? actorId)
    {
        var contract = await _db.Contracts.Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == contractId && c.AffiliateId == affiliateId);
        if (contract is null) return null;
        if (contract.IsActive)
        {
            contract.IsActive = false;
            contract.EndDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
            contract.UpdatedAt = DateTime.UtcNow;
            contract.UpdatedBy = actorId;
            await _db.SaveChangesAsync();
        }
        return ToDto(contract);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Valida el request y devuelve el JSON de precios acordados listo para guardar (o null).</summary>
    private static string? Validate(UpsertContractRequest r)
    {
        if (!DeliveryFrequencies.Contains(r.DeliveryFrequency))
            throw new ArgumentException("Frecuencia de entrega inválida (daily, weekly u on_demand).");
        if (!BillingCycles.Contains(r.BillingCycle))
            throw new ArgumentException("Ciclo de cobro inválido (weekly, biweekly o monthly).");
        if (r.CreditDays is < 0 or > 365)
            throw new ArgumentException("Los días de crédito deben estar entre 0 y 365.");
        if (r.EndDate.HasValue && r.EndDate.Value < r.StartDate)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.");
        if (r.RacksLoaned is < 0 or > 10_000 || r.BottlesLoaned is < 0 or > 10_000)
            throw new ArgumentException("Las cantidades de anaqueles y botellones deben estar entre 0 y 10000.");

        if (r.AgreedPrices is { } ap && ap.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
        {
            if (ap.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Los precios acordados deben ser una lista [{itemId, price}].");
            foreach (var row in ap.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object
                    || !row.TryGetProperty("itemId", out var item) || !Guid.TryParse(item.GetString(), out _)
                    || !row.TryGetProperty("price", out var price) || price.ValueKind != JsonValueKind.Number
                    || !price.TryGetDecimal(out var p) || p < 0)
                    throw new ArgumentException("Cada precio acordado necesita itemId (GUID) y price (número ≥ 0).");
            }
            return ap.GetRawText();
        }
        return null;
    }

    private async Task EnsureNoOtherActiveAsync(Guid affiliateId, Guid customerId, Guid? exceptContractId)
    {
        var exists = await _db.Contracts.AnyAsync(c =>
            c.AffiliateId == affiliateId && c.CustomerId == customerId && c.IsActive && c.Id != exceptContractId);
        if (exists)
            throw new InvalidOperationException("Este cliente ya tiene un contrato activo. Desactívalo antes de crear o activar otro.");
    }

    private static void Apply(Contract c, UpsertContractRequest r, string? agreedPricesJson)
    {
        c.DeliveryFrequency = r.DeliveryFrequency;
        c.BillingCycle = r.BillingCycle;
        c.CreditDays = r.CreditDays;
        c.AgreedPrices = agreedPricesJson;
        c.StartDate = r.StartDate;
        c.EndDate = r.EndDate;
        c.IsActive = r.IsActive;
        c.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        c.RacksLoaned = r.RacksLoaned;
        c.BottlesLoaned = r.BottlesLoaned;
    }

    /// <summary>La hoja de reparto y los cobros leen estos campos del cliente: se mantienen iguales al contrato activo.</summary>
    private static void SyncCustomer(Customer customer, Contract c)
    {
        customer.DeliveryFrequency = c.DeliveryFrequency;
        customer.BillingCycle = c.BillingCycle;
        customer.CreditDays = c.CreditDays;
        customer.UpdatedAt = DateTime.UtcNow;
    }

    // ── Folio, plantilla y documento ─────────────────────────────────────────

    private static readonly (string Key, string Es, string En)[] TemplateTags =
    {
        ("folio", "Folio del contrato", "Contract number"),
        ("fecha", "Fecha de inicio (dd/mm/aaaa)", "Start date (dd/mm/yyyy)"),
        ("dia", "Día de inicio", "Start day"),
        ("mes", "Mes de inicio", "Start month"),
        ("anio", "Año de inicio", "Start year"),
        ("cliente", "Nombre del cliente", "Customer name"),
        ("cedula", "Cédula o RNC", "Tax ID"),
        ("telefono", "Teléfono del cliente", "Customer phone"),
        ("email", "Correo del cliente", "Customer email"),
        ("direccion", "Dirección del cliente", "Customer address"),
        ("anaqueles", "Anaqueles prestados", "Racks loaned"),
        ("botellones", "Botellones prestados", "Bottles loaned"),
        ("frecuencia_entrega", "Frecuencia de entrega", "Delivery frequency"),
        ("ciclo_cobro", "Ciclo de cobro", "Billing cycle"),
        ("dias_credito", "Días de crédito", "Credit days"),
        ("fecha_fin", "Fecha de fin", "End date"),
        ("negocio", "Nombre del negocio", "Business name"),
        ("negocio_telefono", "Teléfono del negocio", "Business phone"),
        ("negocio_direccion", "Dirección del negocio", "Business address"),
    };

    private const string DefaultTitle = "CONTRATO";

    // Basada en el contrato en papel de Nueva Era; cada negocio la edita a su gusto.
    private const string DefaultBody =
        "Cliente: {{cliente}}\n" +
        "Cédula: {{cedula}}\n" +
        "Teléfono: {{telefono}}\n" +
        "Dirección: {{direccion}}\n" +
        "\n" +
        "Productos entregados en préstamo:\n" +
        "Anaqueles: {{anaqueles}}\n" +
        "Botellones: {{botellones}}\n" +
        "\n" +
        "NOTA: Hemos recibido lo anteriormente mencionado y nos comprometemos a cuidar y a pagar en caso de daños o pérdida del mismo.";

    private static readonly Regex TagPattern = new(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

    public async Task<ContractTemplateDto> GetTemplateAsync(Guid affiliateId)
    {
        var saved = await _db.ContractTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.AffiliateId == affiliateId);
        return saved is null
            ? new ContractTemplateDto(DefaultTitle, DefaultBody, false, TagList())
            : new ContractTemplateDto(saved.Title, saved.Body, true, TagList());
    }

    public async Task<ContractTemplateDto> SaveTemplateAsync(Guid affiliateId, SaveContractTemplateRequest request, string? actorId)
    {
        var title = (request.Title ?? string.Empty).Trim();
        var body = (request.Body ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (title.Length == 0 || title.Length > 200) throw new ArgumentException("El título es obligatorio (máx. 200 caracteres).");
        if (body.Length == 0) throw new ArgumentException("El texto del contrato no puede estar vacío.");
        if (body.Length > 20_000) throw new ArgumentException("El texto del contrato es demasiado largo (máx. 20000 caracteres).");

        var known = TemplateTags.Select(t => t.Key).ToHashSet();
        var unknown = TagPattern.Matches(title + "\n" + body).Select(m => m.Groups[1].Value)
            .Where(k => !known.Contains(k)).Distinct().ToList();
        if (unknown.Count > 0)
            throw new ArgumentException("Etiquetas desconocidas: " + string.Join(", ", unknown.Select(k => "{{" + k + "}}")) + ".");

        var template = await _db.ContractTemplates.FirstOrDefaultAsync(t => t.AffiliateId == affiliateId);
        if (template is null)
        {
            template = new ContractTemplate { AffiliateId = affiliateId, CreatedBy = actorId };
            _db.ContractTemplates.Add(template);
        }
        else
        {
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = actorId;
        }
        template.Title = title;
        template.Body = body;
        await _db.SaveChangesAsync();
        return new ContractTemplateDto(template.Title, template.Body, true, TagList());
    }

    public async Task<ContractTemplateDto> ResetTemplateAsync(Guid affiliateId)
    {
        var template = await _db.ContractTemplates.FirstOrDefaultAsync(t => t.AffiliateId == affiliateId);
        if (template is not null)
        {
            _db.ContractTemplates.Remove(template);
            await _db.SaveChangesAsync();
        }
        return new ContractTemplateDto(DefaultTitle, DefaultBody, false, TagList());
    }

    public async Task<ContractDocumentDto?> RenderDocumentAsync(Guid affiliateId, Guid contractId)
    {
        var c = await _db.Contracts.AsNoTracking().Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == contractId && x.AffiliateId == affiliateId);
        if (c is null) return null;
        var affiliate = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == affiliateId);
        var template = await GetTemplateAsync(affiliateId);
        var cu = c.Customer;

        string D(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var values = new Dictionary<string, string>
        {
            ["folio"] = c.ContractNumber ?? "",
            ["fecha"] = D(c.StartDate),
            ["dia"] = c.StartDate.Day.ToString(CultureInfo.InvariantCulture),
            ["mes"] = c.StartDate.Month.ToString(CultureInfo.InvariantCulture),
            ["anio"] = c.StartDate.Year.ToString(CultureInfo.InvariantCulture),
            ["cliente"] = cu?.Name ?? "",
            ["cedula"] = cu?.Rnc ?? "",
            ["telefono"] = cu?.Phone ?? "",
            ["email"] = cu?.Email ?? "",
            ["direccion"] = cu?.Address ?? "",
            ["anaqueles"] = c.RacksLoaned.ToString(CultureInfo.InvariantCulture),
            ["botellones"] = c.BottlesLoaned.ToString(CultureInfo.InvariantCulture),
            ["frecuencia_entrega"] = c.DeliveryFrequency switch { "daily" => "Diaria", "weekly" => "Semanal", "on_demand" => "Por pedido", var x => x },
            ["ciclo_cobro"] = c.BillingCycle switch { "weekly" => "Semanal", "biweekly" => "Quincenal", "monthly" => "Mensual", var x => x },
            ["dias_credito"] = c.CreditDays.ToString(CultureInfo.InvariantCulture),
            ["fecha_fin"] = c.EndDate.HasValue ? D(c.EndDate.Value) : "",
            ["negocio"] = affiliate?.Name ?? "",
            ["negocio_telefono"] = affiliate?.WhatsApp ?? "",
            ["negocio_direccion"] = affiliate?.Address ?? "",
        };
        string Fill(string text) => TagPattern.Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

        return new ContractDocumentDto(
            Fill(template.Title), Fill(template.Body), c.ContractNumber, D(c.StartDate),
            affiliate?.Name ?? "", affiliate?.WhatsApp, affiliate?.Address, affiliate?.LogoUrl, affiliate?.PrimaryColor,
            cu?.Name ?? "");
    }

    private static IReadOnlyList<TemplateTagDto> TagList() => TemplateTags.Select(t => new TemplateTagDto(t.Key, t.Es, t.En)).ToList();

    /// <summary>Folio pedido (validando que no exista) o, si no se pidió, el siguiente al más alto: conserva el ancho del cuaderno (0133 → 0134).</summary>
    private async Task<string> ResolveNumberAsync(Guid affiliateId, string? requested, Guid? exceptContractId)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var number = requested.Trim();
            if (number.Length > 20) throw new ArgumentException("El folio no puede pasar de 20 caracteres.");
            var taken = await _db.Contracts.AnyAsync(c =>
                c.AffiliateId == affiliateId && c.ContractNumber == number && c.Id != exceptContractId);
            if (taken) throw new InvalidOperationException($"El folio {number} ya está en otro contrato.");
            return number;
        }

        var existing = await _db.Contracts.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && c.ContractNumber != null)
            .Select(c => c.ContractNumber!).ToListAsync();
        long max = 0;
        var width = 4;
        foreach (var n in existing)
        {
            if (n.Length > 0 && n.All(char.IsDigit) && long.TryParse(n, out var v))
            {
                if (v > max) max = v;
                width = Math.Max(width, n.Length);
            }
        }
        return (max + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
    }

    private static ContractDto ToDto(Contract c) => new(
        c.Id, c.CustomerId, c.Customer?.Name, c.DeliveryFrequency, c.BillingCycle, c.CreditDays,
        string.IsNullOrWhiteSpace(c.AgreedPrices) ? null : JsonSerializer.Deserialize<JsonElement>(c.AgreedPrices),
        c.StartDate, c.EndDate, c.IsActive, c.Notes, c.CreatedAt,
        c.ContractNumber, c.RacksLoaned, c.BottlesLoaned);
}
