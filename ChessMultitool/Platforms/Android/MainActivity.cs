using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace ChessMultitool;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density
)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Changer la couleur de la barre d'état (Status Bar)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#212121")); // Remplace par la couleur que tu veux
        }

        // Optionnel : Changer la couleur des icônes de la Status Bar (true = icônes noires, false = icônes blanches)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            WindowInsetsControllerCompat wic = new(Window, Window.DecorView);
            wic.AppearanceLightStatusBars = false; // false = icônes blanches, true = icônes noires
        }
    }
}


