using Microsoft.Extensions.Configuration;
using Secop.Application.Interfaces;

namespace Secop.Infrastructure.Filtros;

/// <summary>
/// Implementación de <see cref="IFiltrosProcesoPolicy"/> que lee la configuración
/// del host (appsettings/env vars). Por defecto solo descarta el subtipo publicitario.
/// </summary>
public class FiltrosProcesoPolicy(IConfiguration configuration) : IFiltrosProcesoPolicy
{
    public bool DescartarSoloPublicitario =>
        configuration.GetValue<bool?>("Filtros:DescartarSoloPublicitario") ?? true;
}
