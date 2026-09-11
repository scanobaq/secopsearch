using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Secop.Application.DTOs;
using Secop.Application.Interfaces;
using Secop.Application.UseCases.Procesos.BuscarProcesosCompatibles;
using Secop.Application.UseCases.Procesos.ObtenerDetalleProceso;
using Secop.Application.UseCases.Puntajes.CalcularPuntaje;
using Secop.Application.UseCases.Puntajes.RecalcularPuntajes;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Application.Tests.UseCases;

public class EvaluacionProcesosHandlersTests
{
    private static readonly float[] Embedding = [0.5f];

    private static Proveedor CrearProveedor(List<string>? codigosUnspsc = null)
    {
        var proveedor = new Proveedor(
            "Proveedor",
            "900100200",
            DateTime.UtcNow.AddYears(1),
            1_000_000m,
            codigosUnspsc ?? [],
            []);
        proveedor.AsignarEmbedding(Embedding);
        return proveedor;
    }

    private static Proceso CrearProceso(
        string? codigoPrincipalCategoria = null,
        List<string>? categoriasAdicionales = null)
    {
        var proceso = new Proceso(
            "PROC-001",
            "Título",
            "Objeto",
            1_000_000m,
            DateTime.UtcNow.AddDays(10),
            DateTime.UtcNow.AddDays(-1),
            ModalidadContrato.LicitacionPublica,
            EstadoProceso.Activo,
            "Entidad",
            "900999888",
            "Bogotá",
            "https://secop.gov.co",
            categoriasAdicionales: categoriasAdicionales,
            codigoPrincipalCategoria: codigoPrincipalCategoria);
        proceso.AsignarEmbedding(Embedding);
        return proceso;
    }

    private static Puntaje CrearEvaluacion(
        Proveedor proveedor,
        EstadoElegibilidad elegibilidad = EstadoElegibilidad.RequiresReview,
        EstadoAccionabilidad accionabilidad = EstadoAccionabilidad.UnknownDate,
        float relevancia = 50f)
    {
        var recomendacion = elegibilidad != EstadoElegibilidad.Ineligible &&
                            accionabilidad != EstadoAccionabilidad.InsufficientTime
            ? RecomendacionAutomatica.Analyze
            : (RecomendacionAutomatica?)null;

        return new Puntaje(
            "PROC-001",
            proveedor.Id,
            relevancia,
            elegibilidad,
            accionabilidad,
            recomendacion,
            ["Requiere revisión"],
            DateTime.UtcNow);
    }

    [Theory]
    [InlineData(0.399999f, 0)]
    [InlineData(0.40f, 0)]
    [InlineData(0.45f, 1)]
    [InlineData(0.50f, 1)]
    public async Task Recalcular_AplicaMismoUmbralInclusivo(float similitud, int esperados)
    {
        var procesos = new Mock<IProcesoRepository>();
        var proveedores = new Mock<IProveedorRepository>();
        var puntajes = new Mock<IPuntajeRepository>();
        var embedding = new Mock<IEmbeddingService>();
        var scoring = new Mock<IScoringService>();
        var proceso = CrearProceso();
        var proveedor = CrearProveedor();
        var evaluacion = CrearEvaluacion(proveedor);
        procesos.Setup(r => r.ObtenerActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proceso]);
        proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        embedding.Setup(e => e.CalcularSimilitudAsync(Embedding, Embedding))
            .ReturnsAsync(similitud);
        scoring.Setup(s => s.CalcularAsync(proveedor, proceso, similitud))
            .ReturnsAsync(evaluacion);
        var handler = new RecalcularPuntajesHandler(
            procesos.Object,
            proveedores.Object,
            puntajes.Object,
            embedding.Object,
            scoring.Object,
            NullLogger<RecalcularPuntajesHandler>.Instance);

        var total = await handler.Handle(new RecalcularPuntajesCommand(), default);

        total.Should().Be(esperados);
        puntajes.Verify(
            r => r.GuardarAsync(evaluacion, It.IsAny<CancellationToken>()),
            esperados == 1 ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task CalcularPuntaje_YRecalcular_RechazanBorderlineSinEvidenciaYSinPersistir()
    {
        var procesos = new Mock<IProcesoRepository>();
        var proveedores = new Mock<IProveedorRepository>();
        var puntajes = new Mock<IPuntajeRepository>();
        var embedding = new Mock<IEmbeddingService>();
        var scoring = new Mock<IScoringService>();
        var proceso = CrearProceso(codigoPrincipalCategoria: "80101600");
        var proveedor = CrearProveedor(["80101599"]);
        var evaluacion = CrearEvaluacion(proveedor);
        procesos.Setup(r => r.ObtenerPorIdAsync(proceso.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proceso);
        procesos.Setup(r => r.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([proceso]);
        proveedores.Setup(r => r.ObtenerPorIdAsync(proveedor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proveedor);
        proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([proveedor]);
        embedding.Setup(e => e.CalcularSimilitudAsync(Embedding, Embedding)).ReturnsAsync(0.40f);
        scoring.Setup(s => s.CalcularAsync(proveedor, proceso, 0.40f)).ReturnsAsync(evaluacion);

        var adHoc = new CalcularPuntajeHandler(
            procesos.Object, proveedores.Object, puntajes.Object, embedding.Object, scoring.Object);
        var recalcular = new RecalcularPuntajesHandler(
            procesos.Object, proveedores.Object, puntajes.Object, embedding.Object, scoring.Object,
            NullLogger<RecalcularPuntajesHandler>.Instance);

        var resultadoAdHoc = await adHoc.Handle(new CalcularPuntajeCommand(proceso.Id, proveedor.Id), default);
        var totalRecalculado = await recalcular.Handle(new RecalcularPuntajesCommand(), default);

        resultadoAdHoc.Should().BeNull();
        totalRecalculado.Should().Be(0);
        puntajes.Verify(r => r.GuardarAsync(It.IsAny<Puntaje>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task CalcularPuntaje_YRecalcular_AdmitenCoincidenciaAdicionalParaProcesoLegacy()
    {
        var procesos = new Mock<IProcesoRepository>();
        var proveedores = new Mock<IProveedorRepository>();
        var puntajes = new Mock<IPuntajeRepository>();
        var embedding = new Mock<IEmbeddingService>();
        var scoring = new Mock<IScoringService>();
        var proceso = CrearProceso(categoriasAdicionales: ["80101500"]);
        var proveedor = CrearProveedor(["80101599"]);
        var evaluacion = CrearEvaluacion(proveedor);
        procesos.Setup(r => r.ObtenerPorIdAsync(proceso.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proceso);
        procesos.Setup(r => r.ObtenerActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([proceso]);
        proveedores.Setup(r => r.ObtenerPorIdAsync(proveedor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proveedor);
        proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([proveedor]);
        embedding.Setup(e => e.CalcularSimilitudAsync(Embedding, Embedding)).ReturnsAsync(0.40f);
        scoring.Setup(s => s.CalcularAsync(proveedor, proceso, 0.40f)).ReturnsAsync(evaluacion);

        var adHoc = new CalcularPuntajeHandler(
            procesos.Object, proveedores.Object, puntajes.Object, embedding.Object, scoring.Object);
        var recalcular = new RecalcularPuntajesHandler(
            procesos.Object, proveedores.Object, puntajes.Object, embedding.Object, scoring.Object,
            NullLogger<RecalcularPuntajesHandler>.Instance);

        var resultadoAdHoc = await adHoc.Handle(new CalcularPuntajeCommand(proceso.Id, proveedor.Id), default);
        var totalRecalculado = await recalcular.Handle(new RecalcularPuntajesCommand(), default);

        resultadoAdHoc.Should().NotBeNull();
        totalRecalculado.Should().Be(1);
        puntajes.Verify(r => r.GuardarAsync(evaluacion, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(39.9999f, 0)]
    [InlineData(40f, 1)]
    [InlineData(45f, 1)]
    [InlineData(50f, 1)]
    public async Task Buscar_AplicaMismoUmbralInclusivo(float relevancia, int esperados)
    {
        var proveedores = new Mock<IProveedorRepository>();
        var procesos = new Mock<IProcesoRepository>();
        var puntajes = new Mock<IPuntajeRepository>();
        var proveedor = CrearProveedor();
        var proceso = CrearProceso();
        var evaluacion = CrearEvaluacion(proveedor, relevancia: relevancia);
        proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        puntajes.Setup(r => r.ObtenerPorProveedorAsync(proveedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([evaluacion]);
        procesos.Setup(r => r.ObtenerPorIdAsync(proceso.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proceso);
        var handler = new BuscarProcesosHandler(
            proveedores.Object,
            procesos.Object,
            puntajes.Object);

        var resultado = await handler.Handle(new BuscarProcesosQuery(), default);

        resultado.Should().HaveCount(esperados);
    }

    [Fact]
    public async Task Buscar_UsaEvaluacionPersistidaYExponeDimensiones()
    {
        var proveedores = new Mock<IProveedorRepository>();
        var procesos = new Mock<IProcesoRepository>();
        var puntajes = new Mock<IPuntajeRepository>();
        var proveedor = CrearProveedor();
        var proceso = CrearProceso();
        var evaluacion = CrearEvaluacion(proveedor);
        proveedores.Setup(r => r.ObtenerTodosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([proveedor]);
        puntajes.Setup(r => r.ObtenerPorProveedorAsync(proveedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([evaluacion]);
        procesos.Setup(r => r.ObtenerPorIdAsync(proceso.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proceso);
        var handler = new BuscarProcesosHandler(
            proveedores.Object,
            procesos.Object,
            puntajes.Object);

        var resultado = await handler.Handle(new BuscarProcesosQuery(), default);

        resultado.Should().ContainSingle();
        resultado[0].RelevanciaPorcentaje.Should().Be(50f);
        resultado[0].Elegibilidad.Should().Be(EstadoElegibilidad.RequiresReview);
        resultado[0].Accionabilidad.Should().Be(EstadoAccionabilidad.UnknownDate);
        resultado[0].RecomendacionAutomatica.Should().Be(RecomendacionAutomatica.Analyze);
    }

    [Fact]
    public async Task Detalle_ExponeEvaluacionPersistidaIneligibleSinRecalcular()
    {
        var proveedores = new Mock<IProveedorRepository>();
        var procesos = new Mock<IProcesoRepository>();
        var puntajes = new Mock<IPuntajeRepository>();
        var proveedor = CrearProveedor();
        var proceso = CrearProceso();
        var evaluacion = CrearEvaluacion(
            proveedor,
            EstadoElegibilidad.Ineligible,
            EstadoAccionabilidad.Actionable);
        procesos.Setup(r => r.ObtenerPorIdAsync(proceso.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proceso);
        puntajes.Setup(r => r.ObtenerAsync(proceso.Id, proveedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluacion);
        proveedores.Setup(r => r.ObtenerPorIdAsync(proveedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proveedor);
        var handler = new ObtenerDetalleHandler(
            procesos.Object,
            proveedores.Object,
            puntajes.Object);

        var resultado = await handler.Handle(
            new ObtenerDetalleQuery(proceso.Id, proveedor.Id),
            default);

        resultado.Should().NotBeNull();
        resultado!.Elegibilidad.Should().Be(EstadoElegibilidad.Ineligible);
        resultado.EsAlertable.Should().BeFalse();
    }

    [Theory]
    [InlineData(39.9999f, false)]
    [InlineData(40f, true)]
    [InlineData(45f, true)]
    [InlineData(50f, true)]
    public async Task Detalle_AplicaMismoUmbralInclusivo(float relevancia, bool esperado)
    {
        var proveedores = new Mock<IProveedorRepository>();
        var procesos = new Mock<IProcesoRepository>();
        var puntajes = new Mock<IPuntajeRepository>();
        var proveedor = CrearProveedor();
        var proceso = CrearProceso();
        var evaluacion = CrearEvaluacion(proveedor, relevancia: relevancia);
        procesos.Setup(r => r.ObtenerPorIdAsync(proceso.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proceso);
        puntajes.Setup(r => r.ObtenerAsync(proceso.Id, proveedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluacion);
        proveedores.Setup(r => r.ObtenerPorIdAsync(proveedor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proveedor);
        var handler = new ObtenerDetalleHandler(
            procesos.Object,
            proveedores.Object,
            puntajes.Object);

        var resultado = await handler.Handle(
            new ObtenerDetalleQuery(proceso.Id, proveedor.Id),
            default);

        if (esperado)
            resultado.Should().NotBeNull();
        else
            resultado.Should().BeNull();
    }

    [Fact]
    public void PuntajeDto_NoExponePuntajeTotalNiComponentesHeredados()
    {
        var propiedades = typeof(PuntajeDto).GetProperties().Select(p => p.Name).ToList();

        propiedades.Should().Contain([
            nameof(PuntajeDto.RelevanciaPorcentaje),
            nameof(PuntajeDto.Elegibilidad),
            nameof(PuntajeDto.Accionabilidad),
            nameof(PuntajeDto.Razones)]);
        propiedades.Should().NotContain([
            "PuntajeTotal",
            "PuntajeTiempo",
            "PuntajeCompetencia",
            "PuntajeEntidad"]);
    }
}
