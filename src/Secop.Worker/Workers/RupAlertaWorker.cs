using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;
using Secop.Application.UseCases.Alertas.EnviarAlertasProveedor;

namespace Secop.Worker.Workers;

/// <summary>
/// Se ejecuta una vez al día. Revisa el RUP de todos los proveedores y envía
/// alertas Telegram a los que tienen vencimiento próximo (≤ 30 días).
/// </summary>
public class RupAlertaWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RupAlertaWorker> _logger;
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    public RupAlertaWorker(IServiceProvider services, ILogger<RupAlertaWorker> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RupAlertaWorker iniciado. Intervalo: diario");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope      = _services.CreateScope();
                var mediator         = scope.ServiceProvider.GetRequiredService<IMediator>();
                var proveedorRepo    = scope.ServiceProvider.GetRequiredService<IProveedorRepository>();

                var proveedores = await proveedorRepo.ObtenerTodosAsync(stoppingToken);

                foreach (var proveedor in proveedores)
                {
                    await mediator.Send(new EnviarAlertasCommand(proveedor.Id), stoppingToken);
                }

                _logger.LogInformation("[{Hora}] Revisión de RUP completada para {N} proveedores",
                    DateTime.Now, proveedores.Count());
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error en RupAlertaWorker");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }
}
