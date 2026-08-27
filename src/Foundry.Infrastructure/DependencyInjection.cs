using Foundry.Application.Content;
using Foundry.Infrastructure.Content;

namespace Microsoft.Extensions.DependencyInjection;

public static class FoundryInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra las implementaciones de infraestructura: persistencia JSON de la base de contenido.
    /// </summary>
    public static IServiceCollection AddFoundryInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IContentRepository, JsonContentRepository>();

        return services;
    }
}
