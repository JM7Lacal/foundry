namespace Microsoft.Extensions.DependencyInjection;

public static class FoundryInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra las implementaciones de infraestructura (repositorio JSON, importadores, disco).
    /// Se completa en el Dia 1.
    /// </summary>
    public static IServiceCollection AddFoundryInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
