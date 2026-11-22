using Maalca.Application.Agents;

namespace Maalca.Api.Workers;

/// <summary>
/// Fase 4 del estudio de contenido: cada minuto publica las piezas aprobadas cuya fecha ya llegó.
/// No decide nada: solo saca lo que un humano aprobó. Si una pieza falla tres veces seguidas, se queda quieta con su
/// error a la vista en /ops/contenido hasta que alguien pulse "Publicar ahora". Mismo ciclo que FiscalSyncWorker.
/// </summary>
public class SocialPublishWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SocialPublishWorker> _logger;

    public SocialPublishWorker(IServiceScopeFactory scopes, ILogger<SocialPublishWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Margen al arrancar: las migraciones corren en el arranque y las columnas nuevas pueden no existir todavía.
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var publisher = scope.ServiceProvider.GetRequiredService<ISocialPublisher>();
                var n = await publisher.PublishDueAsync(stoppingToken);
                if (n > 0) _logger.LogInformation("[SocialPublishWorker] {N} publicaciones salieron", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SocialPublishWorker] ciclo falló");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
