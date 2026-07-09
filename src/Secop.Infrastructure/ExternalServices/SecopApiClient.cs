using System.Text.Json;
using Microsoft.Extensions.Logging;
using Secop.Application.DTOs;
using Secop.Application.Interfaces;

namespace Secop.Infrastructure.ExternalServices;

public class SecopApiClient : ISecopApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SecopApiClient> _logger;

    private const string DatasetProcesos = "p6dx-8zbt";
    private const string BaseUrl = "https://www.datos.gov.co/resource";

    // Límite de códigos UNSPSC por consulta HTTP. Con muchos códigos combinados en un
    // único $where la URL puede superar los ~8KB que soportan los front-ends nginx de
    // datos.gov.co, y el servidor responde 414 Request-URI Too Large (bug confirmado
    // en producción con 114 códigos). Trocear en lotes evita ese límite.
    private const int MaxCodigosPorLote = 40;

    public SecopApiClient(HttpClient http, ILogger<SecopApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<SecopProcesoDto>> ObtenerProcesosRecientesAsync(
        DateTime desde,
        DateTime? hasta = null,
        IEnumerable<string>? codigosUnspsc = null,
        IEnumerable<string>? codigosClase = null,
        CancellationToken ct = default)
    {
        // Bug conocido del dataset: el campo se llama "fecha_de_ultima_publicaci" (sin la 'ó')
        var fechaDesde = desde.ToString("yyyy-MM-ddTHH:mm:ss");

        var filtroFecha = hasta.HasValue
            ? $"fecha_de_ultima_publicaci > '{fechaDesde}' AND fecha_de_ultima_publicaci <= '{hasta.Value:yyyy-MM-ddTHH:mm:ss}'"
            : $"fecha_de_ultima_publicaci > '{fechaDesde}'";

        var exactos = codigosUnspsc?.Distinct().ToList() ?? [];
        var clases = codigosClase?.Distinct().ToList() ?? [];

        // Nota: NO filtramos por categorias_adicionales — ese predicado buscaba
        // '%V1.{codigo}%' con punto, pero el campo real del dataset no tiene punto
        // tras "V1" (ej. "V172101500"), así que nunca matcheaba nada. Además era el
        // mayor contribuyente al tamaño de la URL que causaba el 414.
        var predicadosPorLote = TrocearEnLotes(exactos, MaxCodigosPorLote)
            .Select(lote => $"codigo_principal_de_categoria IN({string.Join(",", lote.Select(c => $"'V1.{c}'"))})")
            .Concat(TrocearEnLotes(clases, MaxCodigosPorLote)
                .Select(lote => string.Join(" OR ", lote.Select(c => $"codigo_principal_de_categoria LIKE 'V1.{c}%'"))))
            .ToList();

        var order = Uri.EscapeDataString("fecha_de_ultima_publicaci DESC");

        var urls = predicadosPorLote.Count == 0
            ? [$"{BaseUrl}/{DatasetProcesos}.json?$where={Uri.EscapeDataString(filtroFecha)}&$limit=1000&$order={order}"]
            : predicadosPorLote
                .Select(predicado =>
                {
                    var whereClause = $"{filtroFecha} AND ({predicado})";
                    var where = Uri.EscapeDataString(whereClause);
                    return $"{BaseUrl}/{DatasetProcesos}.json?$where={where}&$limit=1000&$order={order}";
                })
                .ToList();

        var resultadosPorLote = await Task.WhenAll(urls.Select(url => EjecutarConsultaAsync(url, ct)));

        return resultadosPorLote
            .SelectMany(r => r)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .ToList();
    }

    private static IEnumerable<List<string>> TrocearEnLotes(List<string> items, int tamanoLote)
    {
        for (var i = 0; i < items.Count; i += tamanoLote)
            yield return items.Skip(i).Take(tamanoLote).ToList();
    }

    public async Task<List<SecopProcesoDto>> ObtenerDesiertosSinAlertaAsync(CancellationToken ct)
    {
        var where = Uri.EscapeDataString("estado_del_proceso='Desierto'");
        var order = Uri.EscapeDataString("fecha_de_ultima_publicaci DESC");
        var url = $"{BaseUrl}/{DatasetProcesos}.json?$where={where}&$limit=500&$order={order}";

        return await EjecutarConsultaAsync(url, ct);
    }

    public async Task<List<SecopProcesoDto>> ObtenerProcesosConPaginacionAsync(int limit, int offset, CancellationToken ct)
    {
        var order = Uri.EscapeDataString("fecha_de_ultima_publicaci DESC");
        var url = $"{BaseUrl}/{DatasetProcesos}.json?$limit={limit}&$offset={offset}&$order={order}";

        return await EjecutarConsultaAsync(url, ct);
    }

    private async Task<List<SecopProcesoDto>> EjecutarConsultaAsync(string url, CancellationToken ct)
    {
        try
        {
            var json = await _http.GetStringAsync(url, ct);
            var result = JsonSerializer.Deserialize<List<SecopProcesoDto>>(json);
            return result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando SECOP II: {Url}", url);
            return [];
        }
    }

}
