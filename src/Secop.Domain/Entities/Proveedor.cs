namespace Secop.Domain.Entities;

public class Proveedor
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; }
    public string Nit { get; private set; }
    public DateTime RupVigencia { get; private set; }
    public decimal CapacidadFinanciera { get; private set; }
    public List<string> CodigosUnspsc { get; private set; }
    public List<string> ExperienciaDescripcion { get; private set; }
    public List<string> PalabrasClave { get; private set; }
    public float[]? Embedding { get; private set; }
    public long? TelegramChatId { get; private set; }
    public DateTime CreadoEn { get; private set; }
    public DateTime ActualizadoEn { get; private set; }

    private Proveedor()
    {
        Nombre = null!; Nit = null!;
        CodigosUnspsc = null!; ExperienciaDescripcion = null!; PalabrasClave = null!;
    }

    public Proveedor(
        string nombre,
        string nit,
        DateTime rupVigencia,
        decimal capacidadFinanciera,
        List<string> codigosUnspsc,
        List<string> experienciaDescripcion,
        long? telegramChatId = null,
        List<string>? palabrasClave = null)
    {
        Id = Guid.NewGuid();
        Nombre = nombre;
        Nit = nit;
        RupVigencia = rupVigencia;
        CapacidadFinanciera = capacidadFinanciera;
        CodigosUnspsc = codigosUnspsc;
        ExperienciaDescripcion = experienciaDescripcion;
        TelegramChatId = telegramChatId;
        PalabrasClave = palabrasClave ?? [];
        CreadoEn = DateTime.UtcNow;
        ActualizadoEn = DateTime.UtcNow;
    }

    public bool RupVigente() => RupVigencia > DateTime.UtcNow;

    public int DiasParaVencimientoRup() => (RupVigencia.Date - DateTime.UtcNow.Date).Days;

    public bool CubreCapacidadFinanciera(decimal presupuestoProceso, decimal porcentajeMinimo = 0.15m) =>
        CapacidadFinanciera >= presupuestoProceso * porcentajeMinimo;

    public void AsignarEmbedding(float[] embedding)
    {
        Embedding = embedding;
        ActualizadoEn = DateTime.UtcNow;
    }

    public void ActualizarTelegramChatId(long chatId)
    {
        TelegramChatId = chatId;
        ActualizadoEn = DateTime.UtcNow;
    }

    public void ActualizarPalabrasClave(IEnumerable<string> palabras)
    {
        PalabrasClave = palabras
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        ActualizadoEn = DateTime.UtcNow;
    }
}
