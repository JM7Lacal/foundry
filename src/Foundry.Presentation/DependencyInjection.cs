using Foundry.Presentation.ViewModels;

namespace Microsoft.Extensions.DependencyInjection;

public static class FoundryPresentationServiceCollectionExtensions
{
    /// <summary>Registra los ViewModels. La implementacion de <c>IFilePicker</c> la aporta la capa de UI.</summary>
    public static IServiceCollection AddFoundryPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InspectorViewModel>();
        services.AddSingleton<AssistantViewModel>();
        services.AddSingleton<MainViewModel>();

        return services;
    }
}
