using System.Text.Json;
using Maalca.Application.Common.Interfaces;

namespace Maalca.Application.Agents.Tools;

// Herramientas del asistente interno de /ops. Devuelven los mismos datos que antes se pegaban
// enteros en el prompt (overview + afiliados), pero ahora el modelo los pide solo cuando los necesita.

internal static class AgentToolJson
{
    public const string NoInputSchema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public class OpsOverviewTool : IAgentTool
{
    private readonly IPlatformAdminService _ops;

    public OpsOverviewTool(IPlatformAdminService ops)
    {
        _ops = ops;
    }

    public string Name => "ops_overview";
    public string Description =>
        "Resumen de la plataforma MaalCa en este momento: total de negocios, cuántos en cada plan, MRR en USD, " +
        "altas del mes y páginas publicadas. Úsala para preguntas de totales o ingresos.";
    public string InputSchemaJson => AgentToolJson.NoInputSchema;
    public bool InternalOnly => true;

    public async Task<string> ExecuteAsync(AgentToolContext context, JsonElement input, CancellationToken ct)
    {
        var overview = await _ops.GetOverviewAsync();
        return JsonSerializer.Serialize(overview, AgentToolJson.Options);
    }
}

public class OpsAffiliatesTool : IAgentTool
{
    private readonly IPlatformAdminService _ops;

    public OpsAffiliatesTool(IPlatformAdminService ops)
    {
        _ops = ops;
    }

    public string Name => "ops_affiliates";
    public string Description =>
        "Lista de todos los negocios (afiliados) de MaalCa con su plan, estado de pagos, módulos activos y alertas. " +
        "Úsala para preguntas sobre negocios concretos, riesgo de baja o seguimiento.";
    public string InputSchemaJson => AgentToolJson.NoInputSchema;
    public bool InternalOnly => true;

    public async Task<string> ExecuteAsync(AgentToolContext context, JsonElement input, CancellationToken ct)
    {
        var affiliates = await _ops.GetAffiliatesAsync();
        return JsonSerializer.Serialize(affiliates, AgentToolJson.Options);
    }
}
