using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace CUIDAPP
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // La app siempre tiene fondo claro; sin esto, en dispositivos reales la
            // barra de estado a veces se pinta blanca con íconos también blancos
            // (invisibles), aunque en el emulador se vea bien con íconos oscuros.
            if (Window != null)
            {
                Window.SetStatusBarColor(Android.Graphics.Color.White);
                var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
                if (controller != null)
                    controller.AppearanceLightStatusBars = true;
            }

            // Android 13+ exige permiso explícito en tiempo de ejecución para mostrar
            // cualquier notificación del sistema, aunque ya esté declarado en el manifest.
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.PostNotifications) != Android.Content.PM.Permission.Granted)
                {
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(this, new[] { Android.Manifest.Permission.PostNotifications }, 100);
                }
            }
        }
    }
}
