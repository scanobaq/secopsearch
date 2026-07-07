using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Infrastructure.ExternalServices.Telegram;

public static class ProcesoMessageFormatter
{
    public static string Formatear(Puntaje puntaje, Proceso proceso, Proveedor proveedor)
    {
        var emoji = puntaje.Etiqueta switch
        {
            EtiquetaProceso.Proponer  => "🟢 PROPONER",
            EtiquetaProceso.Analizar  => "🟡 ANALIZAR",
            EtiquetaProceso.Descartar => "🔴 DESCARTAR",
            _ => "⚪"
        };

        var presupuesto = proceso.Presupuesto.ToString("C0");
        var cierreTexto = FormatearCierre(proceso);

        var advertencias = puntaje.Advertencias.Count > 0
            ? "\n⚠️ " + string.Join("\n⚠️ ", puntaje.Advertencias)
            : string.Empty;

        return $"""
            <b>{emoji} — Puntaje: {puntaje.PuntajeTotal:F0}/100</b>

            📋 {EscapeHtml(proceso.Titulo)}

            🏛 Entidad: <b>{EscapeHtml(proceso.NombreEntidad)}</b>
            💰 Presupuesto: <b>{presupuesto}</b>
            📅 Cierre: {cierreTexto}
            🏷 Modalidad: {NombreModalidad(proceso)}
            🏢 Empresa recomendada: <b>{EscapeHtml(proveedor.Nombre)}</b>
            {advertencias}
            🔗 <a href="{proceso.UrlProceso}">Ver proceso en SECOP II</a>
            """;
    }

    // El tipo real de proceso vive repartido en dos campos del dominio:
    // Clasificacion (Rfi/RegimenEspecial, agregado en SPEC-02) toma precedencia
    // porque Modalidad cae en Otro para esos casos por diseño (ver SecopProcesoDtoTests).
    private static string NombreModalidad(Proceso proceso) => proceso.Clasificacion switch
    {
        ClasificacionRegimen.Rfi => "Solicitud de información",
        ClasificacionRegimen.RegimenEspecial => "Régimen especial",
        _ => proceso.Modalidad switch
        {
            ModalidadContrato.LicitacionPublica => "Licitación pública",
            ModalidadContrato.SeleccionAbreviada => "Selección abreviada",
            ModalidadContrato.ConcursoMeritos => "Concurso de méritos",
            ModalidadContrato.ContratacionDirecta => "Contratación directa",
            ModalidadContrato.MinimaCuantia => "Mínima cuantía",
            ModalidadContrato.AcuerdoMarcoPrecios => "Acuerdo marco de precios",
            _ => "Otro"
        }
    };

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string FormatearCierre(Proceso proceso)
    {
        // El dataset SECOP p6dx-8zbt no siempre expone fecha real de cierre.
        // Cuando falta, el dominio queda con DateTime.MinValue; eso NO debe mostrarse como vencido.
        if (proceso.FechaCierre <= DateTime.UnixEpoch)
            return "No disponible en SECOP";

        var dias = proceso.DiasHabilesRestantes();
        var diasTexto = dias == 0 ? "Vencido" : $"{dias} días hábiles";
        var cierre = proceso.FechaCierre.ToString("dd 'de' MMMM", new System.Globalization.CultureInfo("es-CO"));

        return $"{cierre} ({diasTexto})";
    }
}
