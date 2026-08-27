using Foundry.Application.Content;
using Foundry.Application.Editing;
using Foundry.Infrastructure.Content;

namespace Microsoft.Extensions.DependencyInjection;

public static class FoundryInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra las implementaciones de infraestructura: persistencia, serializacion e importadores.
    /// </summary>
    public static IServiceCollection AddFoundryInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IContentRepository, JsonContentRepository>();
        services.AddSingleton<IContentSerializer, JsonContentSerializer>();
        services.AddSingleton<IContentImporter, CsvContentImporter>();

        return services;
    }
}
