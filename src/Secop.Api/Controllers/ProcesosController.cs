using MediatR;
using Microsoft.AspNetCore.Mvc;
using Secop.Application.Interfaces;
using Secop.Application.UseCases.Alertas.EnviarAlertasProveedor;
using Secop.Application.UseCases.Decisiones.RegistrarDecision;
using Secop.Application.UseCases.Procesos.BuscarProcesosCompatibles;
using Secop.Application.UseCases.Procesos.ObtenerDetalleProceso;
using Secop.Application.UseCases.Procesos.SincronizarProcesos;
using Secop.Domain.Enums;

namespace Secop.Api.Controllers;

/// <summary>
/// Gestión y consulta de procesos de contratación de SECOP II.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProcesosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProveedorRepository _proveedores;

    public ProcesosController(IMediator mediator, IProveedorRepository proveedores)
    {
        _mediator    = mediator;
        _proveedores = proveedores;
    }

    /// <summary>
    /// Lista procesos de SECOP II compatibles con el perfil del grupo empresarial, ordenados por puntaje descendente.
    /// </summary>
    /// <remarks>
    /// Consulta los puntajes precalculados en base de datos. Los procesos ya pasaron el umbral mínimo
    /// de similitud semántica (0.65) y tienen un puntaje calculado con los 5 componentes del scoring:
    /// similitud (35), requisitos habilitantes (25), tiempo disponible (20), competencia (12), historial entidad (8).
    /// Etiquetas: Proponer ≥70 pts · Analizar ≥40 pts · Descartar &lt;40 pts.
    /// </remarks>
    /// <param name="proveedorId">Filtra por proveedor específico. Si se omite, devuelve para todo el grupo.</param>
    /// <param name="limite">Número máximo de resultados (default 20).</param>
    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] Guid? proveedorId = null,
        [FromQuery] int limite = 20,
        CancellationToken ct = default)
    {
        var resultado = await _mediator.Send(new BuscarProcesosQuery(proveedorId, limite), ct);
        return Ok(resultado);
    }

    /// <summary>
    /// Devuelve el detalle completo y puntaje de un proceso específico.
    /// </summary>
    /// <param name="id">Identificador del proceso en SECOP II (ej: ES-CO-XXXX-XXXX).</param>
    /// <param name="proveedorId">Proveedor para el que se devuelve el puntaje. Si se omite, usa el primer proveedor con puntaje calculado.</param>
    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerDetalle(
        string id,
        [FromQuery] Guid? proveedorId = null,
        CancellationToken ct = default)
    {
        var resultado = await _mediator.Send(new ObtenerDetalleQuery(id, proveedorId), ct);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    /// <summary>
    /// Dispara manualmente un ciclo de sincronización con SECOP II (equivalente a un tick del worker horario).
    /// </summary>
    /// <remarks>
    /// Ejecuta el flujo completo:
    /// 1. Descarga procesos publicados entre `desde` y `hasta` (o desde hace N horas si se omiten las fechas).
    /// 2. Filtra duplicados (procesos ya existentes en BD).
    /// 3. Genera embedding OpenAI para cada proceso nuevo (Título + Objeto).
    /// 4. Calcula puntaje para cada proveedor con embedding (si similitud ≥ 0.65).
    /// 5. Envía alerta Telegram al proveedor si el puntaje ≥ 70.
    ///
    /// Ejemplos:
    /// - `?desde=2026-04-01` → procesos desde el 1 de abril hasta ahora.
    /// - `?desde=2026-04-01&amp;hasta=2026-04-10` → intervalo exacto.
    /// - `?horasAtras=48` → últimas 48 horas (usado por el worker automático).
    /// </remarks>
    /// <param name="desde">Fecha inicio del intervalo (UTC). Si se omite, se usa `horasAtras`.</param>
    /// <param name="hasta">Fecha fin del intervalo (UTC). Si se omite, se usa la hora actual.</param>
    /// <param name="horasAtras">Horas hacia atrás desde ahora (default 2). Solo se usa si `desde` no se especifica.</param>
    [HttpPost("sincronizar")]
    public async Task<IActionResult> Sincronizar(
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] int horasAtras = 2,
        CancellationToken ct = default)
    {
        var fechaDesde = desde ?? DateTime.UtcNow.AddHours(-horasAtras);
        var fechaHasta = hasta ?? DateTime.UtcNow;
        var nuevos = await _mediator.Send(new SincronizarProcesosCommand(fechaDesde, fechaHasta), ct);
        return Ok(new { ProcesosNuevos = nuevos, Desde = fechaDesde, Hasta = fechaHasta });
    }

    /// <summary>
    /// Dispara manualmente las alertas RUP para todos los proveedores (equivalente a un tick del worker diario).
    /// </summary>
    /// <remarks>
    /// Para cada proveedor: verifica si el RUP vence en los próximos 30 días y envía alerta Telegram si corresponde.
    /// El RUP vencido es inhabilitante — los procesos de un proveedor con RUP vencido reciben puntaje 0 automáticamente.
    /// </remarks>
    [HttpPost("alertas-rup")]
    public async Task<IActionResult> EnviarAlertasRup(CancellationToken ct = default)
    {
        var todos = await _proveedores.ObtenerTodosAsync(ct);
        foreach (var p in todos)
            await _mediator.Send(new EnviarAlertasCommand(p.Id), ct);
        return Ok(new { ProveedoresProcesados = todos.Count() });
    }

    /// <summary>
    /// Registra la decisión de un proveedor sobre un proceso (Propuso, Descartó, Ganó, Perdió).
    /// </summary>
    /// <remarks>
    /// Persiste la decisión en la tabla `decisiones` para análisis histórico y futura retroalimentación del modelo de scoring.
    /// Este endpoint es invocado internamente por el bot de Telegram cuando el usuario presiona un botón inline.
    /// </remarks>
    /// <param name="id">Identificador del proceso en SECOP II.</param>
    [HttpPost("{id}/decision")]
    public async Task<IActionResult> RegistrarDecision(
        string id,
        [FromBody] RegistrarDecisionRequest request,
        CancellationToken ct = default)
    {
        await _mediator.Send(new RegistrarDecisionCommand(
            id, request.ProveedorId, request.Accion, request.RazonDescarte), ct);
        return NoContent();
    }
}

/// <summary>
/// Cuerpo del request para registrar una decisión sobre un proceso.
/// </summary>
public record RegistrarDecisionRequest(
    /// <summary>ID del proveedor que toma la decisión.</summary>
    Guid ProveedorId,
    /// <summary>Acción tomada: Propuso, Descarto, Gano, Perdio.</summary>
    AccionDecision Accion,
    /// <summary>Razón del descarte (requerida si Accion = Descarto).</summary>
    string? RazonDescarte = null);
