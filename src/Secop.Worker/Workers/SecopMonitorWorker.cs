using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Secop.Application.UseCases.Procesos.SincronizarProcesos;

namespace Secop.Worker.Workers;

/// <summary>
/// Se ejecuta cada hora. Llama a SECOP II para obtener procesos nuevos desde la
/// última sincronización, genera embeddings, calcula puntajes y envía alertas Telegram.
/// </summary>
public class SecopMonitorWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SecopMonitorWorker> _logger;
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(16);

    // Ventana de búsqueda hacia atrás al iniciar (por si el worker estuvo caído)
    private DateTime _ultimaSincronizacion = DateTime.UtcNow.AddHours(-2);

    public SecopMonitorWorker(IServiceProvider services, ILogger<SecopMonitorWorker> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SecopMonitorWorker iniciado. Intervalo: {Intervalo}h", Intervalo.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope   = _services.CreateScope();
                var mediator      = scope.ServiceProvider.GetRequiredService<IMediator>();

                var nuevos = await mediator.Send(
                    new SincronizarProcesosCommand(_ultimaSincronizacion), stoppingToken);

                _ultimaSincronizacion = DateTime.UtcNow;
                _logger.LogInformation("[{Hora}] Sincronización SECOP completada. Procesos nuevos: {N}",
                    DateTime.Now, nuevos);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error en sincronización SECOP. Se reintentará en {Min} min",
                    Intervalo.TotalMinutes);
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
