using System.Net;
using System.Text;
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
