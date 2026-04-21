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

        var dias = proceso.DiasHabilesRestantes();
        var diasTexto = dias == 0 ? "Vencido" : $"{dias} días hábiles";
        var presupuesto = proceso.Presupuesto.ToString("C0");
        var cierre = proceso.FechaCierre.ToString("dd 'de' MMMM", new System.Globalization.CultureInfo("es-CO"));

        var advertencias = puntaje.Advertencias.Count > 0
            ? "\n⚠️ " + string.Join("\n⚠️ ", puntaje.Advertencias)
            : string.Empty;

        return $"""
            <b>{emoji} — Puntaje: {puntaje.PuntajeTotal:F0}/100</b>

            📋 {EscapeHtml(proceso.Titulo)}

            🏛 Entidad: <b>{EscapeHtml(proceso.NombreEntidad)}</b>
            💰 Presupuesto: <b>{presupuesto}</b>
            📅 Cierre: {cierre} ({diasTexto})
            🏷 Modalidad: {proceso.Modalidad}
            🏢 Empresa recomendada: <b>{EscapeHtml(proveedor.Nombre)}</b>
            {advertencias}
            🔗 <a href="{proceso.UrlProceso}">Ver proceso en SECOP II</a>
            """;
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
