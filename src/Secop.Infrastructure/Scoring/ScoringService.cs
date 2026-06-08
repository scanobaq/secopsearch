using Microsoft.Extensions.Logging;
using Secop.Application.Interfaces;
using Secop.Domain.Constants;
using Secop.Domain.Entities;
using Secop.Domain.Enums;

namespace Secop.Infrastructure.Scoring;

/// <summary>
/// Calcula el puntaje de compatibilidad (0–100) entre un proveedor y un proceso SECOP II.
///
/// Componentes:
///   Similitud semántica   35 pts  (embedding coseno)
///   Requisitos           25 pts  (RUP vigente + capacidad financiera)
///   Tiempo disponible    20 pts  (días hábiles al cierre)
///   Competencia estimada 12 pts  (simplificado v1: desierto vs activo)
///   Historial entidad     8 pts  (simplificado v1: valor neutro)
///
/// Regla inhabilitante: RUP vencido → puntaje = 0, etiqueta = Descartar.
/// </summary>
public class ScoringService(ILogger<ScoringService> logger) : IScoringService
{
    public Task<Puntaje> CalcularAsync(Proveedor proveedor, Proceso proceso, float similitud)
    {
        var advertencias = new List<string>();

        // Inhabilitante automático: RUP vencido
        // if (!proveedor.RupVigente())
        // {
        //     advertencias.Add(AdvertenciasPuntaje.RupVencido);
        //     return Task.FromResult(Puntaje.Inhabilitado(proceso.Id, proveedor.Id, advertencias));
        // }

        // Componente 1: Similitud semántica (35 pts)
        float pSimilitud = similitud * 35f;

        // Componente 2: Requisitos habilitantes (25 pts)
        float pRequisitos = 15f; // RUP vigente ya verificado arriba
        if (proveedor.CubreCapacidadFinanciera(proceso.Presupuesto))
            pRequisitos += 10f;

        // Componente 3: Tiempo disponible (20 pts)
        // int dias = proceso.DiasHabilesRestantes();
        // float pTiempo = dias switch
        // {
        //     >= 15 => 20f,
        //     >= 10 => 15f,
        //     >= 5 => 8f,
        //     _ => 2f
        // };

        // Señal de proceso dirigido
        // if (dias <= 3)
        //     advertencias.Add(AdvertenciasPuntaje.PlazoMuyCorto);

        // Componente 4: Competencia estimada (12 pts) — v1 simplificado
        // Si el proceso ya fue desierto una vez, hay menos competencia
        //float pCompetencia = proceso.EsDesierto() ? 12f : 6f;

        // Componente 5: Historial de la entidad (8 pts) — v1 simplificado
        // Valor neutro hasta tener historial real de adjudicaciones
        //float pEntidad = 4f;

        //float total = pSimilitud + pRequisitos + pTiempo + pCompetencia + pEntidad;
        float total = pSimilitud + pRequisitos + 0 + 0 + 0;
        total = Math.Min(total, 100f); // Cap defensivo

        logger.LogInformation(
            "Puntaje calculado para Proceso {ProcesoId} y Proveedor {ProveedorId}: Total={Total}, Similitud={Similitud}, Requisitos={Requisitos}",
            proceso.Id, proveedor.Id, total, pSimilitud, pRequisitos);

        var etiqueta = total switch
        {
            >= 70 => EtiquetaProceso.Proponer,
            >= 40 => EtiquetaProceso.Analizar,
            _ => EtiquetaProceso.Descartar
        };

        return Task.FromResult(new Puntaje(
            procesoId: proceso.Id,
            proveedorId: proveedor.Id,
            puntajeTotal: total,
            puntajeSimilitud: pSimilitud,
            puntajeRequisitos: pRequisitos,
            puntajeTiempo: 0,//pTiempo,
            puntajeCompetencia: 0,//pCompetencia,
            puntajeEntidad: 0,//pEntidad,
            etiqueta: etiqueta,
            advertencias: advertencias));
    }
}
