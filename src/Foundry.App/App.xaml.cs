using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Foundry.App.Services;
using Foundry.Application.Ai;
using Foundry.Infrastructure.Ai;
using Foundry.Presentation.Services;
using Foundry.Presentation.ViewModels;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Foundry.App;

/// <summary>
/// Composition root. Construye el <see cref="IHost"/> con el contenedor de DI, resuelve la
/// ventana principal y gestiona el ciclo de vida (start / stop / dispose).
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            ReportFatal("Excepcion en la UI", e.Exception);
            e.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ReportFatal("Excepcion no manejada", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportFatal("Excepcion en una tarea", e.Exception);
            e.SetObserved();
        };
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
            .ConfigureAppConfiguration(builder => builder
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true))
            .ConfigureServices((context, services) =>
            {
                services.AddFoundryApplication();
                services.AddFoundryInfrastructure();
                services.AddFoundryPresentation();

                services.AddSingleton<IFilePicker, WpfFilePicker>();
                services.AddSingleton<IDialogService, WpfDialogService>();
                services.AddSingleton<IRecentFiles, JsonRecentFiles>();
                services.AddSingleton<IPreferences, JsonPreferences>();
                services.AddSingleton<IThemeService, WpfThemeService>();
                services.AddSingleton<MainWindow>();

                RegisterAssistantProvider(context.Configuration, services);
            })
            .Build();

        _host.Start();

        _host.Services.GetRequiredService<IThemeService>(); // aplica la paleta guardada antes de mostrar la ventana
        _host.Services.GetRequiredService<MainWindow>().Show();

        _ = LoadBundledSampleAsync();
    }

    /// <summary>
    /// El unico lugar donde se elige el modelo del asistente. Se puede cambiar por config
    /// (<c>appsettings.json</c> → <c>Assistant:Provider</c>) sin recompilar.
    /// </summary>
    private static void RegisterAssistantProvider(IConfiguration configuration, IServiceCollection services)
    {
        var provider = configuration["Assistant:Provider"] ?? "stub";
        var model = configuration["Assistant:Model"];
        var apiKey = configuration["Assistant:ApiKey"] ?? string.Empty;

        switch (provider.Trim().ToLowerInvariant())
        {
            case "claude-code":
            case "claudecode":
                var command = configuration["Assistant:Command"];
                services.AddSingleton<IChatCompletion>(_ => new ClaudeCodeChatCompletion(command));
                break;

            case "ollama":
                services.AddHttpClient();
                services.AddSingleton<IChatCompletion>(sp =>
                    new OllamaChatCompletion(
                        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
                        model ?? "qwen2.5"));
                break;

            case "anthropic":
                services.AddHttpClient();
                services.AddSingleton<IChatCompletion>(sp =>
                    new AnthropicChatCompletion(
                        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
                        apiKey,
                        model ?? "claude-haiku-4-5-20251001"));
                break;

            default:
                services.AddSingleton<IChatCompletion, StubChatCompletion>();
                break;
        }
    }

    /// <summary>Carga el archivo de ejemplo que se copia junto al ejecutable, si existe.</summary>
    private async Task LoadBundledSampleAsync()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "Samples", "tower-defense.json");
        if (_host is not null && File.Exists(samplePath))
        {
            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            await viewModel.LoadFromAsync(samplePath, remember: false).ConfigureAwait(true);
        }
    }

    private static void ReportFatal(string context, Exception exception)
    {
        var text = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("O"))
            .Append("  ")
            .AppendLine(context)
            .AppendLine(exception.ToString())
            .AppendLine(new string('-', 70))
            .ToString();

        var written = TryWrite(Path.Combine(Path.GetTempPath(), "foundry-crash.log"), text)
                      || TryWrite(Path.Combine(AppContext.BaseDirectory, "foundry-crash.log"), text)
                      || TryWrite(
                          Path.Combine(
                              Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                              "foundry-crash.log"),
                          text);

        try
        {
            MessageBox.Show(
                $"{context}:\n\n{exception.GetType().Name}: {exception.Message}\n\n"
                + (written ? "Detalle completo en foundry-crash.log" : text),
                "Foundry",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (InvalidOperationException)
        {
            // sin dispatcher disponible; el archivo de log ya tiene el detalle
        }
    }

    private static bool TryWrite(string path, string text)
    {
        try
        {
            File.AppendAllText(path, text);
            return true;
        }
        catch (Exception ex) when (ex is IOException
                                      or UnauthorizedAccessException
                                      or System.Security.SecurityException
                                      or NotSupportedException
                                      or ArgumentException)
        {
            return false;
        }
    }
}
