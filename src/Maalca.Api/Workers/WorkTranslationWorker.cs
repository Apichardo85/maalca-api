using Maalca.Application.Common.Interfaces;

namespace Maalca.Api.Workers;

/// <summary>
/// Hace los borradores de traducción que el autor pidió desde el panel (WorkChapterTranslations con
/// Status = queued), un capítulo a la vez. La cola es la propia tabla: reiniciar el API no pierde nada
/// (lo que quedó a medias vuelve a la cola, ver WorkTranslationService.ProcessNextAsync).
/// </summary>
public class WorkTranslationWorker : BackgroundService
{
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WorkTranslationWorker> _logger;

    public WorkTranslationWorker(IServiceScopeFactory scopes, ILogger<WorkTranslationWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Margen al arrancar: las migraciones corren en el arranque.
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IWorkTranslationService>();
                worked = await svc.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WorkTranslationWorker] ciclo falló");
            }

            // Con trabajo, sigue con el próximo capítulo; sin trabajo, espera.
            try { await Task.Delay(worked ? TimeSpan.FromSeconds(1) : Idle, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
