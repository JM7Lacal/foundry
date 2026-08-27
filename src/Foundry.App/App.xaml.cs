using System.Windows;
using Foundry.App.ViewModels;
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

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync().ConfigureAwait(true);
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync().ConfigureAwait(true);
        _host.Dispose();

        base.OnExit(e);
    }
}
