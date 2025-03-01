using ChessMultitool.Models;
using System.Text.Json;

namespace ChessMultitool;

public static class OpeningService
{
    public static async Task<OpeningsData> LoadOpeningsAsync()
    {
        // Assurez-vous que le fichier JSON (ici "data/openings.json") est bien inclus dans vos assets
        using var stream = await FileSystem.OpenAppPackageFileAsync("Data/openings.json");
        using var reader = new StreamReader(stream);
        string json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<OpeningsData>(json);
    }
}
