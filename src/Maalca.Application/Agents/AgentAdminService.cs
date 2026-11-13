using System.Text.RegularExpressions;
using Maalca.Application.Common;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Maalca.Application.Agents;

// ---------- DTOs de /ops ----------

public record AgentDefinitionDto(string Key, string Name, string? Description, string SystemPrompt, string ModelTier,
    List<string> Tools, string Visibility, int MaxOutputTokens, bool IsActive, DateTime? UpdatedAt, string? UpdatedBy);

public record UpsertAgentDefinitionRequest(string Name, string? Description, string SystemPrompt, string ModelTier,
    List<string>? Tools, string? Visibility, int? MaxOutputTokens, bool? IsActive);

public record AgentModelDto(string Tier, string ProviderKey, string ModelId, decimal InputPricePerMTok,
    decimal OutputPricePerMTok, bool IsActive, DateTime? UpdatedAt, string? UpdatedBy);

public record UpsertAgentModelRequest(string ProviderKey, string ModelId, decimal InputPricePerMTok,
    decimal OutputPricePerMTok, bool? IsActive);

/// <param name="HasKey">true si hay llave guardada en /ops o en la variable de entorno del API.</param>
/// <param name="KeySource">"ops", "env" o "none".</param>
public record AgentProviderDto(string Key, string Kind, string BaseUrl, bool HasKey, string KeySource,
    string? ApiKeyLast4, bool IsActive);

/// <param name="ApiKey">Vacío o null = conservar la llave que ya está guardada.</param>
public record UpsertAgentProviderRequest(string? Kind, string? BaseUrl, string? ApiKey, bool? IsActive);

public record AgentToolDto(string Name, string Description, bool InternalOnly);

public record AgentPauseDto(string Scope, bool Paused, string? Reason, DateTime? UpdatedAt, string? UpdatedBy);

public record SetAgentPauseRequest(string? Scope, bool Paused, string? Reason);

public record AgentUsageRowDto(string AgentKey, Guid? AffiliateId, int Jobs, int Failed, long TokensInput,
    long TokensOutput, decimal CostUsd);

public record AgentUsageDto(DateTime From, int Jobs, int Failed, decimal CostUsd, List<AgentUsageRowDto> Rows);

public record AgentAdminStateDto(List<AgentDefinitionDto> Agents, List<AgentModelDto> Models,
    List<AgentProviderDto> Providers, List<AgentToolDto> Tools, List<AgentPauseDto> Pauses, AgentUsageDto UsageThisMonth);

public record AgentJobDto(Guid Id, string AgentKey, Guid? AffiliateId, string Kind, string Status, int Attempts,
    string? ModelId, int TokensInput, int TokensOutput, decimal CostUsd, long DurationMs, int ToolCalls,
    string? ErrorCode, string? Error, string? RequestedBy, DateTime CreatedAt, DateTime? FinishedAt);

public interface IAgentAdminService
{
    Task<AgentAdminStateDto> GetStateAsync();
    Task<AgentDefinitionDto> UpsertAgentAsync(string key, UpsertAgentDefinitionRequest request, string? by);
    Task<AgentModelDto> UpsertModelAsync(string tier, UpsertAgentModelRequest request, string? by);
    Task<AgentProviderDto> UpsertProviderAsync(string key, UpsertAgentProviderRequest request, string? by);
    Task<AgentPauseDto> SetPauseAsync(SetAgentPauseRequest request, string? by);
    Task<List<AgentJobDto>> GetJobsAsync(int limit, string? agentKey, string? status);
}

/// <summary>
/// Todo lo que /ops configura del orquestador sin desplegar: fichas de agentes, registro de modelos,
/// llave del proveedor, pausa y consumo. Las validaciones lanzan ArgumentException (→ 400 INVALID_INPUT).
/// </summary>
public class AgentAdminService : IAgentAdminService
{
    private static readonly Regex KeyPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);
    private static readonly string[] Visibilities = { "internal", "client" };

    private readonly AppDbContext _db;
    private readonly IEnumerable<IAgentTool> _tools;
    private readonly IEnumerable<IAgentModelClient> _clients;
    private readonly IConfiguration _config;
    private readonly string _secret;

    public AgentAdminService(AppDbContext db, IEnumerable<IAgentTool> tools, IEnumerable<IAgentModelClient> clients,
        IConfiguration config)
    {
        _db = db;
        _tools = tools;
        _clients = clients;
        _config = config;
        _secret = config["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
    }

    public async Task<AgentAdminStateDto> GetStateAsync()
    {
        var agents = await _db.AgentDefinitions.AsNoTracking().OrderBy(a => a.Key).ToListAsync();
        var models = await _db.AgentModels.AsNoTracking().OrderBy(m => m.Tier).ToListAsync();
        var providers = await _db.AgentProviders.AsNoTracking().OrderBy(p => p.Key).ToListAsync();
        var pauses = await _db.AgentControls.AsNoTracking().Where(c => c.Paused).OrderBy(c => c.Scope).ToListAsync();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return new AgentAdminStateDto(
            agents.Select(a => ToDto(a)).ToList(),
            models.Select(m => ToDto(m)).ToList(),
            providers.Select(p => ToDto(p)).ToList(),
            _tools.OrderBy(t => t.Name).Select(t => new AgentToolDto(t.Name, t.Description, t.InternalOnly)).ToList(),
            pauses.Select(c => ToDto(c)).ToList(),
            await GetUsageAsync(monthStart));
    }

    public async Task<AgentDefinitionDto> UpsertAgentAsync(string key, UpsertAgentDefinitionRequest request, string? by)
    {
        key = NormalizeKey(key, "La clave del agente");
        var name = Required(request.Name, "El nombre", 120);
        var prompt = Required(request.SystemPrompt, "Las instrucciones", 20_000);
        var tier = NormalizeKey(request.ModelTier, "El nivel de modelo");
        if (!await _db.AgentModels.AnyAsync(m => m.Tier == tier))
            throw new ArgumentException($"No existe el nivel de modelo \"{tier}\".");

        var visibility = string.IsNullOrWhiteSpace(request.Visibility) ? "internal" : request.Visibility.Trim().ToLowerInvariant();
        if (!Visibilities.Contains(visibility))
            throw new ArgumentException("La visibilidad debe ser \"internal\" o \"client\".");

        var known = _tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var tools = new List<string>();
        foreach (var raw in request.Tools ?? new List<string>())
        {
            var toolName = (raw ?? string.Empty).Trim();
            if (toolName.Length == 0) continue;
            if (!known.TryGetValue(toolName, out var tool))
                throw new ArgumentException($"La herramienta \"{toolName}\" no existe.");
            // Una herramienta interna ve datos de toda la plataforma: nunca en un agente que usa un dueño.
            if (tool.InternalOnly && visibility != "internal")
                throw new ArgumentException($"La herramienta \"{tool.Name}\" es solo para agentes internos.");
            if (!tools.Contains(tool.Name)) tools.Add(tool.Name);
        }

        var maxTokens = request.MaxOutputTokens ?? 1024;
        if (maxTokens < 64 || maxTokens > 16_000)
            throw new ArgumentException("El máximo de tokens de respuesta debe estar entre 64 y 16000.");

        var entity = await _db.AgentDefinitions.FirstOrDefaultAsync(a => a.Key == key);
        if (entity is null)
        {
            entity = new AgentDefinition { Key = key, CreatedBy = by };
            _db.AgentDefinitions.Add(entity);
        }
        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.SystemPrompt = prompt;
        entity.ModelTier = tier;
        entity.Tools = string.Join(",", tools);
        entity.Visibility = visibility;
        entity.MaxOutputTokens = maxTokens;
        entity.IsActive = request.IsActive ?? true;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = by;
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<AgentModelDto> UpsertModelAsync(string tier, UpsertAgentModelRequest request, string? by)
    {
        tier = NormalizeKey(tier, "El nivel");
        var providerKey = NormalizeKey(request.ProviderKey, "El proveedor");
        if (!await _db.AgentProviders.AnyAsync(p => p.Key == providerKey))
            throw new ArgumentException($"No existe el proveedor \"{providerKey}\".");
        var modelId = Required(request.ModelId, "El identificador del modelo", 120);
        if (request.InputPricePerMTok < 0 || request.OutputPricePerMTok < 0)
            throw new ArgumentException("Los precios no pueden ser negativos.");

        var entity = await _db.AgentModels.FirstOrDefaultAsync(m => m.Tier == tier);
        if (entity is null)
        {
            entity = new AgentModel { Tier = tier, CreatedBy = by };
            _db.AgentModels.Add(entity);
        }
        entity.ProviderKey = providerKey;
        entity.ModelId = modelId;
        entity.InputPricePerMTok = request.InputPricePerMTok;
        entity.OutputPricePerMTok = request.OutputPricePerMTok;
        entity.IsActive = request.IsActive ?? true;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = by;
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<AgentProviderDto> UpsertProviderAsync(string key, UpsertAgentProviderRequest request, string? by)
    {
        key = NormalizeKey(key, "La clave del proveedor");
        var entity = await _db.AgentProviders.FirstOrDefaultAsync(p => p.Key == key);
        if (entity is null)
        {
            entity = new AgentProvider { Key = key, CreatedBy = by };
            _db.AgentProviders.Add(entity);
        }

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            var kind = request.Kind.Trim().ToLowerInvariant();
            if (!_clients.Any(c => c.Kind == kind))
                throw new ArgumentException($"No hay un cliente para proveedores de tipo \"{kind}\".");
            entity.Kind = kind;
        }

        if (!string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            var baseUrl = request.BaseUrl.Trim().TrimEnd('/');
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new ArgumentException("La URL base no es válida.");
            entity.BaseUrl = baseUrl;
        }

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            var apiKey = request.ApiKey.Trim();
            if (apiKey.Length < 8 || apiKey.Length > 500)
                throw new ArgumentException("La llave no tiene un largo válido.");
            entity.ApiKeyEnc = SecretBox.Protect(apiKey, _secret);
            entity.ApiKeyLast4 = apiKey[^4..];
        }

        entity.IsActive = request.IsActive ?? entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = by;
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<AgentPauseDto> SetPauseAsync(SetAgentPauseRequest request, string? by)
    {
        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "global" : request.Scope.Trim().ToLowerInvariant();
        if (scope != "global")
        {
            if (!Guid.TryParse(scope, out var affiliateId))
                throw new ArgumentException("El alcance debe ser \"global\" o el id de un negocio.");
            if (!await _db.Affiliates.AnyAsync(a => a.Id == affiliateId))
                throw new ArgumentException("Ese negocio no existe.");
            scope = affiliateId.ToString();
        }

        var entity = await _db.AgentControls.FirstOrDefaultAsync(c => c.Scope == scope);
        if (entity is null)
        {
            entity = new AgentControl { Scope = scope };
            _db.AgentControls.Add(entity);
        }
        entity.Paused = request.Paused;
        entity.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = by;
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<List<AgentJobDto>> GetJobsAsync(int limit, string? agentKey, string? status)
    {
        var query = _db.AgentJobs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(agentKey)) query = query.Where(j => j.AgentKey == agentKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(j => j.Status == status);
        var rows = await query.OrderByDescending(j => j.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToListAsync();
        return rows.Select(j => new AgentJobDto(j.Id, j.AgentKey, j.AffiliateId, j.Kind, j.Status, j.Attempts, j.ModelId,
            j.TokensInput, j.TokensOutput, j.CostUsd, j.DurationMs, j.ToolCalls, j.ErrorCode, j.Error, j.RequestedBy,
            j.CreatedAt, j.FinishedAt)).ToList();
    }

    private async Task<AgentUsageDto> GetUsageAsync(DateTime from)
    {
        // Se agrupa en memoria: son pocas filas por mes y evita depender de cómo traduce EF el GroupBy con nulos.
        var jobs = await _db.AgentJobs.AsNoTracking()
            .Where(j => j.CreatedAt >= from)
            .Select(j => new { j.AgentKey, j.AffiliateId, j.Status, j.TokensInput, j.TokensOutput, j.CostUsd })
            .ToListAsync();

        var rows = jobs
            .GroupBy(j => new { j.AgentKey, j.AffiliateId })
            .Select(g => new AgentUsageRowDto(
                g.Key.AgentKey,
                g.Key.AffiliateId,
                g.Count(),
                g.Count(j => j.Status == AgentJobStatus.Failed),
                g.Sum(j => (long)j.TokensInput),
                g.Sum(j => (long)j.TokensOutput),
                g.Sum(j => j.CostUsd)))
            .OrderByDescending(r => r.CostUsd)
            .ToList();

        return new AgentUsageDto(from, jobs.Count, jobs.Count(j => j.Status == AgentJobStatus.Failed),
            jobs.Sum(j => j.CostUsd), rows);
    }

    private static AgentDefinitionDto ToDto(AgentDefinition a) => new(a.Key, a.Name, a.Description, a.SystemPrompt,
        a.ModelTier,
        a.Tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        a.Visibility, a.MaxOutputTokens, a.IsActive, a.UpdatedAt, a.UpdatedBy);

    private static AgentModelDto ToDto(AgentModel m) => new(m.Tier, m.ProviderKey, m.ModelId, m.InputPricePerMTok,
        m.OutputPricePerMTok, m.IsActive, m.UpdatedAt, m.UpdatedBy);

    private AgentProviderDto ToDto(AgentProvider p)
    {
        var hasStored = !string.IsNullOrEmpty(p.ApiKeyEnc);
        var hasEnv = p.Kind == "anthropic" && !string.IsNullOrWhiteSpace(
            _config["Anthropic:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        var source = hasStored ? "ops" : hasEnv ? "env" : "none";
        return new AgentProviderDto(p.Key, p.Kind, p.BaseUrl, hasStored || hasEnv, source, p.ApiKeyLast4, p.IsActive);
    }

    private static AgentPauseDto ToDto(AgentControl c) => new(c.Scope, c.Paused, c.Reason, c.UpdatedAt, c.UpdatedBy);

    private static string NormalizeKey(string? value, string label)
    {
        var key = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length == 0 || key.Length > 60 || !KeyPattern.IsMatch(key))
            throw new ArgumentException($"{label} debe ir en minúsculas, con letras, números y guiones (máximo 60).");
        return key;
    }

    private static string Required(string? value, string label, int max)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) throw new ArgumentException($"{label} es obligatorio.");
        if (text.Length > max) throw new ArgumentException($"{label} no puede pasar de {max} caracteres.");
        return text;
    }
}
