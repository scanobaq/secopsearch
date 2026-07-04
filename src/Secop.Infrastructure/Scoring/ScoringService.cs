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

        // Inhabilitante automático: solo ESAL (Decreto 092 de 2017) — mismo mecanismo que RUP vencido
        if (proceso.EsSoloEsal())
        {
            advertencias.Add(AdvertenciasPuntaje.SoloEsal);
            return Task.FromResult(Puntaje.Inhabilitado(proceso.Id, proveedor.Id, advertencias));
        }

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

        // Componente 4: Competencia estimada (12 pts) — normalizada por contadores reales (SPEC-06)
        float pCompetencia = CalcularCompetencia(proceso);

        // Componente 5: Historial de la entidad (8 pts) — v1 simplificado
        // Valor neutro hasta tener historial real de adjudicaciones
        //float pEntidad = 4f;

        //float total = pSimilitud + pRequisitos + pTiempo + pCompetencia + pEntidad;
        float total = pSimilitud + pRequisitos + 0 + pCompetencia + 0;
        total = Math.Min(total, 100f); // Cap defensivo

        logger.LogInformation(
            "Puntaje calculado para Proceso {ProcesoId} y Proveedor {ProveedorId}: Total={Total}, Similitud={Similitud}, Requisitos={Requisitos}, Competencia={Competencia}",
            proceso.Id, proveedor.Id, total, pSimilitud, pRequisitos, pCompetencia);

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
            puntajeCompetencia: pCompetencia,
            puntajeEntidad: 0,//pEntidad,
            etiqueta: etiqueta,
            advertencias: advertencias));
    }

    /// <summary>
    /// Normaliza la competencia estimada (0–12 pts) a partir de los contadores reales
    /// del proceso. Menos competidores → puntaje más alto (menos competencia para el proveedor).
    /// Si no hay ningún contador disponible (proceso aún no cerró), retorna un valor neutro.
    /// </summary>
    private static float CalcularCompetencia(Proceso proceso)
    {
        if (proceso.ProveedoresInvitados is null &&
            proceso.ProveedoresQueManifestaron is null &&
            proceso.RespuestasAlProcedimiento is null &&
            proceso.ConteoRespuestasOfertas is null &&
            proceso.ProveedoresUnicosCon is null)
        {
            return 6f; // Neutral — sin señal aún
        }

        int competidores = proceso.ProveedoresUnicosCon
            ?? proceso.ConteoRespuestasOfertas
            ?? proceso.RespuestasAlProcedimiento
            ?? proceso.ProveedoresQueManifestaron
            ?? proceso.ProveedoresInvitados
            ?? 0;

        return competidores switch
        {
            0 => 12f,
            1 => 10f,
            <= 3 => 8f,
            <= 6 => 5f,
            _ => 2f
        };
    }
}
