using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Secop.Application.Interfaces;
using Secop.Domain.Constants;
using Secop.Domain.Entities;

namespace Secop.Infrastructure.Persistence.Repositories;

public class ProcesoRepository : IProcesoRepository
{
    private readonly AppDbContext _context;

    public ProcesoRepository(AppDbContext context) => _context = context;

    public Task<Proceso?> ObtenerPorIdAsync(string id, CancellationToken ct) =>
        _context.Procesos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Proceso>> ObtenerActivosAsync(CancellationToken ct) =>
        await _context.Procesos
            .Where(p => p.Estado == Domain.Enums.EstadoProceso.Activo)
            .ToListAsync(ct);

    public async Task<IEnumerable<Proceso>> ObtenerDesiertosSinAlertaAsync(CancellationToken ct)
    {
        var idsConAlerta = await _context.Alertas
            .Where(a => a.Tipo == Domain.Enums.TipoAlerta.Desierto && a.ProcesoId != null)
            .Select(a => a.ProcesoId!)
            .ToListAsync(ct);

        return await _context.Procesos
            .Where(p => p.Estado == Domain.Enums.EstadoProceso.Desierto
                     && !idsConAlerta.Contains(p.Id))
            .ToListAsync(ct);
    }

    public Task<bool> ExisteAsync(string id, CancellationToken ct) =>
        _context.Procesos.AnyAsync(p => p.Id == id, ct);

    public async Task GuardarAsync(Proceso proceso, CancellationToken ct)
    {
        if (await ExisteAsync(proceso.Id, ct))
            _context.Procesos.Update(proceso);
        else
            await _context.Procesos.AddAsync(proceso, ct);

        await _context.SaveChangesAsync(ct);
    }

    public async Task GuardarRangoAsync(IEnumerable<Proceso> procesos, CancellationToken ct)
    {
        await _context.Procesos.AddRangeAsync(procesos, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Búsqueda vectorial con el operador coseno de pgvector (<=>).
    /// Usa ADO.NET directo para poder leer la columna de similitud junto con la entidad.
    /// </summary>
    public async Task<IEnumerable<(Proceso Proceso, float Similitud)>> BuscarPorSimilitudAsync(
        float[] embedding,
        float umbralMinimo = PoliticaEvaluacion.UmbralSimilitud,
        int limite = 50,
        CancellationToken ct = default)
    {
        var embStr = "[" + string.Join(",",
            embedding.Select(f => f.ToString("G6", CultureInfo.InvariantCulture))) + "]";

        var similitudes = new Dictionary<string, float>();

        var conn = (NpgsqlConnection)_context.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT id, (1 - (embedding <=> '{embStr}'::vector))::real AS sim
                FROM procesos
                WHERE embedding IS NOT NULL
                  AND (1 - (embedding <=> '{embStr}'::vector)) >= @umbral
                ORDER BY embedding <=> '{embStr}'::vector
                LIMIT @lim
                """;
            cmd.Parameters.AddWithValue("umbral", (double)umbralMinimo);
            cmd.Parameters.AddWithValue("lim", limite);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                similitudes[reader.GetString(0)] = reader.GetFloat(1);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }

        if (similitudes.Count == 0) return [];

        var procesos = await _context.Procesos
            .Where(p => similitudes.Keys.Contains(p.Id))
            .ToListAsync(ct);

        return procesos
            .Select(p => (p, similitudes[p.Id]))
            .OrderByDescending(x => x.Item2);
    }
}
