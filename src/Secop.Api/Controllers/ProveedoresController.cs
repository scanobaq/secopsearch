using MediatR;
using Microsoft.AspNetCore.Mvc;
using Secop.Application.Interfaces;
using Secop.Application.UseCases.Proveedores.GenerarEmbeddingProveedor;
using Secop.Application.UseCases.Puntajes.RecalcularPuntajes;

namespace Secop.Api.Controllers;

/// <summary>
/// Gestión de proveedores del grupo empresarial.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProveedoresController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProveedorRepository _proveedores;

    public ProveedoresController(IMediator mediator, IProveedorRepository proveedores)
    {
        _mediator = mediator;
        _proveedores = proveedores;
    }

    /// <summary>
    /// Lista todos los proveedores del grupo empresarial con su estado actual.
    /// </summary>
    /// <remarks>
    /// Incluye: vigencia del RUP, días restantes para vencimiento, capacidad financiera,
    /// códigos UNSPSC y si ya tiene embedding generado (requerido para la evaluación semántica).
    /// Un proveedor sin embedding no participa en la búsqueda semántica de procesos.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> ObtenerTodos(CancellationToken ct)
    {
        var resultado = await _proveedores.ObtenerTodosAsync(ct);
        return Ok(resultado.Select(p => new
        {
            p.Id,
            p.Nombre,
            p.Nit,
            p.RupVigencia,
            RupVigente = p.RupVigente(),
            DiasRupRestantes = p.DiasParaVencimientoRup(),
            p.CapacidadFinanciera,
            p.CodigosUnspsc,
            p.PalabrasClave,
            TieneEmbedding = p.Embedding is not null
        }));
    }

    /// <summary>
    /// Genera y persiste el embedding semántico del proveedor a partir de su perfil de experiencia.
    /// </summary>
    /// <remarks>
    /// El embedding es un vector de 1536 dimensiones generado por OpenAI (text-embedding-3-small).
    /// Se construye con las descripciones de experiencia y las palabras clave de búsqueda.
    /// Excluye nombre, capacidad financiera y códigos UNSPSC porque se evalúan por separado.
    /// Este vector se usa para calcular la similitud coseno contra los procesos de SECOP II.
    ///
    /// **Cuándo ejecutar:** una vez al registrar el proveedor y cada vez que se actualice su perfil de experiencia.
    /// También debe ejecutarse para cada proveedor después de cambiar la representación semántica;
    /// los embeddings existentes no se regeneran automáticamente.
    /// Sin embedding, el proveedor no participa en la evaluación de procesos.
    /// </remarks>
    /// <param name="id">ID del proveedor.</param>
    [HttpPost("{id}/embedding")]
    public async Task<IActionResult> GenerarEmbedding(Guid id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new GenerarEmbeddingProveedorCommand(id), ct);
        return ok ? Ok(new { Mensaje = "Embedding generado correctamente." }) : NotFound();
    }

    /// <summary>
    /// Recalcula las evaluaciones de todos los procesos activos para un proveedor específico.
    /// </summary>
    /// <remarks>
    /// Útil cuando:
    /// - Se actualiza el perfil o embedding del proveedor.
    /// - Se cambia la política de evaluación.
    /// - Se quiere forzar un recálculo sin esperar al worker.
    ///
    /// El proceso obtiene procesos activos, calcula similitud coseno y, si es ≥ 0.40,
    /// persiste relevancia, elegibilidad, accionabilidad y razones.
    /// </remarks>
    /// <param name="id">ID del proveedor a recalcular.</param>
    [HttpPost("{id}/recalcular")]
    public async Task<IActionResult> Recalcular(Guid id, CancellationToken ct)
    {
        var total = await _mediator.Send(new RecalcularPuntajesCommand(id), ct);
        return Ok(new { PuntajesRecalculados = total });
    }
}
