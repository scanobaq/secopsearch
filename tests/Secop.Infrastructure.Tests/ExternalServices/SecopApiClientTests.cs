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
    public async Task ObtenerProcesosRecientes_BothExactAndClass_BuildsCombinedClause()
    {
        var (client, uris) = CrearClienteConCaptura();

        await client.ObtenerProcesosRecientesAsync(
            Desde,
            codigosUnspsc: ["80101500"],
            codigosClase: ["431110"]);

        var query = Uri.UnescapeDataString(uris[0].Query);
        query.Should().Contain("IN('V1.80101500')");
        query.Should().Contain("LIKE 'V1.431110%'");
        query.Should().Contain(" OR ");
        // Both predicates wrapped in outer parens
        query.Should().MatchRegex(@"\(codigo_principal.*OR.*codigo_principal");
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
    // SPEC-08: categorias_adicionales included in WHERE clause
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ObtenerProcesosRecientes_ExactCodes_AlsoMatchesCategoriasAdicionales()
    {
        var (client, uris) = CrearClienteConCaptura();

        await client.ObtenerProcesosRecientesAsync(
            Desde, codigosUnspsc: ["80101500"]);

        var query = Uri.UnescapeDataString(uris[0].Query);
        query.Should().Contain("categorias_adicionales LIKE '%V1.80101500%'");
        query.Should().Contain(" OR ");
    }

    [Fact]
    public async Task ObtenerProcesosRecientes_ClassCodes_AlsoMatchesCategoriasAdicionales()
    {
        var (client, uris) = CrearClienteConCaptura();

        await client.ObtenerProcesosRecientesAsync(
            Desde, codigosClase: ["801015"]);

        var query = Uri.UnescapeDataString(uris[0].Query);
        query.Should().Contain("categorias_adicionales LIKE '%V1.801015%'");
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
}
