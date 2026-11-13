using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Fiscal;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services.Fiscal;

public interface IFiscalService
{
    Task<FiscalOverviewDto> GetOverviewAsync(Guid affiliateId, string publicBaseUrl);
    Task<FiscalProfileDto> UpsertProfileAsync(Guid affiliateId, FiscalProfileRequest request, string? by);
    Task<FiscalProfileDto> RegisterCompanyAsync(Guid affiliateId, FiscalRegisterRequest request, string publicBaseUrl, string? by);
    Task<List<FiscalSequenceDto>> UpsertSequenceAsync(Guid affiliateId, FiscalSequenceRequest request, string? by);
    Task<List<ElectronicInvoiceDto>> ListInvoicesAsync(Guid affiliateId, string? status, int limit);
    Task<ElectronicInvoiceDto> GetInvoiceAsync(Guid affiliateId, Guid invoiceId);
    Task<ElectronicInvoiceDto> EmitFromOrderAsync(Guid affiliateId, EmitInvoiceRequest request);
    Task<ElectronicInvoiceDto> RetryAsync(Guid affiliateId, Guid invoiceId);
    Task<ElectronicInvoiceDto> RefreshAsync(Guid affiliateId, Guid invoiceId);
    /// <summary>Notificación del proveedor. Devuelve false si la llave no corresponde a este negocio.</summary>
    Task<bool> HandleWebhookAsync(Guid affiliateId, string payload, string? apiKey);
    /// <summary>Consulta al proveedor los comprobantes que siguen en proceso. Lo llama el worker.</summary>
    Task<int> SyncPendingAsync(CancellationToken ct);
}

public class FiscalService : IFiscalService
{
    private static readonly int[] DocTypes = { 31, 32 };
    private const decimal LargeConsumerLimit = 250_000m;

    private readonly AppDbContext _db;
    private readonly IFiscalProvider _provider;
    private readonly ILogger<FiscalService> _logger;
    private readonly string _secret;

    public FiscalService(AppDbContext db, IFiscalProvider provider, IConfiguration config, ILogger<FiscalService> logger)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
        _secret = config["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
    }

    // ── Perfil y alta ───────────────────────────────────────────────────────────────────────────────────────────

    public async Task<FiscalOverviewDto> GetOverviewAsync(Guid affiliateId, string publicBaseUrl)
    {
        var profile = await _db.FiscalProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == affiliateId);
        var sequences = await _db.FiscalSequences.AsNoTracking().Where(s => s.AffiliateId == affiliateId).OrderBy(s => s.DocType).ToListAsync();
        var counts = await _db.ElectronicInvoices.AsNoTracking().Where(i => i.AffiliateId == affiliateId)
            .GroupBy(i => i.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        int Count(string s) => counts.FirstOrDefault(c => c.Key == s)?.N ?? 0;

        return new FiscalOverviewDto(
            _provider.IsConfigured(profile?.Environment ?? "sandbox"),
            profile is null ? null : ToDto(profile),
            sequences.Select(x => ToDto(x)).ToList(),
            WebhookUrl(publicBaseUrl, affiliateId),
            Count("processing") + Count("queued"), Count("rejected"), Count("failed"));
    }

    public async Task<FiscalProfileDto> UpsertProfileAsync(Guid affiliateId, FiscalProfileRequest r, string? by)
    {
        var profile = await _db.FiscalProfiles.FirstOrDefaultAsync(p => p.AffiliateId == affiliateId);
        var isNew = profile is null;
        profile ??= new FiscalProfile { AffiliateId = affiliateId, CreatedBy = by };

        var rnc = Digits(r.Rnc);
        if (rnc.Length is not (9 or 11)) throw new ArgumentException("El RNC debe tener 9 dígitos (o 11 si es cédula).");
        if (string.IsNullOrWhiteSpace(r.LegalName)) throw new ArgumentException("Falta la razón social tal como figura en la DGII.");
        if (string.IsNullOrWhiteSpace(r.Address)) throw new ArgumentException("Falta la dirección fiscal.");
        if (r.Address.Trim().Length > 100) throw new ArgumentException("La dirección fiscal admite hasta 100 caracteres.");
        if (r.Email is { Length: > 80 }) throw new ArgumentException("El correo admite hasta 80 caracteres.");

        var env = (r.Environment ?? profile.Environment).Trim().ToLowerInvariant();
        if (env is not ("sandbox" or "production")) throw new ArgumentException("El ambiente debe ser sandbox o production.");
        var indicator = r.DefaultBillingIndicator ?? profile.DefaultBillingIndicator;
        if (indicator is < 1 or > 4) throw new ArgumentException("El indicador de ITBIS debe ser 1 (18%), 2 (16%), 3 (0%) o 4 (exento).");
        var goodService = r.DefaultGoodService ?? profile.DefaultGoodService;
        if (goodService is not (1 or 2)) throw new ArgumentException("Bien = 1, servicio = 2.");

        if (!isNew && profile.Status != "draft" && (rnc != profile.Rnc || env != profile.Environment))
            throw new ArgumentException("Con la empresa ya registrada no se puede cambiar el RNC ni el ambiente.");

        profile.Rnc = rnc;
        profile.LegalName = r.LegalName.Trim();
        profile.TradeName = Clean(r.TradeName);
        profile.Address = r.Address.Trim();
        profile.Province = Clean(r.Province);
        profile.Municipality = Clean(r.Municipality);
        profile.Email = Clean(r.Email);
        profile.Environment = env;
        profile.DefaultBillingIndicator = indicator;
        profile.DefaultGoodService = goodService;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = by;

        if (isNew) _db.FiscalProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return ToDto(profile);
    }

    public async Task<FiscalProfileDto> RegisterCompanyAsync(Guid affiliateId, FiscalRegisterRequest r, string publicBaseUrl, string? by)
    {
        var profile = await _db.FiscalProfiles.FirstOrDefaultAsync(p => p.AffiliateId == affiliateId)
            ?? throw new ArgumentException("Primero guarda los datos fiscales del negocio.");
        if (!_provider.IsConfigured(profile.Environment))
            throw new ArgumentException("La facturación electrónica todavía no está habilitada en la plataforma para este ambiente.");
        if (string.IsNullOrWhiteSpace(r.CertificateBase64) || string.IsNullOrWhiteSpace(r.CertificateFileName))
            throw new ArgumentException("Sube el certificado digital (.p12).");
        if (string.IsNullOrEmpty(r.CertificatePassword))
            throw new ArgumentException("Falta la contraseña del certificado.");
        try { Convert.FromBase64String(r.CertificateBase64); }
        catch (FormatException) { throw new ArgumentException("El archivo del certificado no es válido."); }

        var result = await _provider.RegisterCompanyAsync(profile.Environment, new FiscalCompanyRegistration(
            profile.LegalName, profile.TradeName, profile.Rnc, profile.Address, profile.Province, profile.Municipality, profile.Email,
            r.CertificateFileName!, r.CertificateBase64!, r.CertificatePassword!,
            WebhookUrl(publicBaseUrl, affiliateId), WebhookSecret(affiliateId)));

        profile.ProviderCompanyId = result.CompanyId;
        profile.ProviderRegisteredAt = DateTime.UtcNow;
        profile.ReceptionUrl = result.ReceptionUrl;
        profile.ApprovalUrl = result.ApprovalUrl;
        profile.AuthenticationUrl = result.AuthenticationUrl;
        profile.CertificateIssuer = result.CertificateIssuer;
        profile.CertificateStartsAt = result.CertificateStartsAt;
        profile.CertificateExpiresAt = result.CertificateExpiresAt;
        if (profile.Status == "draft") profile.Status = "registered";
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = by;
        await _db.SaveChangesAsync();
        return ToDto(profile);
    }

    public async Task<List<FiscalSequenceDto>> UpsertSequenceAsync(Guid affiliateId, FiscalSequenceRequest r, string? by)
    {
        if (!DocTypes.Contains(r.DocType)) throw new ArgumentException("Tipo de comprobante no habilitado: solo E31 y E32.");
        if (r.FromNumber < 1 || r.ToNumber < r.FromNumber || r.ToNumber > 9_999_999_999)
            throw new ArgumentException("El rango de secuencia no es válido.");

        var seq = await _db.FiscalSequences.FirstOrDefaultAsync(s => s.AffiliateId == affiliateId && s.DocType == r.DocType);
        if (seq is null)
        {
            seq = new FiscalSequence { AffiliateId = affiliateId, DocType = r.DocType, CreatedBy = by };
            _db.FiscalSequences.Add(seq);
        }
        else if (r.FromNumber < seq.NextNumber && r.ToNumber >= seq.NextNumber - 1)
        {
            // Un rango que cubre números ya emitidos los reutilizaría.
            var used = await _db.ElectronicInvoices.AnyAsync(i => i.AffiliateId == affiliateId && i.DocType == r.DocType);
            if (used) throw new ArgumentException("El nuevo rango incluye números que ya se usaron.");
        }

        seq.NextNumber = r.FromNumber;
        seq.LastNumber = r.ToNumber;
        seq.ExpiresOn = r.ExpiresOn;
        seq.IsActive = true;
        seq.UpdatedAt = DateTime.UtcNow;
        seq.UpdatedBy = by;

        var profile = await _db.FiscalProfiles.FirstOrDefaultAsync(p => p.AffiliateId == affiliateId);
        if (profile is { Status: "registered" }) profile.Status = "active";

        await _db.SaveChangesAsync();
        var all = await _db.FiscalSequences.AsNoTracking().Where(s => s.AffiliateId == affiliateId).OrderBy(s => s.DocType).ToListAsync();
        return all.Select(x => ToDto(x)).ToList();
    }

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<List<ElectronicInvoiceDto>> ListInvoicesAsync(Guid affiliateId, string? status, int limit)
    {
        var q = _db.ElectronicInvoices.AsNoTracking().Where(i => i.AffiliateId == affiliateId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(i => i.Status == status);
        var rows = await q.OrderByDescending(i => i.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToListAsync();
        return rows.Select(i => ToDto(i, null, null)).ToList();
    }

    public async Task<ElectronicInvoiceDto> GetInvoiceAsync(Guid affiliateId, Guid invoiceId)
    {
        var invoice = await _db.ElectronicInvoices.AsNoTracking().Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException("Comprobante no encontrado.");
        var events = await _db.ElectronicInvoiceEvents.AsNoTracking().Where(e => e.InvoiceId == invoiceId).OrderBy(e => e.CreatedAt).ToListAsync();
        return ToDto(invoice, invoice.Items, events);
    }

    // ── Emisión ─────────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<ElectronicInvoiceDto> EmitFromOrderAsync(Guid affiliateId, EmitInvoiceRequest request)
    {
        if (!DocTypes.Contains(request.DocType)) throw new ArgumentException("Tipo de comprobante no habilitado: solo E31 (crédito fiscal) y E32 (consumo).");

        var profile = await _db.FiscalProfiles.FirstOrDefaultAsync(p => p.AffiliateId == affiliateId);
        if (profile is null || profile.Status == "draft" || string.IsNullOrEmpty(profile.ProviderCompanyId))
            throw new ArgumentException("Completa la configuración fiscal (datos, certificado y secuencias) antes de emitir.");
        if (!_provider.IsConfigured(profile.Environment))
            throw new ArgumentException("La facturación electrónica todavía no está habilitada en la plataforma para este ambiente.");

        var existing = await FindActiveInvoiceAsync(affiliateId, request.OrderId, request.DocType);
        if (existing is not null) return await GetInvoiceAsync(affiliateId, existing.Id);

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrderId && o.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException("Pedido no encontrado.");
        if (order.Status is OrderStatus.Pending or OrderStatus.Canceled)
            throw new ArgumentException("Solo se factura un pedido cobrado y no cancelado.");
        if (!string.Equals(order.Currency, "DOP", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Solo se emiten comprobantes de pedidos en pesos dominicanos (DOP).");
        if (order.ShippingCost > 0)
            throw new ArgumentException("Los pedidos con envío todavía no se pueden facturar electrónicamente.");

        // Comprador: lo que se indique, y si no, la ficha del cliente enlazada al pedido.
        string? buyerRnc = Digits(request.BuyerRnc), buyerName = Clean(request.BuyerName);
        if (order.CustomerId is { } customerId && (string.IsNullOrEmpty(buyerRnc) || buyerName is null))
        {
            var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && c.AffiliateId == affiliateId);
            if (customer is not null)
            {
                if (string.IsNullOrEmpty(buyerRnc)) buyerRnc = Digits(customer.Rnc);
                buyerName ??= Clean(customer.Name);
            }
        }
        buyerName ??= Clean(order.CustomerName);
        if (string.IsNullOrEmpty(buyerRnc)) buyerRnc = null;
        if (buyerRnc is not null && buyerRnc.Length is not (9 or 11)) throw new ArgumentException("El RNC o cédula del comprador no es válido.");
        if (request.DocType == 31 && (buyerRnc is null || buyerName is null))
            throw new ArgumentException("El comprobante de crédito fiscal (E31) exige RNC y nombre del comprador.");

        var (lines, totals) = BuildLines(order, profile);
        if (request.DocType == 32 && totals.Total >= LargeConsumerLimit && buyerRnc is null)
            throw new ArgumentException("Un comprobante de consumo desde RD$ 250,000 exige el RNC o cédula del comprador.");

        var stamp = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4)); // hora de Santo Domingo, sin horario de verano
        var invoice = new ElectronicInvoice
        {
            AffiliateId = affiliateId, OrderId = order.Id, DocType = request.DocType, Status = "queued",
            BuyerRnc = buyerRnc, BuyerName = buyerName, Currency = "DOP", StampDate = stamp,
            Subtotal = totals.TaxedTotal + totals.Exempt, Tax = totals.ItbisTotal, NonBillable = totals.NonBillable, Total = totals.Total,
            PaymentMethod = PaymentMethodFor(order),
            Items = lines.Select(l => new ElectronicInvoiceItem
            {
                LineNumber = l.LineNumber, Name = l.Name, Quantity = l.Quantity, UnitPrice = l.UnitPrice, Amount = l.Amount,
                BillingIndicator = l.BillingIndicator, GoodService = l.GoodService,
            }).ToList(),
        };

        // Número y comprobante en una sola transacción: si algo falla, el número vuelve a la secuencia.
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            var number = await ClaimNumberAsync(affiliateId, request.DocType)
                ?? throw new ArgumentException($"No hay secuencia E{request.DocType} disponible: configura el rango autorizado por la DGII (o se agotó o venció).");
            invoice.Encf = $"E{request.DocType}{number.ToString("D10", CultureInfo.InvariantCulture)}";
            _db.ElectronicInvoices.Add(invoice);
            _db.ElectronicInvoiceEvents.Add(new ElectronicInvoiceEvent { InvoiceId = invoice.Id, AffiliateId = affiliateId, Source = "api", Status = "queued", Detail = invoice.Encf });
            try
            {
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // Otra petición facturó este pedido a la vez: el índice único lo impide y se devuelve el ganador.
                await tx.RollbackAsync();
                _db.ChangeTracker.Clear();
                var winner = await FindActiveInvoiceAsync(affiliateId, request.OrderId, request.DocType);
                if (winner is not null) return await GetInvoiceAsync(affiliateId, winner.Id);
                throw;
            }
        }

        await SendAsync(invoice, profile, lines, totals, order);
        return await GetInvoiceAsync(affiliateId, invoice.Id);
    }

    public async Task<ElectronicInvoiceDto> RetryAsync(Guid affiliateId, Guid invoiceId)
    {
        var invoice = await _db.ElectronicInvoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == invoiceId && i.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException("Comprobante no encontrado.");
        // Solo lo que no llegó a un veredicto: un comprobante rechazado no se reenvía, se emite uno nuevo desde el pedido.
        if (invoice.Status != "failed") throw new ArgumentException("Solo se puede reintentar un comprobante que falló al enviarse.");
        if (invoice.OrderId is null) throw new ArgumentException("El comprobante no tiene pedido de origen.");

        var profile = await _db.FiscalProfiles.FirstOrDefaultAsync(p => p.AffiliateId == affiliateId)
            ?? throw new ArgumentException("Falta la configuración fiscal.");
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == invoice.OrderId && o.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException("Pedido no encontrado.");

        var lines = invoice.Items.OrderBy(i => i.LineNumber)
            .Select(i => new FiscalLine(i.LineNumber, i.Name, i.Quantity, i.UnitPrice, i.Amount, i.BillingIndicator, i.GoodService)).ToList();
        var (_, totals) = BuildLines(order, profile);
        await SendAsync(invoice, profile, lines, totals, order);
        return await GetInvoiceAsync(affiliateId, invoice.Id);
    }

    public async Task<ElectronicInvoiceDto> RefreshAsync(Guid affiliateId, Guid invoiceId)
    {
        var invoice = await _db.ElectronicInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.AffiliateId == affiliateId)
            ?? throw new KeyNotFoundException("Comprobante no encontrado.");
        if (string.IsNullOrEmpty(invoice.ProviderId)) throw new ArgumentException("El comprobante todavía no llegó al proveedor.");
        var profile = await _db.FiscalProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == affiliateId)
            ?? throw new ArgumentException("Falta la configuración fiscal.");
        await PullAsync(invoice, profile, "manual");
        return await GetInvoiceAsync(affiliateId, invoiceId);
    }

    // ── Webhook y worker ────────────────────────────────────────────────────────────────────────────────────────

    public async Task<bool> HandleWebhookAsync(Guid affiliateId, string payload, string? apiKey)
    {
        // Alanube prueba la URL al registrar la empresa con {"message":"Test message"}: se responde 200 sin tocar nada.
        if (IsProviderTestMessage(payload)) return true;
        if (!SecretsEqual(apiKey, WebhookSecret(affiliateId))) return false;

        var evt = new FiscalWebhookEvent { AffiliateId = affiliateId, Payload = payload.Length > 20000 ? payload[..20000] : payload };
        _db.FiscalWebhookEvents.Add(evt);
        try
        {
            string? providerId = null;
            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var doc = JsonDocument.Parse(payload);
                providerId = AlanubeProvider.FindString(doc.RootElement, "documentId") ?? AlanubeProvider.FindString(doc.RootElement, "id");
            }

            // El webhook es un aviso: el estado real se vuelve a pedir al proveedor.
            var invoice = providerId is null ? null
                : await _db.ElectronicInvoices.FirstOrDefaultAsync(i => i.AffiliateId == affiliateId && i.ProviderId == providerId);
            var profile = invoice is null ? null : await _db.FiscalProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AffiliateId == affiliateId);
            if (invoice is not null && profile is not null) await PullAsync(invoice, profile, "webhook");
            evt.Handled = invoice is not null;
        }
        catch (Exception ex) when (ex is JsonException or FiscalProviderException)
        {
            evt.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> SyncPendingAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        var pending = await _db.ElectronicInvoices
            .Where(i => i.Status == "processing" && i.ProviderId != null && (i.LastSyncAt == null || i.LastSyncAt < cutoff))
            .OrderBy(i => i.LastSyncAt).Take(20).ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var ids = pending.Select(p => p.AffiliateId).Distinct().ToList();
        var profiles = await _db.FiscalProfiles.AsNoTracking().Where(p => ids.Contains(p.AffiliateId)).ToDictionaryAsync(p => p.AffiliateId, ct);
        foreach (var invoice in pending)
        {
            if (!profiles.TryGetValue(invoice.AffiliateId, out var profile)) continue;
            try { await PullAsync(invoice, profile, "poll"); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Fiscal] no se pudo consultar {Encf}", invoice.Encf);
                invoice.LastSyncAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }
        return pending.Count;
    }

    // ── Internos ────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task SendAsync(ElectronicInvoice invoice, FiscalProfile profile, IReadOnlyList<FiscalLine> lines, FiscalTotals totals, Order order)
    {
        invoice.Attempts++;
        var document = new FiscalDocument(
            invoice.DocType, invoice.Encf, invoice.StampDate, PaymentType: 1, invoice.PaymentMethod,
            new FiscalIssuer(profile.Rnc, profile.LegalName, profile.TradeName, profile.Address, profile.Province, profile.Municipality, profile.Email),
            invoice.BuyerRnc is null && invoice.BuyerName is null ? null : new FiscalBuyer(invoice.BuyerRnc, invoice.BuyerName),
            lines, totals);
        try
        {
            var result = await _provider.EmitAsync(new FiscalAccount(profile.Environment, profile.ProviderCompanyId), document);
            Apply(invoice, result, "api");
            // Aceptado o rechazado ya en la primera respuesta es posible; si no, queda en proceso para el worker.
            if (invoice.Status == "queued") invoice.Status = "processing";
        }
        catch (FiscalProviderException ex)
        {
            invoice.Status = "failed";
            invoice.ErrorCode = ex.Code;
            invoice.ErrorMessage = ex.Message;
            invoice.UpdatedAt = DateTime.UtcNow;
            _db.ElectronicInvoiceEvents.Add(new ElectronicInvoiceEvent
            {
                InvoiceId = invoice.Id, AffiliateId = invoice.AffiliateId, Source = "api", Status = "failed",
                Detail = $"{ex.Code}: {ex.Message}",
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task PullAsync(ElectronicInvoice invoice, FiscalProfile profile, string source)
    {
        var result = await _provider.GetDocumentAsync(new FiscalAccount(profile.Environment, profile.ProviderCompanyId), invoice.DocType, invoice.ProviderId!);
        Apply(invoice, result, source);
        await _db.SaveChangesAsync();
    }

    private void Apply(ElectronicInvoice invoice, FiscalDocumentResult r, string source)
    {
        var changed = invoice.Status != r.Status || invoice.LegalStatus != r.LegalStatus;
        invoice.ProviderId ??= r.ProviderId;
        invoice.Status = r.Status;
        invoice.LegalStatus = r.LegalStatus ?? invoice.LegalStatus;
        invoice.XmlUrl = r.XmlUrl ?? invoice.XmlUrl;
        invoice.PdfUrl = r.PdfUrl ?? invoice.PdfUrl;
        invoice.SecurityCode = r.SecurityCode ?? invoice.SecurityCode;
        invoice.StampUrl = r.StampUrl ?? invoice.StampUrl;
        invoice.ErrorCode = r.Status is "rejected" or "failed" ? r.ErrorCode : null;
        invoice.ErrorMessage = r.Status is "rejected" or "failed" ? r.ErrorMessage : null;
        invoice.LastSyncAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        if (r.Status is "accepted" or "rejected" or "failed") invoice.FinishedAt ??= DateTime.UtcNow;
        if (changed)
            _db.ElectronicInvoiceEvents.Add(new ElectronicInvoiceEvent
            {
                InvoiceId = invoice.Id, AffiliateId = invoice.AffiliateId, Source = source, Status = r.Status, LegalStatus = r.LegalStatus, Detail = r.ErrorMessage,
            });
    }

    private Task<ElectronicInvoice?> FindActiveInvoiceAsync(Guid affiliateId, Guid orderId, int docType) =>
        _db.ElectronicInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.AffiliateId == affiliateId && i.OrderId == orderId && i.DocType == docType && i.Status != "rejected");

    /// <summary>Toma el siguiente número del rango con una sola sentencia: dos ventas simultáneas nunca comparten número.</summary>
    private async Task<long?> ClaimNumberAsync(Guid affiliateId, int docType)
    {
        var connection = _db.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = @"
UPDATE ""FiscalSequences"" SET ""NextNumber"" = ""NextNumber"" + 1, ""UpdatedAt"" = now()
WHERE ""AffiliateId"" = @a AND ""DocType"" = @t AND ""IsActive"" AND ""NextNumber"" <= ""LastNumber""
  AND (""ExpiresOn"" IS NULL OR ""ExpiresOn"" >= (now() AT TIME ZONE 'America/Santo_Domingo')::date)
RETURNING ""NextNumber"" - 1;";
        AddParameter(command, "@a", affiliateId);
        AddParameter(command, "@t", docType);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        command.Parameters.Add(p);
    }

    /// <summary>Del pedido a las líneas del e-CF. El ITBIS sale de la tasa con que el API calculó el pedido.</summary>
    private static (List<FiscalLine> Lines, FiscalTotals Totals) BuildLines(Order order, FiscalProfile profile)
    {
        var (indicator, rate) = order.TaxRate switch
        {
            null => (profile.DefaultBillingIndicator, profile.DefaultBillingIndicator switch { 1 => 18m, 2 => 16m, _ => 0m }),
            18m => (1, 18m),
            16m => (2, 16m),
            0m => (3, 0m),
            _ => throw new ArgumentException($"La tasa de impuesto del pedido ({order.TaxRate}%) no es una tasa de ITBIS válida (18%, 16% o 0%)."),
        };

        var items = JsonArrayField.Parse<OrderItemDto>(order.ItemsJson);
        if (items.Count == 0) throw new ArgumentException("El pedido no tiene artículos.");

        var lines = new List<FiscalLine>();
        var n = 1;
        foreach (var item in items)
        {
            var amount = Math.Round(item.Price * item.Qty, 2, MidpointRounding.AwayFromZero);
            lines.Add(new FiscalLine(n++, item.Name, item.Qty, item.Price, amount, indicator, profile.DefaultGoodService));
        }

        var billed = lines.Sum(l => l.Amount);
        if (Math.Abs(billed - order.Subtotal) > 0.05m)
            throw new ArgumentException("Las líneas del pedido no cuadran con su subtotal; revisa el pedido antes de facturar.");

        var itbis = Math.Round(billed * rate / 100m, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(itbis - order.Tax) > 0.05m)
            throw new ArgumentException("El ITBIS calculado no coincide con el impuesto cobrado en el pedido.");

        var totals = new FiscalTotals(
            indicator == 1 ? billed : 0, indicator == 2 ? billed : 0, indicator == 3 ? billed : 0, indicator == 4 ? billed : 0,
            indicator == 1 ? itbis : 0, indicator == 2 ? itbis : 0, indicator == 3 ? itbis : 0, order.Tip);
        return (lines, totals);
    }

    private static int PaymentMethodFor(Order order)
    {
        var method = (order.CollectedMethod ?? order.PaymentMethod ?? string.Empty).ToLowerInvariant();
        if (method == "cash") return 1;
        if (method == "card" || !string.IsNullOrEmpty(order.StripePaymentIntentId)) return 3;
        return 8;
    }

    private static bool IsProviderTestMessage(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("message", out var m)
                && m.ValueKind == JsonValueKind.String && m.GetString() == "Test message";
        }
        catch (JsonException) { return false; }
    }

    private string WebhookSecret(Guid affiliateId) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_secret), Encoding.UTF8.GetBytes("fiscal-webhook|" + affiliateId)));

    private static bool SecretsEqual(string? given, string expected) =>
        given is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));

    private static string WebhookUrl(string baseUrl, Guid affiliateId) => $"{baseUrl.TrimEnd('/')}/api/webhooks/alanube/{affiliateId}";

    private static string Digits(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static FiscalProfileDto ToDto(FiscalProfile p) => new(
        p.Rnc, p.LegalName, p.TradeName, p.Address, p.Province, p.Municipality, p.Email, p.Environment, p.Status,
        p.DefaultBillingIndicator, p.DefaultGoodService, p.ReceptionUrl, p.ApprovalUrl, p.AuthenticationUrl,
        p.CertificateIssuer, p.CertificateStartsAt, p.CertificateExpiresAt);

    private static FiscalSequenceDto ToDto(FiscalSequence s) => new(
        s.DocType, s.NextNumber, s.LastNumber, Math.Max(0, s.LastNumber - s.NextNumber + 1), s.ExpiresOn, s.IsActive);

    private static ElectronicInvoiceDto ToDto(ElectronicInvoice i, IEnumerable<ElectronicInvoiceItem>? items, IEnumerable<ElectronicInvoiceEvent>? events) => new(
        i.Id, i.OrderId, i.DocType, i.Encf, i.Status, i.LegalStatus, i.BuyerRnc, i.BuyerName, i.Subtotal, i.Tax, i.NonBillable, i.Total,
        i.StampDate, i.XmlUrl, i.PdfUrl, i.SecurityCode, i.StampUrl, i.ErrorCode, i.ErrorMessage, i.CreatedAt, i.FinishedAt,
        items?.OrderBy(x => x.LineNumber).Select(x => new ElectronicInvoiceItemDto(x.LineNumber, x.Name, x.Quantity, x.UnitPrice, x.Amount, x.BillingIndicator)).ToList(),
        events?.Select(e => new ElectronicInvoiceEventDto(e.CreatedAt, e.Source, e.Status, e.LegalStatus, e.Detail)).ToList());
}
