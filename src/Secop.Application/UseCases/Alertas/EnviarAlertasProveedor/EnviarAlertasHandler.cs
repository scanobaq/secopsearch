using MediatR;
using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;

namespace Secop.Application.UseCases.Alertas.EnviarAlertasProveedor;

public class EnviarAlertasHandler : IRequestHandler<EnviarAlertasCommand>
{
    private readonly IProveedorRepository _proveedores;
    private readonly IProcesoRepository _procesos;
    private readonly IPuntajeRepository _puntajes;
    private readonly IAlertaService _alertas;
    private readonly ILogger<EnviarAlertasHandler> _logger;

    private const int DiasAlertaRup = 30;

    public EnviarAlertasHandler(
        IProveedorRepository proveedores,
        IProcesoRepository procesos,
        IPuntajeRepository puntajes,
        IAlertaService alertas,
        ILogger<EnviarAlertasHandler> logger)
    {
        _proveedores = proveedores;
        _procesos = procesos;
        _puntajes = puntajes;
        _alertas = alertas;
        _logger = logger;
    }

    public async Task Handle(EnviarAlertasCommand request, CancellationToken ct)
    {
        var proveedor = await _proveedores.ObtenerPorIdAsync(request.ProveedorId, ct);
        if (proveedor?.TelegramChatId is null) return;

        var chatId = proveedor.TelegramChatId.Value;

        // Alertas de evaluaciones reviewables y accionables.
        var puntajesProveedor = await _puntajes.ObtenerPorProveedorAsync(proveedor.Id, ct);
        var pendientes = puntajesProveedor
            .Where(p => p.EsAlertable)
            .ToList();

        foreach (var puntaje in pendientes)
        {
            var proceso = await _procesos.ObtenerPorIdAsync(puntaje.ProcesoId, ct);
            if (proceso is null) continue;

            await _alertas.EnviarAlertaProcesoAsync(chatId, puntaje, proceso, proveedor, ct);
        }

        // Alerta de RUP por vencer
        if (proveedor.DiasParaVencimientoRup() <= DiasAlertaRup)
        {
            await _alertas.EnviarAlertaRupPorVencerAsync(chatId, proveedor, ct);
            _logger.LogWarning("RUP por vencer en {Dias} días — Proveedor: {Nombre}",
                proveedor.DiasParaVencimientoRup(), proveedor.Nombre);
        }
    }
}
