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
        _mediator = mediator;
        _proveedores = proveedores;
    }

    /// <summary>
    /// Lista procesos evaluados para el grupo, ordenados por relevancia semántica descendente.
    /// </summary>
    /// <remarks>
    /// Consulta evaluaciones persistidas que pasaron el umbral inclusivo de similitud semántica 0.40.
    /// Expone relevancia porcentual, elegibilidad, accionabilidad y razones. La recomendación automática
    /// solo puede ser Analyze; Propose queda reservada para una decisión humana.
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
    /// Devuelve el detalle y la evaluación persistida de un proceso específico.
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
    /// Dispara manualmente un ciclo de sincronización con SECOP II.
    /// </summary>
    /// <remarks>
    /// Ejecuta el flujo completo:
    /// 1. Descarga procesos cuya fecha de última publicación está dentro de `[desde, hasta)`.
    /// 2. Filtra duplicados (procesos ya existentes en BD).
    /// 3. Genera embedding OpenAI para cada proceso nuevo (Título + Objeto).
    /// 4. Evalúa cada proveedor con embedding si la similitud es ≥ 0.40.
    /// 5. Envía una recomendación Analyze solo si la evaluación es alertable.
    ///
    /// Ejemplos:
    /// - `?desde=2026-04-01` → procesos publicados desde el 1 de abril.
    /// - `?desde=2026-04-01&amp;hasta=2026-04-10` → publicaciones en `[desde, hasta)`.
    /// - `?horasAtras=48` → procesos publicados durante las últimas 48 horas.
    /// </remarks>
    /// <param name="desde">Inicio inclusivo del intervalo de última publicación en SECOP (UTC). Si se omite, se usa `horasAtras`.</param>
    /// <param name="hasta">Fin exclusivo del intervalo de última publicación en SECOP (UTC). Si se omite, se usa la hora actual.</param>
    /// <param name="horasAtras">Horas de publicación que se consultan hacia atrás desde ahora (default 2). Solo se usa si `desde` no se especifica.</param>
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
    /// Durante la experimentación, un RUP vencido no afecta la elegibilidad de oportunidades;
    /// este bloqueo debe restaurarse y validarse antes de producción.
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
