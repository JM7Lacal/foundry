using System.IO;
using System.Windows;
using System.Windows.Threading;
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
    private static readonly string CrashLogPath =
        Path.Combine(Path.GetTempPath(), "foundry-crash.log");

    private IHost? _host;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            Bootstrap();
        }
        catch (Exception ex)
        {
            ReportFatal("Fallo al iniciar", ex);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(true);
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void Bootstrap()
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

        _host.Start();
        _host.Services.GetRequiredService<MainWindow>().Show();

        _ = LoadBundledSampleAsync();
    }

    /// <summary>Carga el archivo de ejemplo que se copia junto al ejecutable, si existe.</summary>
    private async Task LoadBundledSampleAsync()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "Samples", "tower-defense.json");
        if (_host is not null && File.Exists(samplePath))
        {
            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            await viewModel.LoadFromAsync(samplePath).ConfigureAwait(true);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReportFatal("Excepcion en la UI", e.Exception);
        e.Handled = true;
        Shutdown(-1);
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ReportFatal("Excepcion no manejada", ex);
        }
    }

    private static void ReportFatal(string context, Exception exception)
    {
        var message = $"{DateTimeOffset.Now:O}  {context}{Environment.NewLine}{exception}{Environment.NewLine}{new string('-', 60)}{Environment.NewLine}";

        try
        {
            File.AppendAllText(CrashLogPath, message);
        }
        catch (IOException)
        {
            // si no se puede escribir el log, al menos mostramos el cartel
        }

        MessageBox.Show(
            $"{context}:{Environment.NewLine}{Environment.NewLine}{exception.GetType().Name}: {exception.Message}{Environment.NewLine}{Environment.NewLine}Detalle en:{Environment.NewLine}{CrashLogPath}",
            "Foundry",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
