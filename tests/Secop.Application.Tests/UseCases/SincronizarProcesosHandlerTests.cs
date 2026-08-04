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
        float[]? embedding = null,
        string nombre = "Test S.A.")
    {
        var p = new Proveedor(
            nombre: nombre,
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

    private void ConfigurarFuentes(
        IReadOnlyList<SecopProcesoDto> dtosUnspsc,
        IReadOnlyList<SecopProcesoDto> dtosGenerales)
    {
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.Is<IEnumerable<string>?>(codigos => codigos != null),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtosUnspsc.ToList());
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtosGenerales.ToList());
    }

    private void ConfigurarProcesamiento(string procesoId, float[] embedding)
    {
        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embedding, embedding))
            .ReturnsAsync(0.8f);
    }

    private static SecopProcesoDto CrearDto(string id) => CrearDtoAbierto(id);

    /// <summary>
    /// Crea un DTO que satisface EstaAbiertoParaAplicar() (EstadoApertura/Estado/Fase reales),
    /// necesario para ejercitar el pipeline completo en pruebas de filtros y mapeo.
    /// </summary>
    private static SecopProcesoDto CrearDtoAbierto(
        string id,
        string? modalidad = null,
        string? nombreEntidad = "Entidad",
        string? objeto = "Objeto",
        string? codigoPrincipalCategoria = "V1.80101500") =>
        new()
        {
            Id = id,
            Titulo = "T-" + id,
            NombreEntidad = nombreEntidad,
            Objeto = objeto,
            Modalidad = modalidad,
            EstadoApertura = "Abierto",
            Estado = "Publicado",
            Fase = "Presentación de oferta",
            CodigoPrincipalCategoria = codigoPrincipalCategoria
        };

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
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = CrearHandler();
        await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        _secopApi.Verify();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyword fetch skipped when PalabrasClave is empty
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SinPalabrasClave_NingunProcesoKeywordOnly()
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
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(0);
        _puntajes.Verify(r => r.GuardarAsync(
            It.Is<Puntaje>(p => p.Advertencias.Contains(AdvertenciasPuntaje.EncontradoPorTexto)),
            It.IsAny<CancellationToken>()), Times.Never);
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
        var dto = CrearDtoAbierto(procesoId, objeto: "Licencia de software empresarial");

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);

        // Llamada UNSPSC: no encuentra nada → idsUnspsc vacío
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.Is<IEnumerable<string>?>(c => c != null),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Llamada general (sin filtro UNSPSC): retorna el proceso con "software" en Objeto
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
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
        var dto = CrearDtoAbierto(procesoId, objeto: "Licencia de software empresarial");

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);

        // Ambas llamadas retornan el mismo proceso — UNSPSC lo captura primero
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
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

        // Llamada general también retorna el mismo proceso
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .ReturnsAsync(0.3f); // below threshold → no scoring

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(1);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Provider-specific matching by UNSPSC or keyword
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("V1.12345678")]
    [InlineData("V112345678")]
    [InlineData("12345678")]
    public async Task Handle_UnspscExactMatchWithoutKeyword_AssignsOnlyMatchingProvider(string categoria)
    {
        const string procesoId = "PROC-UNSPSC-EXACT";
        var embedding = new float[] { 0.5f };
        var proveedorCorrecto = CrearProveedor(
            codigosUnspsc: ["12345678"], palabrasClave: ["logística"],
            embedding: embedding, nombre: "Proveedor correcto");
        var otroProveedor = CrearProveedor(
            codigosUnspsc: ["87654321"], palabrasClave: ["publicidad"],
            embedding: embedding, nombre: "Otro proveedor");
        var dto = CrearDtoAbierto(
            procesoId,
            objeto: "Suministro de elementos médicos",
            codigoPrincipalCategoria: categoria);

        ConfigurarFuentes([dto], []);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedorCorrecto, otroProveedor]);
        ConfigurarProcesamiento(procesoId, embedding);
        _scoring.Setup(s => s.CalcularAsync(proveedorCorrecto, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(CrearPuntaje(procesoId, proveedorCorrecto.Id));

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(1);
        _scoring.Verify(s => s.CalcularAsync(proveedorCorrecto, It.IsAny<Proceso>(), 0.8f), Times.Once);
        _scoring.Verify(s => s.CalcularAsync(otroProveedor, It.IsAny<Proceso>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnspscSixDigitClassMatch_AssignsProvider()
    {
        const string procesoId = "PROC-UNSPSC-CLASS";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(
            codigosUnspsc: ["12345678"],
            palabrasClave: ["logística"],
            embedding: embedding);
        var dto = CrearDtoAbierto(
            procesoId,
            objeto: "Suministro de elementos médicos",
            codigoPrincipalCategoria: "V1.12345699");

        ConfigurarFuentes([dto], []);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .ReturnsAsync(0.3f);

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(1);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("V1.85121600")]
    [InlineData(null)]
    [InlineData("codigo-invalido")]
    public async Task Handle_UnspscWithoutProviderCodeOrKeyword_DoesNotAssignProvider(string? categoria)
    {
        const string procesoId = "PROC-MEDICO-001";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(
            codigosUnspsc: ["80111500"],
            palabrasClave: ["logística deportiva", "eventos empresariales"],
            embedding: embedding);
        var dto = CrearDtoAbierto(
            procesoId,
            nombreEntidad: "ESE Tangua",
            objeto: "CONTRATO DE PRESTACIÓN DE SERVICIOS PROFESIONALES COMO MÉDICO GENERAL PARA ROTACIÓN EN EL SERVICIO DE URGENCIAS",
            codigoPrincipalCategoria: categoria);

        ConfigurarFuentes([dto], []);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);

        var handler = CrearHandler();
        await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never);
        _puntajes.Verify(r => r.GuardarAsync(It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_KeywordOnly_AssignsOnlyProviderWithMatchingKeyword()
    {
        const string procesoId = "PROC-KEYWORD-PROVIDER";
        var embedding = new float[] { 0.5f };
        var proveedorKeyword = CrearProveedor(
            palabrasClave: ["software"], embedding: embedding, nombre: "Proveedor keyword");
        var proveedorSinKeywords = CrearProveedor(
            codigosUnspsc: ["43211500"], palabrasClave: [],
            embedding: embedding, nombre: "Proveedor sin keywords");
        var dto = CrearDtoAbierto(
            procesoId,
            objeto: "Licenciamiento de software",
            codigoPrincipalCategoria: "V1.99999999");

        ConfigurarFuentes([], [dto]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedorKeyword, proveedorSinKeywords]);
        ConfigurarProcesamiento(procesoId, embedding);
        _scoring.Setup(s => s.CalcularAsync(proveedorKeyword, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(CrearPuntaje(procesoId, proveedorKeyword.Id));

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(1);
        _scoring.Verify(s => s.CalcularAsync(proveedorKeyword, It.IsAny<Proceso>(), 0.8f), Times.Once);
        _scoring.Verify(s => s.CalcularAsync(proveedorSinKeywords, It.IsAny<Proceso>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameProcesoInBothSources_MergesEligibleProvidersAndProcessesOnce()
    {
        const string procesoId = "PROC-MERGED-SOURCES";
        var embedding = new float[] { 0.5f };
        var proveedorUnspsc = CrearProveedor(
            codigosUnspsc: ["12345678"], palabrasClave: ["logística"],
            embedding: embedding, nombre: "Proveedor UNSPSC");
        var proveedorKeyword = CrearProveedor(
            codigosUnspsc: ["87654321"], palabrasClave: ["software"],
            embedding: embedding, nombre: "Proveedor keyword");
        var dtoUnspsc = CrearDtoAbierto(
            procesoId,
            objeto: "Suministro de elementos médicos",
            codigoPrincipalCategoria: "V1.12345678");
        var dtoKeyword = CrearDtoAbierto(
            procesoId,
            objeto: "Suministro de software",
            codigoPrincipalCategoria: "V1.12345678");

        ConfigurarFuentes([dtoUnspsc], [dtoKeyword]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedorUnspsc, proveedorKeyword]);
        ConfigurarProcesamiento(procesoId, embedding);
        _scoring.Setup(s => s.CalcularAsync(proveedorUnspsc, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(CrearPuntaje(procesoId, proveedorUnspsc.Id));
        _scoring.Setup(s => s.CalcularAsync(proveedorKeyword, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(CrearPuntaje(procesoId, proveedorKeyword.Id));

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(1);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Once);
        _embedding.Verify(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _scoring.Verify(s => s.CalcularAsync(proveedorUnspsc, It.IsAny<Proceso>(), 0.8f), Times.Once);
        _scoring.Verify(s => s.CalcularAsync(proveedorKeyword, It.IsAny<Proceso>(), 0.8f), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Regla de negocio simplificada: RegimenEspecial y Rfi se descartan siempre,
    // sin excepción de "con ofertas" y sin toggle configurable.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RegimenEspecial_SiempreDescartado()
    {
        const string procesoId = "PROC-PUBLICITARIO-001";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(palabrasClave: null, embedding: embedding);
        var dto = CrearDtoAbierto(procesoId, modalidad: "Régimen Especial");

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(0);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RegimenEspecialConOfertas_TambienDescartado()
    {
        const string procesoId = "PROC-CONOFERTAS-001";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(palabrasClave: null, embedding: embedding);
        var dto = CrearDtoAbierto(procesoId, modalidad: "Régimen Especial (con ofertas)");

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(0);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Rfi_SiempreDescartado()
    {
        const string procesoId = "PROC-RFI-001";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(palabrasClave: null, embedding: embedding);
        var dto = CrearDtoAbierto(procesoId, modalidad: "Solicitud de información a los Proveedores");

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(0);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-04: Entity name pattern flags without discarding
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EntidadConPatronRegimenEspecial_AgregaAdvertenciaSinDescartar()
    {
        const string procesoId = "PROC-UNIV-001";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(palabrasClave: null, embedding: embedding);
        var dto = CrearDtoAbierto(procesoId, modalidad: "Licitación Pública", nombreEntidad: "UNIVERSIDAD NACIONAL DE COLOMBIA");

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);
        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .ReturnsAsync(0.8f);

        var puntaje = CrearPuntaje(procesoId, proveedor.Id);
        Puntaje? puntajeCapturado = null;
        _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(puntaje);
        _puntajes.Setup(r => r.GuardarAsync(It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()))
            .Callback<Puntaje, CancellationToken>((p, _) => puntajeCapturado = p);

        var handler = CrearHandler();
        var result = await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        result.Should().Be(1);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Once);
        puntajeCapturado.Should().NotBeNull();
        puntajeCapturado!.Advertencias.Should().Contain(AdvertenciasPuntaje.PosibleRegimenEspecial);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SPEC-01/02/05/08/09: New field mapping onto Proceso
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MapeaCamposNuevosDesdeDto()
    {
        const string procesoId = "PROC-MAPEO-001";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(palabrasClave: null, embedding: embedding);
        var dto = CrearDtoAbierto(procesoId, modalidad: "Licitación Pública");
        dto.CategoriasAdicionales = "V1.80101500,V1.43211503";
        dto.TipoContrato = "Prestación de servicios";
        dto.NombreProveedorAdjudicado = "Proveedor Ganador";
        dto.ValorTotalAdjudicacion = "500000";
        dto.FechaAdjudicacion = "2026-05-01";
        dto.Adjudicado = "Si";

        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);
        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .ReturnsAsync(0.3f);

        Proceso? procesoGuardado = null;
        _procesos.Setup(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()))
            .Callback<Proceso, CancellationToken>((p, _) => procesoGuardado = p);

        var handler = CrearHandler();
        await handler.Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        procesoGuardado.Should().NotBeNull();
        procesoGuardado!.CategoriasAdicionales.Should().BeEquivalentTo(["80101500", "43211503"]);
        procesoGuardado.TipoContrato.Should().Be("Prestación de servicios");
        procesoGuardado.AdjudicadoA.Should().Be("Proveedor Ganador");
        procesoGuardado.Estado.Should().Be(EstadoProceso.Adjudicado);
        procesoGuardado.Clasificacion.Should().Be(ClasificacionRegimen.Ley80);
    }
}
