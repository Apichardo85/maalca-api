using Maalca.Application.Services.Fiscal;

namespace Maalca.Api.Workers;

/// <summary>
/// Consulta al proveedor los comprobantes que siguen "processing" hasta que la DGII da su veredicto. Es la red de
/// seguridad del webhook (que es solo un aviso): si el aviso se pierde, el comprobante igual se resuelve aquí.
/// No reenvía nada: un comprobante enviado dos veces consumiría dos e-NCF. Mismo ciclo que ScreenMonitorWorker.
/// </summary>
public class FiscalSyncWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<FiscalSyncWorker> _logger;

    public FiscalSyncWorker(IServiceScopeFactory scopes, ILogger<FiscalSyncWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Margen al arrancar: las migraciones corren en el arranque y las tablas pueden no existir todavía.
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IFiscalService>();
                await svc.SyncPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FiscalSyncWorker] ciclo falló");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
