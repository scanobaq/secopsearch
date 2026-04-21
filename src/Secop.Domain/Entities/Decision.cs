using Secop.Domain.Enums;

namespace Secop.Domain.Entities;

public class Decision
{
    public Guid Id { get; private set; }
    public string ProcesoId { get; private set; }
    public Guid ProveedorId { get; private set; }
    public AccionDecision Accion { get; private set; }
    public string? RazonDescarte { get; private set; }
    public string? Resultado { get; private set; }
    public DateTime RegistradoEn { get; private set; }

    private Decision() { ProcesoId = null!; }

    public Decision(string procesoId, Guid proveedorId, AccionDecision accion, string? razonDescarte = null)
    {
        Id = Guid.NewGuid();
        ProcesoId = procesoId;
        ProveedorId = proveedorId;
        Accion = accion;
        RazonDescarte = razonDescarte;
        RegistradoEn = DateTime.UtcNow;
    }

    public void RegistrarResultado(string resultado) => Resultado = resultado;
}
