namespace Secop.Application.UseCases.Proveedores.GenerarEmbeddingProveedor;

/// <summary>
/// Genera y persiste el embedding de un proveedor a partir de su experiencia.
/// Debe ejecutarse una vez al registrar el proveedor y cada vez que se actualice su perfil.
/// </summary>
public record GenerarEmbeddingProveedorCommand(Guid ProveedorId) : MediatR.IRequest<bool>;
