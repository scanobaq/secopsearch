using MediatR;
using Secop.Application.DTOs;

namespace Secop.Application.UseCases.Puntajes.CalcularPuntaje;

public record CalcularPuntajeCommand(string ProcesoId, Guid ProveedorId) : IRequest<PuntajeDto?>;
