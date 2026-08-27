using Foundry.Application.Undo;

// Convencion .NET: las extensiones de registro viven en este namespace para que aparezcan
// automaticamente donde se configura el contenedor.
namespace Microsoft.Extensions.DependencyInjection;

public static class FoundryApplicationServiceCollectionExtensions
{
    /// <summary>Registra los servicios de la capa Application (casos de uso, undo/redo, validacion).</summary>
    public static IServiceCollection AddFoundryApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<UndoStack>();

        return services;
    }
}
