using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Secop.Application.Interfaces;
using Secop.Application.UseCases.Proveedores.GenerarEmbeddingProveedor;
using Secop.Domain.Entities;

namespace Secop.Application.Tests.UseCases;

public class GenerarEmbeddingProveedorHandlerTests
{
    [Fact]
    public async Task Handle_RegeneraEmbeddingConTextoSemanticoDeCapacidades()
    {
        var proveedor = new Proveedor(
            nombre: "Logística y Eventos SAS",
            nit: "900123456",
            rupVigencia: DateTime.UtcNow.AddYears(1),
            capacidadFinanciera: 7_000_000_000m,
            codigosUnspsc: ["80141900"],
            experienciaDescripcion: ["Producción de eventos corporativos"],
            palabrasClave: ["logística", "eventos BTL"]);
        proveedor.AsignarEmbedding([0.1f]);

        var repositorio = new Mock<IProveedorRepository>();
        var embeddings = new Mock<IEmbeddingService>();
        var embeddingRegenerado = new[] { 0.7f, 0.8f };
        repositorio.Setup(r => r.ObtenerPorIdAsync(proveedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proveedor);
        embeddings.Setup(e => e.GenerarEmbeddingAsync(
                "Experiencia y capacidades: Producción de eventos corporativos" + Environment.NewLine +
                "Palabras clave de búsqueda: logística | eventos BTL",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingRegenerado);

        var handler = new GenerarEmbeddingProveedorHandler(
            repositorio.Object,
            embeddings.Object,
            NullLogger<GenerarEmbeddingProveedorHandler>.Instance);

        var resultado = await handler.Handle(
            new GenerarEmbeddingProveedorCommand(proveedor.Id),
            default);

        resultado.Should().BeTrue();
        proveedor.Embedding.Should().BeSameAs(embeddingRegenerado);
        repositorio.Verify(r => r.ActualizarAsync(proveedor, It.IsAny<CancellationToken>()), Times.Once);
    }
}
