using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Secop.Application.DTOs;
using Secop.Application.Interfaces;
using Secop.Application.UseCases.Procesos.SincronizarProcesos;
using Secop.Domain.Constants;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Application.Tests.UseCases;

public class SincronizarProcesosHandlerTests
{
    private readonly Mock<ISecopApiClient> _secopApi = new();
    private readonly Mock<IProcesoRepository> _procesos = new();
    private readonly Mock<IProveedorRepository> _proveedores = new();
    private readonly Mock<IPuntajeRepository> _puntajes = new();
    private readonly Mock<IEmbeddingService> _embedding = new();
    private readonly Mock<IScoringService> _scoring = new();
    private readonly Mock<IAlertaService> _alertas = new();

    private SincronizarProcesosHandler CrearHandler() =>
        new(_secopApi.Object, _procesos.Object, _proveedores.Object,
            _puntajes.Object, _embedding.Object, _scoring.Object,
            _alertas.Object, NullLogger<SincronizarProcesosHandler>.Instance);

    private static Proveedor CrearProveedor(
        List<string>? codigosUnspsc = null,
        List<string>? palabrasClave = null,
        long? chatId = null,
        float[]? embedding = null)
    {
        var p = new Proveedor(
            nombre: "Test S.A.",
            nit: "900100200",
            rupVigencia: DateTime.UtcNow.AddYears(1),
            capacidadFinanciera: 10_000_000m,
            codigosUnspsc: codigosUnspsc ?? ["80101500"],
            experienciaDescripcion: ["Consultoría"],
            telegramChatId: chatId,
            palabrasClave: palabrasClave);
        if (embedding is not null)
            p.AsignarEmbedding(embedding);
        return p;
    }

    private static Proceso CrearProceso(string id = "PROC-001") =>
        new(id, "Título " + id, "Objeto", 1_000_000m,
            DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(-1),
            ModalidadContrato.LicitacionPublica, EstadoProceso.Activo,
            "Entidad", "900999888", "Bogotá", "https://secop.gov.co");

    private static Puntaje CrearPuntaje(string procesoId, Guid proveedorId) =>
        new(procesoId, proveedorId, 75f, 26f, 20f, 15f, 10f, 4f,
            EtiquetaProceso.Proponer, []);

    private static SecopProcesoDto CrearDto(string id) =>
        new() { Id = id, Titulo = "T-" + id, NombreEntidad = "Entidad" };

    // ─────────────────────────────────────────────────────────────────────────
    // CodigosClase derivation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_BuildsCodigosClase_FromProveedorUnspscCodes()
    {
        // "80101500" → clase "801015"
        var proveedor = CrearProveedor(["80101500"]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);

        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.Is<IEnumerable<string>?>(clases => clases != null && clases.Contains("801015")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Verifiable();

        var handler = CrearHandler();
        await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        _secopApi.Verify();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyword fetch skipped when PalabrasClave is empty
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SkipsKeywordFetch_WhenPalabrasClaveIsEmpty()
    {
        var proveedor = CrearProveedor(palabrasClave: null);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = CrearHandler();
        await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        _secopApi.Verify(
            c => c.ObtenerProcesosPorPalabraClaveAsync(
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyword-only proceso gets advertencia
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ProcesoFoundOnlyByKeyword_GetsEncontradoPorTextoAdvertencia()
    {
        const string procesoId = "PROC-KW-001";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var proveedor = CrearProveedor(palabrasClave: ["software"], embedding: embedding);
        var dto = CrearDto(procesoId);

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);

        // UNSPSC search returns nothing → this proceso won't be in idsUnspsc
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _secopApi
            .Setup(c => c.ObtenerProcesosPorPalabraClaveAsync(
                It.IsAny<DateTime>(), "software", It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embedding, embedding))
            .ReturnsAsync(0.8f);

        Puntaje? puntajeCapturado = null;
        var puntaje = CrearPuntaje(procesoId, proveedor.Id);
        _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(puntaje);
        _puntajes.Setup(r => r.GuardarAsync(It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()))
            .Callback<Puntaje, CancellationToken>((p, _) => puntajeCapturado = p);

        var handler = CrearHandler();
        await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        puntajeCapturado.Should().NotBeNull();
        puntajeCapturado!.Advertencias.Should()
            .Contain(AdvertenciasPuntaje.EncontradoPorTexto);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UNSPSC proceso does NOT get advertencia
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ProcesoFoundByUnspsc_DoesNotGetEncontradoPorTextoAdvertencia()
    {
        const string procesoId = "PROC-UNSPSC-001";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var proveedor = CrearProveedor(palabrasClave: ["software"], embedding: embedding);
        var dto = CrearDto(procesoId);

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);

        // UNSPSC search returns the proceso → it's in idsUnspsc
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        // keyword search also returns same ID (would be deduped)
        _secopApi
            .Setup(c => c.ObtenerProcesosPorPalabraClaveAsync(
                It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embedding, embedding))
            .ReturnsAsync(0.8f);

        Puntaje? puntajeCapturado = null;
        var puntaje = CrearPuntaje(procesoId, proveedor.Id);
        _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(puntaje);
        _puntajes.Setup(r => r.GuardarAsync(It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()))
            .Callback<Puntaje, CancellationToken>((p, _) => puntajeCapturado = p);

        var handler = CrearHandler();
        await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        puntajeCapturado.Should().NotBeNull();
        puntajeCapturado!.Advertencias.Should()
            .NotContain(AdvertenciasPuntaje.EncontradoPorTexto);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Deduplication: same ID in both sets → processed once
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SameProcesoIdInBothSets_ProcessedOnce()
    {
        const string procesoId = "PROC-DUP-001";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(palabrasClave: ["tech"], embedding: embedding);
        var dto = CrearDto(procesoId);

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);

        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        _secopApi
            .Setup(c => c.ObtenerProcesosPorPalabraClaveAsync(
                It.IsAny<DateTime>(), "tech", It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .ReturnsAsync(0.4f); // below threshold → no scoring

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(1);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
