using Foundry.Application.Ai;
using Foundry.Application.Editing;
using Foundry.Application.Undo;
using Foundry.Application.Validation;

// Convencion .NET: las extensiones de registro viven en este namespace para que aparezcan
// automaticamente donde se configura el contenedor.
namespace Microsoft.Extensions.DependencyInjection;

public static class FoundryApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de la capa Application (casos de uso, undo/redo, validacion).
    /// <paramref name="assistantPromptVersion"/>: version de system prompt para el asistente — id
    /// (<c>assistant-system@v1</c>) o familia (<c>assistant-system</c> → ultima); <c>null</c> = ultima.
    /// </summary>
    public static IServiceCollection AddFoundryApplication(
        this IServiceCollection services, string? assistantPromptVersion = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<UndoStack>();
        services.AddSingleton<ContentValidator>();
        services.AddSingleton(_ => PromptLibrary.Default);
        services.AddSingleton(sp => new ContentAssistant(
            sp.GetRequiredService<IChatCompletion>(),
            sp.GetRequiredService<IContentSerializer>(),
            sp.GetRequiredService<PromptLibrary>(),
            assistantPromptVersion));

        return services;
    }
}
