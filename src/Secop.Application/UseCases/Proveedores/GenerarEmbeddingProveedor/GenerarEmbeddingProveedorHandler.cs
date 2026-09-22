using MediatR;
using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;
using Secop.Application.Services;

namespace Secop.Application.UseCases.Proveedores.GenerarEmbeddingProveedor;

public class GenerarEmbeddingProveedorHandler : IRequestHandler<GenerarEmbeddingProveedorCommand, bool>
{
    private readonly IProveedorRepository _proveedores;
    private readonly IEmbeddingService _embedding;
    private readonly ILogger<GenerarEmbeddingProveedorHandler> _logger;

    public GenerarEmbeddingProveedorHandler(
        IProveedorRepository proveedores,
        IEmbeddingService embedding,
        ILogger<GenerarEmbeddingProveedorHandler> logger)
    {
        _proveedores = proveedores;
        _embedding = embedding;
        _logger = logger;
    }

    public async Task<bool> Handle(GenerarEmbeddingProveedorCommand request, CancellationToken ct)
    {
        var proveedor = await _proveedores.ObtenerPorIdAsync(request.ProveedorId, ct);
        if (proveedor is null)
        {
            _logger.LogWarning("Proveedor {Id} no encontrado", request.ProveedorId);
            return false;
        }

        var textoEmbedding = ConstructorTextoSemantico.CrearParaProveedor(proveedor);

        var embedding = await _embedding.GenerarEmbeddingAsync(textoEmbedding, ct);
        proveedor.AsignarEmbedding(embedding);

        await _proveedores.ActualizarAsync(proveedor, ct);

        _logger.LogInformation("Embedding generado para proveedor {Nombre} ({Id})",
            proveedor.Nombre, proveedor.Id);

        return true;
    }
}
