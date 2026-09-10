using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Secop.Infrastructure.ExternalServices;

namespace Secop.Infrastructure.Tests.ExternalServices;

public class SecopApiClientTests
{
    private static (SecopApiClient client, List<Uri> capturedUris) CrearClienteConCaptura(
        string responseBody = "[]")
    {
        var capturedUris = new List<Uri>();
        var handler = new CapturingHandler(capturedUris, responseBody);
        var http = new HttpClient(handler);
        var client = new SecopApiClient(http, NullLogger<SecopApiClient>.Instance);
        return (client, capturedUris);
    }

    private static DateTime Desde => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string CrearPagina(string prefijo, int cantidad) =>
        JsonSerializer.Serialize(Enumerable.Range(0, cantidad).Select(i => new
        {
            id_del_proceso = $"{prefijo}-{i}"
        }));

    private static HttpResponseMessage CrearRespuesta(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    // ─────────────────────────────────────────────────────────────────────────
    // WHERE clause construction
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerProcesosRecientes_OnlyExactCodes_BuildsInClause_NoLikeOnCodigoPrincipal()
    {
        var (client, uris) = CrearClienteConCaptura();

        await client.ObtenerProcesosRecientesAsync(
            Desde, codigosUnspsc: ["80101500", "43211503"]);

        var query = Uri.UnescapeDataString(uris[0].Query);
        query.Should().Contain("IN('V1.80101500','V1.43211503')");
        query.Should().NotContain("codigo_principal_de_categoria LIKE");
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_OnlyClassCodes_BuildsLikeClause_WrappedInParens()
    {
        var (client, uris) = CrearClienteConCaptura();

        await client.ObtenerProcesosRecientesAsync(
            Desde, codigosClase: ["801015"]);

        var query = Uri.UnescapeDataString(uris[0].Query);
        query.Should().Contain("codigo_principal_de_categoria LIKE 'V1.801015%'");
        query.Should().NotContain(" IN(");
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_BothExactAndClass_BuildsSeparateBatchQueries()
    {
        var (client, uris) = CrearClienteConCaptura();

        await client.ObtenerProcesosRecientesAsync(
            Desde,
            codigosUnspsc: ["80101500"],
            codigosClase: ["431110"]);

        // Exactos y clases van en llamadas HTTP separadas (lotes distintos)
        uris.Should().HaveCount(2);
        var queries = uris.Select(u => Uri.UnescapeDataString(u.Query)).ToList();

        queries.Should().ContainSingle(q => q.Contains("IN('V1.80101500')"));
        queries.Should().ContainSingle(q => q.Contains("LIKE 'V1.431110%'"));
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_EmptyClassList_NoLikeFragment()
    {
        var (client, uris) = CrearClienteConCaptura();

        await client.ObtenerProcesosRecientesAsync(
            Desde,
            codigosUnspsc: ["80101500"],
            codigosClase: []);

        var query = Uri.UnescapeDataString(uris[0].Query);
        query.Should().NotContain("codigo_principal_de_categoria LIKE");
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_RangoFechaPublicacion_IncluyeDesdeYExcluyeHasta()
    {
        var (clientConHasta, urisConHasta) = CrearClienteConCaptura();
        var hasta = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await clientConHasta.ObtenerProcesosRecientesAsync(Desde, hasta);

        Uri.UnescapeDataString(urisConHasta.Single().Query).Split('&')[0].Should().Be(
            "?$where=fecha_de_ultima_publicaci >= '2026-01-01T00:00:00' AND " +
            "fecha_de_ultima_publicaci < '2026-01-02T00:00:00'");

        var (clientSinHasta, urisSinHasta) = CrearClienteConCaptura();

        await clientSinHasta.ObtenerProcesosRecientesAsync(Desde);

        Uri.UnescapeDataString(urisSinHasta.Single().Query).Split('&')[0].Should().Be(
            "?$where=fecha_de_ultima_publicaci >= '2026-01-01T00:00:00'");
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_ConsultaGeneral_PaginaHastaRespuestaCorta()
    {
        var uris = new List<Uri>();
        var handler = new RoutingHandler(uri =>
        {
            var query = Uri.UnescapeDataString(uri.Query);
            return query.Contains("$offset=0")
                ? CrearRespuesta(CrearPagina("pagina-1", 1000))
                : CrearRespuesta(CrearPagina("pagina-2", 2));
        }, uris);
        var client = new SecopApiClient(
            new HttpClient(handler),
            NullLogger<SecopApiClient>.Instance);
        var hasta = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var resultado = await client.ObtenerProcesosRecientesAsync(Desde, hasta);

        resultado.Should().HaveCount(1002);
        uris.Should().HaveCount(2);
        var queries = uris.Select(uri => Uri.UnescapeDataString(uri.Query)).ToList();
        queries.Should().ContainSingle(query => query.Contains("$offset=0"));
        queries.Should().ContainSingle(query => query.Contains("$offset=1000"));
        queries.Should().AllSatisfy(query =>
        {
            query.Should().Contain("$limit=1000");
            query.Should().Contain(
                "fecha_de_ultima_publicaci >= '2026-01-01T00:00:00' AND " +
                "fecha_de_ultima_publicaci < '2026-01-02T00:00:00'");
            query.Should().Contain(
                "$order=fecha_de_ultima_publicaci DESC, id_del_proceso DESC");
        });
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_MultiplesLotes_PaginaCadaLoteIndependientemente()
    {
        var uris = new List<Uri>();
        var handler = new RoutingHandler(uri =>
        {
            var query = Uri.UnescapeDataString(uri.Query);
            var prefijo = query.Contains(" IN(") ? "exactos" : "clases";
            var cantidad = query.Contains("$offset=0") ? 1000 : 1;
            return CrearRespuesta(CrearPagina($"{prefijo}-{cantidad}", cantidad));
        }, uris);
        var client = new SecopApiClient(
            new HttpClient(handler),
            NullLogger<SecopApiClient>.Instance);

        var resultado = await client.ObtenerProcesosRecientesAsync(
            Desde,
            codigosUnspsc: ["80101500"],
            codigosClase: ["431110"]);

        resultado.Should().HaveCount(2002);
        uris.Should().HaveCount(4);
        var queries = uris.Select(uri => Uri.UnescapeDataString(uri.Query)).ToList();
        queries.Should().ContainSingle(query => query.Contains(" IN(") && query.Contains("$offset=0"));
        queries.Should().ContainSingle(query => query.Contains(" IN(") && query.Contains("$offset=1000"));
        queries.Should().ContainSingle(query => query.Contains(" LIKE ") && query.Contains("$offset=0"));
        queries.Should().ContainSingle(query => query.Contains(" LIKE ") && query.Contains("$offset=1000"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bug 414: categorias_adicionales removido (predicado roto, nunca matchea
    // porque el campo real no tiene punto tras "V1"), y troceo en lotes para
    // no generar una URL gigante que el servidor rechaza con 414
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerProcesosRecientes_NingunaUrlContieneCategoriasAdicionales()
    {
        var (client, uris) = CrearClienteConCaptura();

        await client.ObtenerProcesosRecientesAsync(
            Desde,
            codigosUnspsc: ["80101500"],
            codigosClase: ["801015"]);

        uris.Should().NotBeEmpty();
        foreach (var uri in uris)
        {
            var query = Uri.UnescapeDataString(uri.Query);
            query.Should().NotContain("categorias_adicionales");
        }
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_MasDe40CodigosExactos_HaceMultiplesLlamadas()
    {
        var (client, uris) = CrearClienteConCaptura();

        var codigos = Enumerable.Range(1, 100).Select(i => $"801{i:D5}").ToList();

        await client.ObtenerProcesosRecientesAsync(Desde, codigosUnspsc: codigos);

        // 100 códigos en lotes de 40 -> 3 llamadas (40 + 40 + 20)
        uris.Should().HaveCount(3);
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_ResultadosDuplicadosEntreLotes_SeDeduplicanPorId()
    {
        var handler = new SequencedHandler(
        [
            """[{"id_del_proceso":"P1"},{"id_del_proceso":"P2"}]""",
            """[{"id_del_proceso":"P2"},{"id_del_proceso":"P3"}]""",
        ]);
        var http = new HttpClient(handler);
        var client = new SecopApiClient(http, NullLogger<SecopApiClient>.Instance);

        var codigos = Enumerable.Range(1, 80).Select(i => $"801{i:D5}").ToList();

        var resultado = await client.ObtenerProcesosRecientesAsync(Desde, codigosUnspsc: codigos);

        resultado.Should().HaveCount(3);
        resultado.Select(p => p.Id).Should().BeEquivalentTo(["P1", "P2", "P3"]);
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_ErrorSecop_PropagaLaExcepcion()
    {
        var handler = new RoutingHandler(
            _ => CrearRespuesta("{}", HttpStatusCode.BadGateway));
        var client = new SecopApiClient(
            new HttpClient(handler),
            NullLogger<SecopApiClient>.Instance);

        Func<Task> act = () => client.ObtenerProcesosRecientesAsync(Desde);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Inner handler
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly List<Uri> _uris;
        private readonly string _body;

        public CapturingHandler(List<Uri> uris, string body)
        {
            _uris = uris;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _uris.Add(request.RequestUri!);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Func<Uri, HttpResponseMessage> _responseFactory;
        private readonly List<Uri>? _uris;

        public RoutingHandler(
            Func<Uri, HttpResponseMessage> responseFactory,
            List<Uri>? uris = null)
        {
            _responseFactory = responseFactory;
            _uris = uris;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (_uris is not null)
            {
                lock (_uris)
                    _uris.Add(uri);
            }

            return Task.FromResult(_responseFactory(uri));
        }
    }

    /// <summary>
    /// Handler que devuelve un body distinto por cada llamada recibida, en el orden
    /// en que las peticiones concurrentes llegan (usa un índice thread-safe).
    /// </summary>
    private sealed class SequencedHandler : HttpMessageHandler
    {
        private readonly string[] _bodies;
        private int _index = -1;

        public SequencedHandler(string[] bodies)
        {
            _bodies = bodies;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var i = Interlocked.Increment(ref _index) % _bodies.Length;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_bodies[i], Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
