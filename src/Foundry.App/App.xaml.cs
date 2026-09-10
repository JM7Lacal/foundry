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
                .AddJsonFile("appsettings.json", optional: true)
                // Override local, ignorado por git: aca (y solo aca) van las API keys.
                .AddJsonFile("appsettings.Local.json", optional: true)
                // Ultima palabra: variables de entorno (p. ej. Assistant__ApiKey) para no
                // tener secretos en ningun archivo.
                .AddEnvironmentVariables())
            .ConfigureServices((context, services) =>
            {
                services.AddFoundryApplication(context.Configuration["Assistant:PromptVersion"]);
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
    /// El unico lugar donde se arma la pila del asistente. Por config (<c>appsettings.json</c> /
    /// <c>appsettings.Local.json</c> / variables de entorno como <c>Assistant__Provider</c>), sin
    /// recompilar: <c>Assistant:Provider</c> (primario), <c>Assistant:Fallback</c> (respaldo
    /// opcional). El proveedor elegido se envuelve en <see cref="ResilientChatCompletion"/>
    /// (reintentos + backoff + fallback + traza). Las API keys van solo por
    /// <c>appsettings.Local.json</c> (ignorado por git) o <c>Assistant__ApiKey</c>.
    /// </summary>
    private static void RegisterAssistantProvider(IConfiguration configuration, IServiceCollection services)
    {
        var primary = Blank(configuration["Assistant:Provider"]) ?? "stub";
        var fallback = Blank(configuration["Assistant:Fallback"]);

        services.AddHttpClient();

        // Observabilidad: ultimas llamadas en memoria + una linea JSON por llamada en disco.
        var callLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Foundry",
            "chat-calls.jsonl");
        services.AddSingleton<ChatCallLog>(_ => new ChatCallLog(capacity: 100, filePath: callLogPath));
        services.AddSingleton<IChatCallListener>(sp => sp.GetRequiredService<ChatCallLog>());

        services.AddSingleton<IChatCompletion>(sp => new ResilientChatCompletion(
            primary: BuildProvider(primary, configuration, sp),
            fallback: fallback is null || string.Equals(fallback, primary, StringComparison.OrdinalIgnoreCase)
                ? null
                : BuildProvider(fallback, configuration, sp),
            policy: ResiliencePolicy.Default,
            listener: sp.GetRequiredService<IChatCallListener>()));
    }

    /// <summary>Construye un proveedor crudo por nombre. Agregar uno nuevo = un <c>case</c>.</summary>
    private static IChatCompletion BuildProvider(string name, IConfiguration configuration, IServiceProvider sp)
    {
        var model = Blank(configuration["Assistant:Model"]);
        var apiKey = Blank(configuration["Assistant:ApiKey"]) ?? string.Empty;

        return name.Trim().ToLowerInvariant() switch
        {
            "claude-code" or "claudecode" => new ClaudeCodeChatCompletion(configuration["Assistant:Command"]),

            "ollama" => new OllamaChatCompletion(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
                model ?? "qwen2.5"),

            "anthropic" => new AnthropicChatCompletion(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
                apiKey,
                model ?? "claude-haiku-4-5-20251001"),

            "azure" or "azure-openai" or "foundry" => new AzureOpenAIChatCompletion(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
                endpoint: configuration["Assistant:Azure:Endpoint"] ?? string.Empty,
                deployment: configuration["Assistant:Azure:Deployment"] ?? string.Empty,
                apiKey: apiKey,
                apiVersion: Blank(configuration["Assistant:Azure:ApiVersion"]),
                model: model),

            _ => new StubChatCompletion(),
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// En el primer arranque copia el ejemplo empaquetado a <c>Documentos\Foundry</c> y lo abre
    /// desde ahi, como un archivo normal: asi Guardar (Ctrl+S) escribe sin pedir destino y el
    /// trabajo sobrevive a los rebuilds (que regeneran <c>bin\</c>). Si Documentos no se puede
    /// escribir, cae a abrir el bundle como documento "sin titulo".
    /// </summary>
    private async Task LoadBundledSampleAsync()
    {
        if (_host is null)
        {
            return;
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, "Samples", "tower-defense.json");
        if (!File.Exists(bundled))
        {
            return;
        }

        var userCopy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Foundry",
            "tower-defense.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(userCopy)!);
            if (!File.Exists(userCopy))
            {
                File.Copy(bundled, userCopy);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Documentos no disponible: se usa el bundle directamente.
        }

        var viewModel = _host.Services.GetRequiredService<MainViewModel>();
        if (File.Exists(userCopy))
        {
            await viewModel.LoadFromAsync(userCopy).ConfigureAwait(true);
        }
        else
        {
            await viewModel.LoadSampleAsync(bundled).ConfigureAwait(true);
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
