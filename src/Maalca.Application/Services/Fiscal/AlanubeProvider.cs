using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Maalca.Application.Common.Fiscal;

namespace Maalca.Application.Services.Fiscal;

/// <summary>
/// Alanube (https://developer.alanube.co): REST + JSON, Bearer. Un token de MaalCa por ambiente (variables
/// ALANUBE_SANDBOX_TOKEN y ALANUBE_PRODUCTION_TOKEN, como STRIPE_SECRET_KEY) y cada negocio es una empresa
/// "associated" cuyo id viaja en company.id al emitir. Lo que la documentación pública no precisa (formato exacto de
/// la respuesta de consulta, payload del webhook) se lee de forma tolerante y se confirma contra el sandbox.
/// </summary>
public class AlanubeProvider : IFiscalProvider
{
    public const string HttpClientName = "alanube";
    private const string SandboxUrl = "https://sandbox.alanube.co/dom/v1/";
    private const string ProductionUrl = "https://api.alanube.co/dom/v1/";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null, WriteIndented = false };

    private readonly IHttpClientFactory _http;

    public AlanubeProvider(IHttpClientFactory http) => _http = http;

    public bool IsConfigured(string environment) => !string.IsNullOrWhiteSpace(Token(environment));

    private static string? Token(string environment) =>
        Environment.GetEnvironmentVariable(environment == "production" ? "ALANUBE_PRODUCTION_TOKEN" : "ALANUBE_SANDBOX_TOKEN");

    private static string PathFor(int docType) => docType switch
    {
        31 => "fiscal-invoices",
        32 => "invoices",
        _ => throw new FiscalProviderException("UNSUPPORTED_DOC_TYPE", $"El tipo de comprobante E{docType} todavía no está habilitado."),
    };

    // ── Alta de empresa ─────────────────────────────────────────────────────────────────────────────────────────

    public async Task<FiscalCompanyResult> RegisterCompanyAsync(string environment, FiscalCompanyRegistration r, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>
        {
            ["name"] = r.Name,
            ["identification"] = r.Identification,
            ["address"] = r.Address,
            ["type"] = Environment.GetEnvironmentVariable("ALANUBE_COMPANY_TYPE") ?? "associated",
            ["certificate"] = new Dictionary<string, object>
            {
                ["name"] = Path.GetFileNameWithoutExtension(r.CertificateFileName),
                ["extension"] = Path.GetExtension(r.CertificateFileName).TrimStart('.').ToLowerInvariant(),
                ["content"] = r.CertificateBase64,
                ["password"] = r.CertificatePassword,
            },
            ["webhooks"] = new Dictionary<string, object>
            {
                ["documents"] = new Dictionary<string, object>
                {
                    ["emissionFinished"] = new Dictionary<string, object>
                    {
                        ["status"] = "active",
                        ["url"] = r.WebhookUrl,
                        ["headers"] = new Dictionary<string, object> { ["x-api-key"] = r.WebhookSecret },
                    },
                },
            },
        };
        Put(body, "tradeName", r.TradeName);
        Put(body, "province", r.Province);
        Put(body, "municipality", r.Municipality);
        Put(body, "email", r.Email);

        using var doc = await SendAsync(environment, HttpMethod.Post, "company", body, ct);
        var root = doc.RootElement;
        var id = FindString(root, "id") ?? throw new FiscalProviderException("BAD_RESPONSE", "Alanube no devolvió el id de la empresa.");
        return new FiscalCompanyResult(
            id,
            FindString(root, "reception"),
            FindString(root, "approval"),
            FindString(root, "authentication"),
            FindString(root, "issuerName"),
            FindDate(root, "startDate"),
            FindDate(root, "endDate"));
    }

    // ── Emisión y consulta ──────────────────────────────────────────────────────────────────────────────────────

    public async Task<FiscalDocumentResult> EmitAsync(FiscalAccount account, FiscalDocument d, CancellationToken ct = default)
    {
        var idDoc = new Dictionary<string, object>
        {
            ["encf"] = d.Encf,
            ["paymentType"] = d.PaymentType,
            ["incomeType"] = 1,
            ["taxAmountIndicator"] = 0,
            ["paymentFormsTable"] = new[]
            {
                new Dictionary<string, object> { ["paymentMethod"] = d.PaymentMethod, ["paymentAmount"] = d.Totals.Total + d.Totals.NonBillable },
            },
        };

        var sender = new Dictionary<string, object>
        {
            ["rnc"] = d.Issuer.Rnc,
            ["companyName"] = d.Issuer.LegalName,
            ["address"] = d.Issuer.Address,
            ["stampDate"] = d.StampDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
        Put(sender, "tradename", d.Issuer.TradeName);
        Put(sender, "province", d.Issuer.Province);
        Put(sender, "municipality", d.Issuer.Municipality);
        Put(sender, "mail", d.Issuer.Email);

        var t = d.Totals;
        var totals = new Dictionary<string, object> { ["totalAmount"] = t.Total };
        if (t.TaxedTotal > 0) totals["totalTaxedAmount"] = t.TaxedTotal;
        if (t.Taxed1 > 0) { totals["i1AmountTaxed"] = t.Taxed1; totals["itbisS1"] = 18; totals["itbis1Total"] = t.Itbis1; }
        if (t.Taxed2 > 0) { totals["i2AmountTaxed"] = t.Taxed2; totals["itbisS2"] = 16; totals["itbis2Total"] = t.Itbis2; }
        if (t.Taxed3 > 0) { totals["i3AmountTaxed"] = t.Taxed3; totals["itbisS3"] = 0; totals["itbis3Total"] = t.Itbis3; }
        if (t.Exempt > 0) totals["exemptAmount"] = t.Exempt;
        if (t.ItbisTotal > 0) totals["itbisTotal"] = t.ItbisTotal;
        if (t.NonBillable > 0) totals["nonBillableAmount"] = t.NonBillable;

        var body = new Dictionary<string, object>
        {
            ["idDoc"] = idDoc,
            ["sender"] = sender,
            ["totals"] = totals,
            ["itemDetails"] = d.Lines.Select(l => new Dictionary<string, object>
            {
                ["lineNumber"] = l.LineNumber,
                ["billingIndicator"] = l.BillingIndicator,
                ["itemName"] = l.Name,
                ["goodServiceIndicator"] = l.GoodService,
                ["quantityItem"] = l.Quantity,
                ["unitPriceItem"] = l.UnitPrice,
                ["itemAmount"] = l.Amount,
            }).ToList(),
        };

        if (d.Buyer is { } b && (!string.IsNullOrWhiteSpace(b.Rnc) || !string.IsNullOrWhiteSpace(b.Name)))
        {
            var buyer = new Dictionary<string, object>();
            Put(buyer, "rnc", b.Rnc);
            Put(buyer, "companyName", b.Name);
            body["buyer"] = buyer;
        }
        if (!string.IsNullOrWhiteSpace(account.CompanyId))
            body["company"] = new Dictionary<string, object> { ["id"] = account.CompanyId };

        using var doc = await SendAsync(account.Environment, HttpMethod.Post, PathFor(d.DocType), body, ct);
        return ParseDocument(doc.RootElement);
    }

    public async Task<FiscalDocumentResult> GetDocumentAsync(FiscalAccount account, int docType, string providerId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(account.Environment, HttpMethod.Get, $"{PathFor(docType)}/{Uri.EscapeDataString(providerId)}", null, ct);
        return ParseDocument(doc.RootElement);
    }

    // ── Respuesta ───────────────────────────────────────────────────────────────────────────────────────────────

    private static FiscalDocumentResult ParseDocument(JsonElement root)
    {
        var status = FindString(root, "status")?.ToUpperInvariant();
        var legal = FindString(root, "legalStatus")?.ToUpperInvariant();

        // FINISHED es el único estado final de Alanube; el veredicto de la DGII lo da legalStatus.
        var neutral = status switch
        {
            "FINISHED" when legal is "ACCEPTED" or "ACCEPTED_WITH_OBSERVATIONS" => "accepted",
            "FINISHED" when legal == "REJECTED" => "rejected",
            "FAILED" => "failed",
            _ => legal == "REJECTED" ? "rejected" : "processing",
        };

        string? code = null, message = null;
        if (neutral is "rejected" or "failed")
        {
            var gov = new List<string>();
            if (root.TryGetProperty("governmentResponse", out var g) && g.ValueKind == JsonValueKind.Object
                && g.TryGetProperty("value", out var vals) && vals.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vals.EnumerateArray())
                {
                    var c = v.TryGetProperty("codigo", out var cc) ? cc.ToString() : null;
                    var m = v.TryGetProperty("valor", out var mm) ? mm.ToString() : null;
                    if (!string.IsNullOrWhiteSpace(m)) gov.Add(string.IsNullOrWhiteSpace(c) ? m! : $"{c}: {m}");
                    code ??= c;
                }
            }
            message = gov.Count > 0 ? string.Join(" · ", gov) : "La DGII no aceptó el comprobante.";
        }

        return new FiscalDocumentResult(
            FindString(root, "id"), neutral, legal,
            FindString(root, "documentNumber") ?? FindString(root, "encf"),
            FindString(root, "xml"), FindString(root, "pdf"), FindString(root, "securityCode"), FindString(root, "documentStampUrl"),
            code, message);
    }

    // ── HTTP ────────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<JsonDocument> SendAsync(string environment, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var token = Token(environment);
        if (string.IsNullOrWhiteSpace(token))
            throw new FiscalProviderException("PROVIDER_NOT_CONFIGURED", "La facturación electrónica todavía no está configurada en la plataforma.");

        using var request = new HttpRequestMessage(method, (environment == "production" ? ProductionUrl : SandboxUrl) + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string text;
        try
        {
            using var client = _http.CreateClient(HttpClientName);
            response = await client.SendAsync(request, ct);
            text = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FiscalProviderException("PROVIDER_UNREACHABLE", "No hubo respuesta de Alanube. El comprobante puede haberse enviado: consulta su estado antes de reintentar.", transient: true);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text); }
                catch (JsonException) { throw new FiscalProviderException("BAD_RESPONSE", "Alanube devolvió una respuesta ilegible.", transient: true); }
            }

            var (code, message) = ReadError(text);
            var transient = (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout;
            throw new FiscalProviderException(code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), message ?? $"Alanube respondió {(int)response.StatusCode}.", transient);
        }
    }

    private static (string? Code, string? Message) ReadError(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.ToString() : null;
            string? message = null;
            if (root.TryGetProperty("message", out var m))
                message = m.ValueKind == JsonValueKind.Array ? string.Join(" · ", m.EnumerateArray().Select(x => x.ToString())) : m.ToString();
            return (code, message);
        }
        catch (JsonException) { return (null, null); }
    }

    // ── Utilidades ──────────────────────────────────────────────────────────────────────────────────────────────

    private static void Put(Dictionary<string, object> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) target[key] = value.Trim();
    }

    /// <summary>Busca una propiedad por nombre (sin distinguir mayúsculas), primero en el nivel actual y luego hacia dentro.</summary>
    public static string? FindString(JsonElement root, string name)
    {
        var queue = new Queue<JsonElement>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var e = queue.Dequeue();
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in e.EnumerateObject())
                {
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                        && p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                        return p.Value.ToString();
                }
                foreach (var p in e.EnumerateObject())
                    if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) queue.Enqueue(p.Value);
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in e.EnumerateArray())
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array) queue.Enqueue(item);
            }
        }
        return null;
    }

    private static DateTime? FindDate(JsonElement root, string name) =>
        DateTime.TryParse(FindString(root, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;
}
