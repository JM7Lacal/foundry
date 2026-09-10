using System.Diagnostics;
using System.Text.Json;
using Foundry.Application.Ai;

namespace Foundry.Infrastructure.Ai;

/// <summary>
/// Listener de observabilidad: guarda las ultimas N <see cref="ChatCallReport"/> en memoria (para
/// un panel o un test) y, si se le da una ruta, va agregando una linea JSON por llamada a un
/// archivo (formato JSON Lines, comodo de grepear o cargar despues). Thread-safe.
/// </summary>
public sealed class ChatCallLog : IChatCallListener
{
    private static readonly JsonSerializerOptions LineFormat = new() { WriteIndented = false };

    private readonly int _capacity;
    private readonly string? _filePath;
    private readonly object _gate = new();
    private readonly Queue<ChatCallReport> _recent = new();

    public ChatCallLog(int capacity = 100, string? filePath = null)
    {
        _capacity = Math.Max(1, capacity);
        _filePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
    }

    /// <summary>Las llamadas mas recientes, de la mas vieja a la mas nueva.</summary>
    public IReadOnlyList<ChatCallReport> Recent
    {
        get
        {
            lock (_gate)
            {
                return _recent.ToArray();
            }
        }
    }

    public void OnCall(ChatCallReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        lock (_gate)
        {
            _recent.Enqueue(report);
            while (_recent.Count > _capacity)
            {
                _recent.Dequeue();
            }
        }

        AppendToFile(report);
    }

    private void AppendToFile(ChatCallReport report)
    {
        if (_filePath is null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_filePath, JsonSerializer.Serialize(report, LineFormat) + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Debug.WriteLine($"ChatCallLog no pudo escribir '{_filePath}': {ex.Message}");
        }
    }
}
