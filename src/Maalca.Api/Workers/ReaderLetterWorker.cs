using Maalca.Application.Creator;

namespace Maalca.Api.Workers;

/// <summary>
/// Manda las cartas del club de lectores que el autor puso en cola: lotes de 50 por ciclo hasta vaciar la
/// audiencia. Cada entrega queda registrada (ReaderLetterDelivery), así que reiniciar el API no duplica
/// correos: el siguiente ciclo sigue con quien falta. Mismo ciclo que SocialPublishWorker.
/// </summary>
public class ReaderLetterWorker : BackgroundService
{
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReaderLetterWorker> _logger;

    public ReaderLetterWorker(IServiceScopeFactory scopes, ILogger<ReaderLetterWorker> logger)
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
            var sent = 0;
            try
            {
                using var scope = _scopes.CreateScope();
                var club = scope.ServiceProvider.GetRequiredService<IReaderClubService>();
                sent = await club.ProcessQueuedLettersAsync(stoppingToken);
                if (sent > 0) _logger.LogInformation("[ReaderLetterWorker] {N} cartas salieron", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReaderLetterWorker] ciclo falló");
            }

            // Con lote enviado, sigue enseguida (pausa corta para no saturar Resend); sin trabajo, espera.
            try { await Task.Delay(sent > 0 ? TimeSpan.FromSeconds(2) : Idle, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
