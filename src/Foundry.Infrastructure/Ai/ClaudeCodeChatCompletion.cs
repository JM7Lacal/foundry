using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Foundry.Application.Ai;

namespace Foundry.Infrastructure.Ai;

/// <summary>
/// Usa el CLI de Claude Code (<c>claude -p</c>) como modelo: aprovecha la suscripcion del
/// desarrollador, sin costo por llamada. Solo para uso individual.
/// </summary>
/// <remarks>
/// Resuelve el ejecutable en este orden: ruta explicita → <c>claude</c> en el PATH → instalacion
/// nativa en <c>%APPDATA%\Claude\claude-code\&lt;version&gt;\claude.exe</c>.
/// </remarks>
public sealed class ClaudeCodeChatCompletion : IChatCompletion
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    private readonly string _executable;

    public ClaudeCodeChatCompletion(string? commandOverride = null)
    {
        _executable = Resolve(commandOverride);
    }

    public string Name => "claude-code";

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var startInfo = new ProcessStartInfo
        {
            FileName = _executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetTempPath(),
        };
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("--output-format");
        startInfo.ArgumentList.Add("json");

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new ChatCompletionException($"No se pudo ejecutar '{_executable}'.", ex);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        await process.StandardInput.WriteAsync(Flatten(messages)).ConfigureAwait(false);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new ChatCompletionException("El CLI 'claude' tardo demasiado.");
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new ChatCompletionException($"'claude' salio con codigo {process.ExitCode}. {stderr}".Trim());
        }

        return ExtractResult(stdout);
    }

    private static string Resolve(string? commandOverride)
    {
        if (!string.IsNullOrWhiteSpace(commandOverride))
        {
            return commandOverride;
        }

        if (OnPath("claude"))
        {
            return "claude";
        }

        var nativeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude",
            "claude-code");

        if (Directory.Exists(nativeRoot))
        {
            var latest = Directory.GetDirectories(nativeRoot)
                .Select(dir => Path.Combine(dir, "claude.exe"))
                .Where(File.Exists)
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .FirstOrDefault();

            if (latest is not null)
            {
                return latest;
            }
        }

        return "claude"; // se dejara fallar con un mensaje claro al ejecutar
    }

    private static bool OnPath(string command)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        var extensions = new[] { string.Empty, ".exe", ".cmd", ".bat" };
        return paths.Any(dir => extensions.Any(ext => File.Exists(Path.Combine(dir, command + ext))));
    }

    private static string Flatten(IReadOnlyList<ChatMessage> messages)
    {
        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            builder.AppendLine(message.Content).AppendLine();
        }

        return builder.ToString();
    }

    private static string ExtractResult(string stdout)
    {
        try
        {
            using var document = JsonDocument.Parse(stdout);
            var root = document.RootElement;

            var isError = root.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
            var text = root.TryGetProperty("result", out var result) ? result.GetString() : null;

            if (isError)
            {
                throw new ChatCompletionException($"claude: {text ?? "error desconocido"}");
            }

            if (text is not null)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // no vino en formato json; devolvemos lo crudo
        }

        return stdout;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // ya termino
        }
    }
}
