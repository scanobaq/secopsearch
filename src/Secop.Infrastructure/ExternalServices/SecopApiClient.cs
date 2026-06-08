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

        var unspscPredicados = new List<string>();
        if (exactos.Count > 0)
        {
            var inClause = string.Join(",", exactos.Select(c => $"'V1.{c}'"));
            unspscPredicados.Add($"codigo_principal_de_categoria IN({inClause})");
        }
        foreach (var clase in clases)
            unspscPredicados.Add($"codigo_principal_de_categoria LIKE 'V1.{clase}%'");

        var whereClause = filtroFecha;
        if (unspscPredicados.Count > 0)
            whereClause += $" AND ({string.Join(" OR ", unspscPredicados)})";

        var where = Uri.EscapeDataString(whereClause);
        var order = Uri.EscapeDataString("fecha_de_ultima_publicaci DESC");
        var url = $"{BaseUrl}/{DatasetProcesos}.json?$where={where}&$limit=1000&$order={order}";

        return await EjecutarConsultaAsync(url, ct);
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
