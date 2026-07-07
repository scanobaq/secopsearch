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

    // El dataset real nunca devuelve "tipo_de_proceso" — el campo correcto es
    // "modalidad_de_contratacion" (bug raíz identificado en SPEC-01).
    [JsonPropertyName("modalidad_de_contratacion")]
    public string? Modalidad { get; set; }

    [JsonPropertyName("estado_del_procedimiento")]
    public string? Estado { get; set; }

    [JsonPropertyName("adjudicado")]
    public string? Adjudicado { get; set; }

    [JsonPropertyName("valor_total_adjudicacion")]
    public string? ValorTotalAdjudicacion { get; set; }

    [JsonPropertyName("nombre_del_proveedor")]
    public string? NombreProveedorAdjudicado { get; set; }

    [JsonPropertyName("fecha_adjudicacion")]
    public string? FechaAdjudicacion { get; set; }

    [JsonPropertyName("categorias_adicionales")]
    public string? CategoriasAdicionales { get; set; }

    [JsonPropertyName("tipo_de_contrato")]
    public string? TipoContrato { get; set; }

    [JsonPropertyName("proveedores_invitados")]
    public string? ProveedoresInvitados { get; set; }

    [JsonPropertyName("proveedores_que_manifestaron")]
    public string? ProveedoresQueManifestaron { get; set; }

    [JsonPropertyName("respuestas_al_procedimiento")]
    public string? RespuestasAlProcedimiento { get; set; }

    [JsonPropertyName("conteo_de_respuestas_a_ofertas")]
    public string? ConteoRespuestasOfertas { get; set; }

    [JsonPropertyName("proveedores_unicos_con")]
    public string? ProveedoresUnicosCon { get; set; }

    [JsonPropertyName("fase")]
    public string? Fase { get; set; }

    [JsonPropertyName("estado_de_apertura_del_proceso")]
    public string? EstadoApertura { get; set; }

    [JsonPropertyName("estado_resumen")]
    public string? EstadoResumen { get; set; }

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

    public bool EstaAbiertoParaAplicar() =>
        EsAbierto(EstadoApertura) &&
        EsPublicado(Estado) &&
        (EsFaseAplicable(Fase) || EsFaseAplicable(EstadoResumen)) &&
        FechaRecepcionPermiteAplicar();

    public ModalidadContrato ObtenerModalidad() => NormalizarModalidad(Modalidad) switch
    {
        var t when t != null && t.Contains("LICITACI") => ModalidadContrato.LicitacionPublica,
        var t when t != null && t.Contains("ABREVIADA") => ModalidadContrato.SeleccionAbreviada,
        var t when t != null && t.Contains("RITOS") => ModalidadContrato.ConcursoMeritos,
        var t when t != null && t.Contains("DIRECTA") => ModalidadContrato.ContratacionDirecta,
        var t when t != null && t.Contains("NIMA CUANT") => ModalidadContrato.MinimaCuantia,
        var t when t != null && t.Contains("ACUERDO") => ModalidadContrato.AcuerdoMarcoPrecios,
        _ => ModalidadContrato.Otro
    };

    /// <summary>
    /// Clasifica el régimen de contratación real (SPEC-02) a partir de
    /// <c>modalidad_de_contratacion</c>.
    /// </summary>
    public ClasificacionRegimen ObtenerClasificacion() => NormalizarModalidad(Modalidad) switch
    {
        var m when m != null && m.Contains("SOLICITUD DE INFORMACI") => ClasificacionRegimen.Rfi,
        var m when m != null && m.Contains("GIMEN ESPECIAL")         => ClasificacionRegimen.RegimenEspecial,
        _ => ClasificacionRegimen.Ley80
    };

    /// <summary>
    /// Determina si el tipo de contrato corresponde al régimen ESAL
    /// (Decreto 092 de 2017) para entidades sin ánimo de lucro (SPEC-07).
    /// </summary>
    public bool EsElegibleEsal() =>
        TipoContrato?.Contains("092", StringComparison.OrdinalIgnoreCase) == true &&
        TipoContrato.Contains("2017", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parsea <c>categorias_adicionales</c> (lista separada por comas de códigos "V1.XXXXXXXX")
    /// y retorna los códigos sin el prefijo "V1." (SPEC-08).
    /// </summary>
    public List<string> ObtenerCategoriasAdicionales()
    {
        if (string.IsNullOrWhiteSpace(CategoriasAdicionales))
            return [];

        return CategoriasAdicionales
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.StartsWith("V1.", StringComparison.OrdinalIgnoreCase) ? c[3..] : c)
            .ToList();
    }

    public EstadoProceso ObtenerEstado()
    {
        if (EsAdjudicado())
            return EstadoProceso.Adjudicado;

        return Estado?.ToUpperInvariant() switch
        {
            var e when e != null && e.Contains("DESIERTO")     => EstadoProceso.Desierto,
            var e when e != null && e.Contains("CANCELADO")    => EstadoProceso.Cancelado,
            var e when e != null && e.Contains("SUSPENDIDO")   => EstadoProceso.Suspendido,
            var e when e != null && e.Contains("CERRADO")      => EstadoProceso.Cerrado,
            var e when e != null && e.Contains("SELECCIONADO") => EstadoProceso.Seleccionado,
            _ => EstadoProceso.Activo
        };
    }

    private bool EsAdjudicado() =>
        Adjudicado?.Equals("Si", StringComparison.OrdinalIgnoreCase) == true ||
        Adjudicado?.Equals("true", StringComparison.OrdinalIgnoreCase) == true ||
        (!string.IsNullOrWhiteSpace(NombreProveedorAdjudicado) &&
         !string.IsNullOrWhiteSpace(FechaAdjudicacion));

    private static string? NormalizarModalidad(string? modalidad) =>
        modalidad?.ToUpperInvariant();

    private static bool EsAbierto(string? value) =>
        value?.Equals("Abierto", StringComparison.OrdinalIgnoreCase) == true;

    private static bool EsPublicado(string? value) =>
        value?.Equals("Publicado", StringComparison.OrdinalIgnoreCase) == true;

    private static bool EsFaseAplicable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        return value.Equals("Presentación de oferta", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Fase de ofertas", StringComparison.OrdinalIgnoreCase);
    }

    private bool FechaRecepcionPermiteAplicar()
    {
        // Este campo es sparse en SECOP: algunos registros lo traen y otros no.
        // Si viene, es la señal más fuerte para evitar procesos ya vencidos.
        // Si no viene o no parsea, caemos al filtro operativo por estado/fase.
        if (!DateTime.TryParse(FechaCierre, out var fechaRecepcion))
            return true;

        return fechaRecepcion.Date >= DateTime.UtcNow.Date;
    }
}

public class UrlProcesoDto
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
