using System.IO;
using System.Windows;
using Foundry.App.Services;
using Foundry.Presentation.Services;
using Foundry.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Foundry.App;

/// <summary>
/// Composition root. Construye el <see cref="IHost"/> con el contenedor de DI, resuelve la
/// ventana principal y gestiona el ciclo de vida (start / stop / dispose).
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddFoundryApplication();
                services.AddFoundryInfrastructure();
                services.AddFoundryPresentation();

                services.AddSingleton<IFilePicker, WpfFilePicker>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync().ConfigureAwait(true);

        _host.Services.GetRequiredService<MainWindow>().Show();

        await LoadBundledSampleAsync().ConfigureAwait(true);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync().ConfigureAwait(true);
        _host.Dispose();

        base.OnExit(e);
    }

    /// <summary>Carga el archivo de ejemplo que se copia junto al ejecutable, si existe.</summary>
    private async Task LoadBundledSampleAsync()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "Samples", "tower-defense.json");
        if (File.Exists(samplePath))
        {
            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            await viewModel.LoadFromAsync(samplePath).ConfigureAwait(true);
        }
    }
}
