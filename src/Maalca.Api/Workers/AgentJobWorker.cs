using Maalca.Application.Agents;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Api.Workers;

/// <summary>
/// Toma los trabajos de agente en cola (AgentJobs con Status = queued) y los ejecuta de uno en uno.
/// La cola es la propia tabla en PostgreSQL: FOR UPDATE SKIP LOCKED deja que varias instancias del
/// API tomen trabajos distintos sin pisarse, sin Redis ni otro servicio. Mismo patrón de ciclo que
/// ScreenMonitorWorker. Los chats de /ops no pasan por aquí: se ejecutan en la misma petición.
/// </summary>
public class AgentJobWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    // Un trabajo "running" con más de 15 minutos quedó huérfano (el proceso murió a mitad).

    // Toma un trabajo y lo marca running en una sola sentencia. No toma nada si hay pausa global,
    // ni trabajos de un negocio en pausa (se quedan en cola hasta que se reanude).
    private const string ClaimSql = @"
UPDATE ""AgentJobs"" SET ""Status"" = 'running', ""LockedAt"" = now(), ""StartedAt"" = now(), ""Attempts"" = ""Attempts"" + 1, ""UpdatedAt"" = now()
WHERE ""Id"" = (
    SELECT j.""Id"" FROM ""AgentJobs"" j
    WHERE j.""Status"" = 'queued' AND j.""RunAfter"" <= now()
      AND NOT EXISTS (SELECT 1 FROM ""AgentControls"" c WHERE c.""Paused"" AND (c.""Scope"" = 'global' OR c.""Scope"" = j.""AffiliateId""::text))
    ORDER BY j.""RunAfter""
    LIMIT 1
    FOR UPDATE SKIP LOCKED
)
RETURNING ""Id"";";

    // Huérfanos: vuelven a la cola si les quedan intentos; si no, quedan como fallidos.
    private const string RequeueStaleSql = @"
UPDATE ""AgentJobs"" SET ""Status"" = 'queued', ""LockedAt"" = NULL, ""UpdatedAt"" = now()
WHERE ""Status"" = 'running' AND ""Kind"" <> 'chat' AND ""Attempts"" < ""MaxAttempts"" AND ""LockedAt"" < now() - interval '15 minutes';";

    private const string FailStaleSql = @"
UPDATE ""AgentJobs"" SET ""Status"" = 'failed', ""ErrorCode"" = 'STALE', ""Error"" = 'El trabajo quedó a medias y agotó sus intentos.', ""FinishedAt"" = now(), ""UpdatedAt"" = now()
WHERE ""Status"" = 'running' AND (""Kind"" = 'chat' OR ""Attempts"" >= ""MaxAttempts"") AND ""LockedAt"" < now() - interval '15 minutes';";

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AgentJobWorker> _logger;

    public AgentJobWorker(IServiceScopeFactory scopes, ILogger<AgentJobWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Margen al arrancar: las migraciones corren en el arranque y la tabla puede no existir todavía.
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); } catch (OperationCanceledException) { return; }

        var lastSweep = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                if (DateTime.UtcNow - lastSweep > TimeSpan.FromMinutes(5))
                {
                    await SweepStaleAsync(stoppingToken);
                    lastSweep = DateTime.UtcNow;
                }
                worked = await RunOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AgentJobWorker] ciclo falló");
            }

            // Si hubo trabajo, se busca el siguiente enseguida; si no, se espera.
            if (!worked)
            {
                try { await Task.Delay(IdleDelay, stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task<bool> RunOneAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobId = await ClaimAsync(db, ct);
        if (jobId is null) return false;

        var runner = scope.ServiceProvider.GetRequiredService<IAgentRunner>();
        await runner.ExecuteClaimedAsync(jobId.Value, ct);
        return true;
    }

    private static async Task<Guid?> ClaimAsync(AppDbContext db, CancellationToken ct)
    {
        // ADO.NET directo: UPDATE … RETURNING no es componible y así no depende de cómo lo envuelva EF.
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = ClaimSql;
            var result = await command.ExecuteScalarAsync(ct);
            return result is Guid id ? id : null;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task SweepStaleAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(RequeueStaleSql, ct);
        await db.Database.ExecuteSqlRawAsync(FailStaleSql, ct);
    }
}
