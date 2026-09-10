using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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

    private SincronizarProcesosHandler CrearHandler(
        ILogger<SincronizarProcesosHandler>? logger = null) =>
        new(_secopApi.Object, _procesos.Object, _proveedores.Object,
            _puntajes.Object, _embedding.Object, _scoring.Object,
            _alertas.Object, logger ?? NullLogger<SincronizarProcesosHandler>.Instance);

    private static object? GetLogProperty(
        Mock<ILogger<SincronizarProcesosHandler>> logger,
        string propertyName) =>
        logger.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Select(invocation => invocation.Arguments[2])
            .OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .SelectMany(state => state)
            .Single(property => property.Key == propertyName)
            .Value;

    private static string GetMaxSimilaritiesByProcess(
        Mock<ILogger<SincronizarProcesosHandler>> logger) =>
        GetLogProperty(logger, "MaxSimilaritiesByProcess")!.ToString()!;

    private static string GetRenderedCompletionLog(
        Mock<ILogger<SincronizarProcesosHandler>> logger) =>
        logger.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Select(invocation => invocation.Arguments[2]?.ToString())
            .Single(message => message?.StartsWith("Sincronización completada.", StringComparison.Ordinal) == true)!;

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

    private static Puntaje CrearPuntaje(
        string procesoId,
        Guid proveedorId,
        float relevancia = 75f,
        EstadoElegibilidad elegibilidad = EstadoElegibilidad.Eligible,
        EstadoAccionabilidad accionabilidad = EstadoAccionabilidad.Actionable)
    {
        var recomendacion = elegibilidad != EstadoElegibilidad.Ineligible &&
                            accionabilidad != EstadoAccionabilidad.InsufficientTime
            ? RecomendacionAutomatica.Analyze
            : (RecomendacionAutomatica?)null;

        return new Puntaje(
            procesoId,
            proveedorId,
            relevancia,
            elegibilidad,
            accionabilidad,
            recomendacion,
            [],
            DateTime.UtcNow);
    }

    private void ConfigurarFuenteGeneral(IReadOnlyList<SecopProcesoDto> dtos)
    {
        _secopApi
            .Setup(c => c.ObtenerProcesosRecientesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos.ToList());
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

    private void ConfigurarProcesamiento(string procesoId, float[] embedding, float similitud)
    {
        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embedding, embedding))
            .ReturnsAsync(similitud);
    }

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
            Presupuesto = "60000000",
            CodigoPrincipalCategoria = codigoPrincipalCategoria
        };

    [Theory]
    [InlineData(0.399999f, false)]
    [InlineData(0.40f, true)]
    [InlineData(0.45f, true)]
    [InlineData(0.50f, true)]
    public async Task Handle_AplicaUmbralSemanticoInclusivo(float similitud, bool debeCalcularPuntaje)
    {
        const string procesoId = "PROC-SEMANTIC-BOUNDARY";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(
            codigosUnspsc: ["80101500"],
            palabrasClave: ["logística"],
            embedding: embedding,
            chatId: 123);
        var dto = CrearDtoAbierto(
            procesoId,
            objeto: "Suministro de equipos médicos",
            codigoPrincipalCategoria: "V1.85121600");

        ConfigurarFuenteGeneral([dto]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        ConfigurarProcesamiento(procesoId, embedding, similitud);
        _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), similitud))
            .ReturnsAsync(CrearPuntaje(procesoId, proveedor.Id));
        var logger = new Mock<ILogger<SincronizarProcesosHandler>>();

        var nuevos = await CrearHandler(logger.Object).Handle(
            new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)),
            default);

        nuevos.Should().Be(debeCalcularPuntaje ? 1 : 0);
        GetMaxSimilaritiesByProcess(logger).Should().Be(
            $"{procesoId}={similitud.ToString("F4", CultureInfo.InvariantCulture)}");
        _embedding.Verify(
            e => e.CalcularSimilitudAsync(embedding, embedding),
            Times.Once());
        _procesos.Verify(
            r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()),
            debeCalcularPuntaje ? Times.Once() : Times.Never());
        _scoring.Verify(
            s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), similitud),
            debeCalcularPuntaje ? Times.Once() : Times.Never());
        _alertas.Verify(
            a => a.EnviarAlertaProcesoAsync(
                123,
                It.IsAny<Puntaje>(),
                It.IsAny<Proceso>(),
                proveedor,
                It.IsAny<CancellationToken>()),
            debeCalcularPuntaje ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task Handle_SummaryLog_IncludesMaximumForEachRejectedProcessInEvaluationOrder()
    {
        var embeddingX = new float[] { 1f };
        var embeddingY = new float[] { 2f };
        var embeddingZ = new float[] { 3f };
        var providerEmbeddingA = new float[] { 10f };
        var providerEmbeddingB = new float[] { 20f };
        var providerA = CrearProveedor(embedding: providerEmbeddingA, nombre: "Proveedor A");
        var providerB = CrearProveedor(embedding: providerEmbeddingB, nombre: "Proveedor B");
        var processIds = new[] { "CO1.REQ.X", "CO1.REQ.Y\r\nINJECTED", "CO1.REQ.Z" };
        ConfigurarFuenteGeneral(processIds.Select(id => CrearDtoAbierto(id)).ToList());
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([providerA, providerB]);
        _procesos.Setup(r => r.ExisteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.SetupSequence(e => e.GenerarEmbeddingAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingX)
            .ReturnsAsync(embeddingY)
            .ReturnsAsync(embeddingZ);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingX, providerEmbeddingA)).ReturnsAsync(0.3200f);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingX, providerEmbeddingB)).ReturnsAsync(0.3973f);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingY, providerEmbeddingA)).ReturnsAsync(0.3810f);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingY, providerEmbeddingB)).ReturnsAsync(0.3700f);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingZ, providerEmbeddingA)).ReturnsAsync(0.3922f);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingZ, providerEmbeddingB)).ReturnsAsync(0.3944f);
        var logger = new Mock<ILogger<SincronizarProcesosHandler>>();

        var result = await CrearHandler(logger.Object).Handle(
            new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)),
            default);

        result.Should().Be(0);
        GetMaxSimilaritiesByProcess(logger).Should().Be(
            "CO1.REQ.X=0.3973, CO1.REQ.Y INJECTED=0.3810, CO1.REQ.Z=0.3944");
        GetLogProperty(logger, "Umbral").Should().Be(0.40f);
        GetRenderedCompletionLog(logger).Should()
            .Contain("rechazados sin proveedor sobre 0.40: 3")
            .And.NotContain("rechazados sin proveedor sobre 0.4: 3");
        _embedding.Verify(e => e.GenerarEmbeddingAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _embedding.Verify(e => e.CalcularSimilitudAsync(
            It.IsAny<float[]>(), It.IsAny<float[]>()), Times.Exactly(6));
        _procesos.Verify(r => r.GuardarAsync(
            It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never());
        _scoring.Verify(s => s.CalcularAsync(
            It.IsAny<Proveedor>(), It.IsAny<Proceso>(), It.IsAny<float>()), Times.Never());
        _puntajes.Verify(r => r.GuardarAsync(
            It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()), Times.Never());
        _alertas.Verify(a => a.EnviarAlertaProcesoAsync(
            It.IsAny<long>(), It.IsAny<Puntaje>(), It.IsAny<Proceso>(), It.IsAny<Proveedor>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_SummaryLog_UsesNotAvailableWhenNoProviderHasEmbedding()
    {
        const string processId = "CO1.REQ.SIN-PROVEEDOR";
        var embedding = new float[] { 1f };
        ConfigurarFuenteGeneral([CrearDtoAbierto(processId)]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CrearProveedor()]);
        _procesos.Setup(r => r.ExisteAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        var logger = new Mock<ILogger<SincronizarProcesosHandler>>();

        var result = await CrearHandler(logger.Object).Handle(
            new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)),
            default);

        result.Should().Be(0);
        GetMaxSimilaritiesByProcess(logger).Should().Be($"{processId}=N/A");
        _embedding.Verify(e => e.GenerarEmbeddingAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
        _embedding.Verify(e => e.CalcularSimilitudAsync(
            It.IsAny<float[]>(), It.IsAny<float[]>()), Times.Never());
        _procesos.Verify(r => r.GuardarAsync(
            It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never());
        _scoring.Verify(s => s.CalcularAsync(
            It.IsAny<Proveedor>(), It.IsAny<Proceso>(), It.IsAny<float>()), Times.Never());
        _puntajes.Verify(r => r.GuardarAsync(
            It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()), Times.Never());
        _alertas.Verify(a => a.EnviarAlertaProcesoAsync(
            It.IsAny<long>(), It.IsAny<Puntaje>(), It.IsAny<Proceso>(), It.IsAny<Proveedor>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_SummaryLog_UsesStableEmptyValueWhenNoProcessesAreEvaluated()
    {
        var dto = CrearDtoAbierto("CO1.REQ.NO-EVALUADO");
        dto.Presupuesto = "59999999.99";
        ConfigurarFuenteGeneral([dto]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CrearProveedor(embedding: [1f])]);
        _procesos.Setup(r => r.ExisteAsync(dto.Id!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var logger = new Mock<ILogger<SincronizarProcesosHandler>>();

        var result = await CrearHandler(logger.Object).Handle(
            new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)),
            default);

        result.Should().Be(0);
        GetMaxSimilaritiesByProcess(logger).Should().Be("ninguna");
        _embedding.Verify(e => e.GenerarEmbeddingAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        _embedding.Verify(e => e.CalcularSimilitudAsync(
            It.IsAny<float[]>(), It.IsAny<float[]>()), Times.Never());
        _procesos.Verify(r => r.GuardarAsync(
            It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never());
        _scoring.Verify(s => s.CalcularAsync(
            It.IsAny<Proveedor>(), It.IsAny<Proceso>(), It.IsAny<float>()), Times.Never());
        _puntajes.Verify(r => r.GuardarAsync(
            It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()), Times.Never());
        _alertas.Verify(a => a.EnviarAlertaProcesoAsync(
            It.IsAny<long>(), It.IsAny<Puntaje>(), It.IsAny<Proceso>(), It.IsAny<Proveedor>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Theory]
    [InlineData(EstadoElegibilidad.Eligible, EstadoAccionabilidad.Actionable, true)]
    [InlineData(EstadoElegibilidad.RequiresReview, EstadoAccionabilidad.Actionable, true)]
    [InlineData(EstadoElegibilidad.Eligible, EstadoAccionabilidad.UnknownDate, true)]
    [InlineData(EstadoElegibilidad.Ineligible, EstadoAccionabilidad.Actionable, false)]
    [InlineData(EstadoElegibilidad.Eligible, EstadoAccionabilidad.InsufficientTime, false)]
    public async Task Handle_AlertaSoloEvaluacionesReviewablesYConTiempo(
        EstadoElegibilidad elegibilidad,
        EstadoAccionabilidad accionabilidad,
        bool debeAlertar)
    {
        const string procesoId = "PROC-ALERT-POLICY";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(chatId: 123, embedding: embedding);
        var evaluacion = CrearPuntaje(
            procesoId,
            proveedor.Id,
            elegibilidad: elegibilidad,
            accionabilidad: accionabilidad);

        ConfigurarFuenteGeneral([CrearDtoAbierto(procesoId)]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        ConfigurarProcesamiento(procesoId, embedding);
        _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(evaluacion);

        await CrearHandler().Handle(
            new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)),
            default);

        evaluacion.RecomendacionAutomatica.Should().Be(
            debeAlertar ? RecomendacionAutomatica.Analyze : null);
        _alertas.Verify(a => a.EnviarAlertaProcesoAsync(
                123, evaluacion, It.IsAny<Proceso>(), proveedor, It.IsAny<CancellationToken>()),
            debeAlertar ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task Handle_ProcesoArquitectonicoNoRelacionado_ConSimilitudSobreUmbralContinuaEvaluacion()
    {
        const string procesoId = "CO1.REQ.10810339";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(
            codigosUnspsc: ["80101500"],
            palabrasClave: ["logística", "eventos BTL"],
            embedding: embedding,
            nombre: "Logística y Eventos SAS");
        var dto = CrearDtoAbierto(
            procesoId,
            nombreEntidad: "Secretaría de Infraestructura",
            objeto: "Estudios y diseños arquitectónicos y estructurales para una sede pública");
        dto.Titulo = "Consultoría para diseño estructural";

        ConfigurarFuenteGeneral([dto]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        ConfigurarProcesamiento(procesoId, embedding, 0.40059844f);
        var puntaje = CrearPuntaje(procesoId, proveedor.Id, 40.059844f);
        _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.40059844f))
            .ReturnsAsync(puntaje);

        var nuevos = await CrearHandler().Handle(
            new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)),
            default);

        nuevos.Should().Be(1);
        _embedding.Verify(e => e.GenerarEmbeddingAsync(
            "Consultoría para diseño estructural " +
            "Estudios y diseños arquitectónicos y estructurales para una sede pública",
            It.IsAny<CancellationToken>()), Times.Once);
        _scoring.Verify(
            s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.40059844f),
            Times.Once);
        _puntajes.Verify(
            r => r.GuardarAsync(puntaje, It.IsAny<CancellationToken>()),
            Times.Once);
        _procesos.Verify(
            r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MultiplesProveedores_PersisteUnaVezYPuntuaSoloLosCalificados()
    {
        const string procesoId = "PROC-MULTIPROVEEDOR";
        var embeddingProceso = new float[] { 1f };
        var embeddingAlto = new float[] { 2f };
        var embeddingMedio = new float[] { 3f };
        var embeddingBajo = new float[] { 4f };
        var proveedorAlto = CrearProveedor(chatId: 101, embedding: embeddingAlto, nombre: "Proveedor alto");
        var proveedorMedio = CrearProveedor(chatId: 202, embedding: embeddingMedio, nombre: "Proveedor medio");
        var proveedorBajo = CrearProveedor(chatId: 303, embedding: embeddingBajo, nombre: "Proveedor bajo");
        var dto = CrearDtoAbierto(procesoId);

        ConfigurarFuenteGeneral([dto]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedorAlto, proveedorMedio, proveedorBajo]);
        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingProceso);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingProceso, embeddingAlto)).ReturnsAsync(0.70f);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingProceso, embeddingMedio)).ReturnsAsync(0.50f);
        _embedding.Setup(e => e.CalcularSimilitudAsync(embeddingProceso, embeddingBajo)).ReturnsAsync(0.39f);
        var puntajeAlto = CrearPuntaje(procesoId, proveedorAlto.Id);
        var puntajeMedio = CrearPuntaje(procesoId, proveedorMedio.Id, 50f);
        _scoring.Setup(s => s.CalcularAsync(proveedorAlto, It.IsAny<Proceso>(), 0.70f))
            .ReturnsAsync(puntajeAlto);
        _scoring.Setup(s => s.CalcularAsync(proveedorMedio, It.IsAny<Proceso>(), 0.50f))
            .ReturnsAsync(puntajeMedio);

        var nuevos = await CrearHandler().Handle(
            new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)),
            default);

        nuevos.Should().Be(1);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Once);
        _embedding.Verify(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _embedding.Verify(e => e.CalcularSimilitudAsync(embeddingProceso, It.IsAny<float[]>()), Times.Exactly(3));
        _scoring.Verify(s => s.CalcularAsync(proveedorAlto, It.IsAny<Proceso>(), 0.70f), Times.Once);
        _scoring.Verify(s => s.CalcularAsync(proveedorMedio, It.IsAny<Proceso>(), 0.50f), Times.Once);
        _scoring.Verify(s => s.CalcularAsync(proveedorBajo, It.IsAny<Proceso>(), It.IsAny<float>()), Times.Never);
        _puntajes.Verify(r => r.GuardarAsync(puntajeAlto, It.IsAny<CancellationToken>()), Times.Once);
        _puntajes.Verify(r => r.GuardarAsync(puntajeMedio, It.IsAny<CancellationToken>()), Times.Once);
        _alertas.Verify(a => a.EnviarAlertaProcesoAsync(
            101, puntajeAlto, It.IsAny<Proceso>(), proveedorAlto, It.IsAny<CancellationToken>()), Times.Once);
        _alertas.Verify(a => a.EnviarAlertaProcesoAsync(
            202, puntajeMedio, It.IsAny<Proceso>(), proveedorMedio, It.IsAny<CancellationToken>()), Times.Once);
        _alertas.Verify(a => a.EnviarAlertaProcesoAsync(
            303, It.IsAny<Puntaje>(), It.IsAny<Proceso>(), proveedorBajo, It.IsAny<CancellationToken>()), Times.Never);
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
        ConfigurarFuenteGeneral([dto]);

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
        ConfigurarFuenteGeneral([dto]);

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
        ConfigurarFuenteGeneral([dto]);

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
        ConfigurarFuenteGeneral([dto]);
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
        puntajeCapturado!.Razones.Should().Contain(RazonesEvaluacion.PosibleRegimenEspecial);
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
        ConfigurarFuenteGeneral([dto]);
        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _embedding.Setup(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _embedding.Setup(e => e.CalcularSimilitudAsync(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .ReturnsAsync(0.8f);
        _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(CrearPuntaje(procesoId, proveedor.Id));

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

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("-1", null)]
    [InlineData("no-es-numero", null)]
    [InlineData("59999999.99", null)]
    public async Task Handle_CandidatoNuevoNoElegible_NoEjecutaEfectosPosteriores(
        string? presupuesto, string? modalidad)
    {
        const string procesoId = "PROC-NO-ELEGIBLE";
        var embedding = new float[] { 0.5f };
        var proveedor = CrearProveedor(embedding: embedding);
        var dto = CrearDtoAbierto(procesoId, modalidad: modalidad);
        dto.Presupuesto = presupuesto;
        ConfigurarFuenteGeneral([dto]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([proveedor]);
        ConfigurarProcesamiento(procesoId, embedding);
        _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.8f))
            .ReturnsAsync(CrearPuntaje(procesoId, proveedor.Id));

        var nuevos = await CrearHandler().Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default);

        nuevos.Should().Be(0);
        _embedding.Verify(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never);
        _embedding.Verify(e => e.CalcularSimilitudAsync(It.IsAny<float[]>(), It.IsAny<float[]>()), Times.Never);
        _scoring.Verify(s => s.CalcularAsync(It.IsAny<Proveedor>(), It.IsAny<Proceso>(), It.IsAny<float>()), Times.Never);
        _puntajes.Verify(r => r.GuardarAsync(It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()), Times.Never);
        _alertas.Verify(a => a.EnviarAlertaProcesoAsync(It.IsAny<long>(), It.IsAny<Puntaje>(), It.IsAny<Proceso>(), It.IsAny<Proveedor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ProcesoExistenteConEntradaNoElegible_SaltaAntesDeEfectosPosteriores()
    {
        const string procesoId = "PROC-EXISTENTE";
        var proveedor = CrearProveedor(embedding: [0.5f]);
        var dto = CrearDtoAbierto(procesoId, modalidad: "Contratación Directa");
        dto.Presupuesto = "invalido";
        ConfigurarFuenteGeneral([dto]);
        _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([proveedor]);
        _procesos.Setup(r => r.ExisteAsync(procesoId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        (await CrearHandler().Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default)).Should().Be(0);
        _embedding.Verify(e => e.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _procesos.Verify(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CandidatoElegible_ConservaPresupuestoAprobado()
    {
        var culturaOriginal = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-CO");
            const string procesoId = "PROC-ELEGIBLE";
            var embedding = new float[] { 0.5f };
            var proveedor = CrearProveedor(embedding: embedding);
            var dto = CrearDtoAbierto(procesoId);
            dto.Presupuesto = "60000000.50";
            ConfigurarFuenteGeneral([dto]);
            _proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([proveedor]);
            ConfigurarProcesamiento(procesoId, embedding);
            _scoring.Setup(s => s.CalcularAsync(proveedor, It.IsAny<Proceso>(), 0.8f))
                .ReturnsAsync(CrearPuntaje(procesoId, proveedor.Id));
            Proceso? guardado = null;
            _procesos.Setup(r => r.GuardarAsync(It.IsAny<Proceso>(), It.IsAny<CancellationToken>()))
                .Callback<Proceso, CancellationToken>((proceso, _) => guardado = proceso);

            (await CrearHandler().Handle(new SincronizarProcesosCommand(DateTime.UtcNow.AddHours(-2)), default)).Should().Be(1);
            guardado!.Presupuesto.Should().Be(60_000_000.50m);
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaOriginal;
        }
    }

}
