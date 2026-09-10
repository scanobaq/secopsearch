using System.Globalization;
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

    public bool TryObtenerPresupuestoCop(out decimal presupuestoCop)
    {
        presupuestoCop = default;
        var valor = Presupuesto?.Trim();
        if (string.IsNullOrEmpty(valor) || !EsLiteralDecimalAscii(valor) ||
            !decimal.TryParse(valor, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var resultado) ||
            resultado <= 0 || Canonicalizar(valor) != resultado.ToString("0.############################", CultureInfo.InvariantCulture))
            return false;

        presupuestoCop = resultado;
        return true;
    }

    private static bool EsLiteralDecimalAscii(string valor)
    {
        var puntoEncontrado = false;
        for (var indice = 0; indice < valor.Length; indice++)
        {
            var caracter = valor[indice];
            if (caracter is >= '0' and <= '9')
                continue;

            if (caracter == '.' && !puntoEncontrado && indice > 0 && indice < valor.Length - 1)
            {
                puntoEncontrado = true;
                continue;
            }

            return false;
        }

        return true;
    }

    private static string Canonicalizar(string valor)
    {
        var partes = valor.Split('.');
        var entero = partes[0].TrimStart('0');
        entero = entero.Length == 0 ? "0" : entero;
        if (partes.Length == 1)
            return entero;

        var fraccion = partes[1].TrimEnd('0');
        return fraccion.Length == 0 ? entero : $"{entero}.{fraccion}";
    }

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

    [JsonPropertyName("codigo_principal_de_categoria")]
    public string? CodigoPrincipalCategoria { get; set; }

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

    public bool EstaAbiertoParaAplicar()
    {
        if (!EsAbierto(EstadoApertura) || !EsPublicado(Estado))
            return false;

        if (EsFaseAplicable(Fase) || EsFaseAplicable(EstadoResumen))
            return FechaRecepcionPermiteAplicar();

        return string.IsNullOrWhiteSpace(Fase) &&
               EsResumenIndefinido(EstadoResumen) &&
               FechaRecepcionExplicitaPermiteAplicar();
    }

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
            .Select(QuitarPrefijoV1)
            .ToList();
    }

    // El campo real categorias_adicionales del API nunca trae punto tras "V1"
    // (ej. "V172101500"), a diferencia de codigo_principal_de_categoria que sí
    // lo trae ("V1.80111500"). Se soportan ambos formatos por las dudas.
    private static string QuitarPrefijoV1(string codigo) => codigo switch
    {
        var c when c.StartsWith("V1.", StringComparison.OrdinalIgnoreCase) => c[3..],
        var c when c.StartsWith("V1", StringComparison.OrdinalIgnoreCase) => c[2..],
        _ => codigo
    };

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

    private static bool EsResumenIndefinido(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Trim().Equals("No Definido", StringComparison.OrdinalIgnoreCase);

    private bool FechaRecepcionPermiteAplicar()
    {
        // Este campo es sparse en SECOP: algunos registros lo traen y otros no.
        // Si viene, es la señal más fuerte para evitar procesos ya vencidos.
        // Si no viene o no parsea, caemos al filtro operativo por estado/fase.
        if (!DateTime.TryParse(FechaCierre, out var fechaRecepcion))
            return true;

        return fechaRecepcion.Date >= DateTime.UtcNow.Date;
    }

    private bool FechaRecepcionExplicitaPermiteAplicar()
    {
        string[] formatosIso =
        [
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd'T'HH:mm:ssK"
        ];

        // SECOP también entrega timestamps sin offset; se interpretan como UTC.
        return DateTimeOffset.TryParseExact(
                   FechaCierre,
                   formatosIso,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                   out var fechaRecepcion) &&
               fechaRecepcion >= DateTimeOffset.UtcNow;
    }
}

public class UrlProcesoDto
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
