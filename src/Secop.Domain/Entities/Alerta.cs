using Secop.Domain.Enums;

namespace Secop.Domain.Entities;

public class Alerta
{
    public Guid Id { get; private set; }
    public string? ProcesoId { get; private set; }
    public Guid? ProveedorId { get; private set; }
    public TipoAlerta Tipo { get; private set; }
    public DateTime EnviadaEn { get; private set; }
    public int? TelegramMessageId { get; private set; }

    private Alerta() { /* Para EF Core */ }

    public Alerta(TipoAlerta tipo, string? procesoId = null, Guid? proveedorId = null)
    {
        Id = Guid.NewGuid();
        Tipo = tipo;
        ProcesoId = procesoId;
        ProveedorId = proveedorId;
        EnviadaEn = DateTime.UtcNow;
    }

    public void AsignarTelegramMessageId(int messageId) => TelegramMessageId = messageId;
}
