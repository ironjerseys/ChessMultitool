#if DEBUG
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;

namespace ChessMultitool.Services;

public static class DebugFileLogger
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    public static string LogPath =>
        Path.Combine(FileSystem.AppDataDirectory, "ai_chess_debug.log");

    private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static async Task AppendJsonLineAsync<T>(T entry, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(entry, options);

        await gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(FileSystem.AppDataDirectory);
            await File.AppendAllTextAsync(LogPath, line + Environment.NewLine, ct);
        }
        finally
        {
            gate.Release();
        }
    }
}
#endif
