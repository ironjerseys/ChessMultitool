using ChessMultitool.Models;
using System.Text.Json;

namespace ChessMultitool;

public static class TrapService
{
    public static async Task<TrapsData> LoadTrapsAsync()
    {
        // Assurez-vous que le fichier JSON (ici "data/openings.json") est bien inclus dans vos assets
        using var stream = await FileSystem.OpenAppPackageFileAsync("Data/traps.json");
        using var reader = new StreamReader(stream);
        string json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<TrapsData>(json);
    }
}
