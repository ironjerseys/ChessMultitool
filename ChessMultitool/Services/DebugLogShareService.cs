#if DEBUG
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ChessMultitool.Services;

public static class DebugLogShareService
{
    public static async Task ShareAsync()
    {
        var path = DebugFileLogger.LogPath;

        if (!File.Exists(path))
        {
            await Shell.Current.DisplayAlert("Logs", "Aucun fichier de log trouvé.", "OK");
            return;
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "AI chess debug log",
            File = new ShareFile(path)
        });
    }
}
#endif
