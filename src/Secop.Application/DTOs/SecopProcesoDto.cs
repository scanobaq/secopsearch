using System.Text.Json.Serialization;
using Secop.Domain.Enums;

namespace Secop.Application.DTOs;

/// <summary>
/// Mapea la respuesta JSON del endpoint de SECOP II (datos.gov.co).
/// Los nombres de los campos siguen el dataset p6dx-8zbt tal cual vienen de la API,
/// incluyendo el bug conocido: "fecha_de_ultima_publicaci" (sin la 'ó' final).
/// </summary>
public class SecopProcesoDto
{
    [JsonPropertyName("id_del_proceso")]
    public string? Id { get; set; }

    [JsonPropertyName("nombre_del_procedimiento")]
    public string? Titulo { get; set; }

    [JsonPropertyName("descripci_n_del_procedimiento")]
    public string? Objeto { get; set; }

    [JsonPropertyName("precio_base")]
    public string? Presupuesto { get; set; }

    // El dataset trunca el nombre: "fecha_de_recepcion_de" (sin "_ofertas")
    [JsonPropertyName("fecha_de_recepcion_de")]
    public string? FechaCierre { get; set; }

    // Bug conocido del dataset SECOP II: campo truncado sin acento
    [JsonPropertyName("fecha_de_ultima_publicaci")]
    public string? FechaUltimaPublicacion { get; set; }

    [JsonPropertyName("tipo_de_proceso")]
    public string? TipoProceso { get; set; }

    [JsonPropertyName("estado_del_procedimiento")]
    public string? Estado { get; set; }

    // El dataset usa "entidad" (sin prefijo "nombre_de_la_")
    [JsonPropertyName("entidad")]
    public string? NombreEntidad { get; set; }

    [JsonPropertyName("nit_entidad")]
    public string? NitEntidad { get; set; }

    [JsonPropertyName("departamento_entidad")]
    public string? DepartamentoEntidad { get; set; }

    // El dataset devuelve urlproceso como objeto { "url": "..." }
    [JsonPropertyName("urlproceso")]
    public UrlProcesoDto? UrlProcesoObj { get; set; }

    public string? UrlProceso => UrlProcesoObj?.Url;

    // Helpers de mapeo
    public bool EsValido() =>
        !string.IsNullOrWhiteSpace(Id) &&
        !string.IsNullOrWhiteSpace(Titulo) &&
        !string.IsNullOrWhiteSpace(NombreEntidad);

    public ModalidadContrato ObtenerModalidad() => TipoProceso?.ToUpperInvariant() switch
    {
        var t when t != null && t.Contains("LICITACION") => ModalidadContrato.LicitacionPublica,
        var t when t != null && t.Contains("ABREVIADA")  => ModalidadContrato.SeleccionAbreviada,
        var t when t != null && t.Contains("MERITOS")    => ModalidadContrato.ConcursoMeritos,
        var t when t != null && t.Contains("DIRECTA")    => ModalidadContrato.ContratacionDirecta,
        var t when t != null && t.Contains("MINIMA")     => ModalidadContrato.MinimaCuantia,
        var t when t != null && t.Contains("ACUERDO")    => ModalidadContrato.AcuerdoMarcoPrecios,
        _ => ModalidadContrato.Otro
    };

    public EstadoProceso ObtenerEstado() => Estado?.ToUpperInvariant() switch
    {
        var e when e != null && e.Contains("DESIERTO")   => EstadoProceso.Desierto,
        var e when e != null && e.Contains("ADJUDICADO") => EstadoProceso.Adjudicado,
        var e when e != null && e.Contains("CANCELADO")  => EstadoProceso.Cancelado,
        var e when e != null && e.Contains("CERRADO")    => EstadoProceso.Cerrado,
        _ => EstadoProceso.Activo
    };
}

public class UrlProcesoDto
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
