using Foundry.Application.Content;
using Foundry.Application.Editing;
using Foundry.Infrastructure.Content;

namespace Microsoft.Extensions.DependencyInjection;

public static class FoundryInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra las implementaciones de infraestructura: persistencia y serializacion JSON.
    /// </summary>
    public static IServiceCollection AddFoundryInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IContentRepository, JsonContentRepository>();
        services.AddSingleton<IContentSerializer, JsonContentSerializer>();

        return services;
    }
}
