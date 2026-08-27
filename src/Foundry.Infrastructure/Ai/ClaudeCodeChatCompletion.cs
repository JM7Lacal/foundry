using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Foundry.Application.Ai;

namespace Foundry.Infrastructure.Ai;

/// <summary>
/// Usa el CLI de Claude Code (<c>claude -p</c>) como modelo: aprovecha la suscripcion del
/// desarrollador, sin costo por llamada ni setup extra. Solo para uso individual.
/// </summary>
public sealed class ClaudeCodeChatCompletion : IChatCompletion
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    public string Name => "claude-code";

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetTempPath(),
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("claude");
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
            throw new ChatCompletionException("No se pudo ejecutar el CLI 'claude'. ¿Esta en el PATH?", ex);
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
            if (document.RootElement.TryGetProperty("result", out var result) && result.GetString() is { } text)
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
