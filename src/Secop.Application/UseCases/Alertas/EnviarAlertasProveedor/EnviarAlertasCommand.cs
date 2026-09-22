using MediatR;

namespace Secop.Application.UseCases.Alertas.EnviarAlertasProveedor;

/// <summary>
/// Envía alertas de evaluaciones Analyze pendientes para un proveedor,
/// y alertas de RUP por vencer si corresponde.
/// </summary>
public record EnviarAlertasCommand(Guid ProveedorId) : IRequest;
