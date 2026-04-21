namespace Secop.Domain.Entities;

/// <summary>
/// Representa una de las unidades de negocio del grupo empresarial.
/// Se asocia a uno o más Proveedores registrados en SECOP.
/// </summary>
public class Empresa
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; }
    public string Descripcion { get; private set; }
    public List<Guid> ProveedorIds { get; private set; }

    private Empresa() { Nombre = null!; Descripcion = null!; ProveedorIds = null!; }

    public Empresa(string nombre, string descripcion)
    {
        Id = Guid.NewGuid();
        Nombre = nombre;
        Descripcion = descripcion;
        ProveedorIds = [];
    }

    public void AgregarProveedor(Guid proveedorId)
    {
        if (!ProveedorIds.Contains(proveedorId))
            ProveedorIds.Add(proveedorId);
    }
}
