namespace CUIDAPP.Helpers
{
    /// <summary>
    /// Color de la barra de estado del sistema (Android). Las pantallas con encabezado de color
    /// (splash, login) la pintan igual que su encabezado; el resto usa la barra blanca por defecto.
    /// </summary>
    public static class BarraEstado
    {
        public static readonly Color AzulEncabezado = Color.FromArgb("#2A63B8");

        public static void Azul() => Establecer(AzulEncabezado, iconosOscuros: false);

        // "Blanca" = color de superficie del tema (blanca en claro, oscura en oscuro).
        public static void Blanca() => Establecer(Services.Tema.C("ColorSurface"), iconosOscuros: !Services.Tema.EsOscuro);

        /// <summary>Alto de la barra de estado en unidades MAUI (0 fuera de Android).</summary>
        public static double Alto() => DimensionSistema("status_bar_height");

        /// <summary>Alto de la barra de navegación / gestos inferior (0 fuera de Android).</summary>
        public static double AltoInferior() => DimensionSistema("navigation_bar_height");

        private static double DimensionSistema(string nombre)
        {
#if ANDROID
            var ctx = Platform.CurrentActivity ?? Android.App.Application.Context;
            var id = ctx.Resources?.GetIdentifier(nombre, "dimen", "android") ?? 0;
            if (id > 0)
                return ctx.Resources!.GetDimensionPixelSize(id) / DeviceDisplay.Current.MainDisplayInfo.Density;
#endif
            return 0;
        }

        public static void Establecer(Color color, bool iconosOscuros)
        {
#if ANDROID
            var ventana = Platform.CurrentActivity?.Window;
            if (ventana == null) return;
            ventana.SetStatusBarColor(Android.Graphics.Color.ParseColor(color.ToArgbHex()));
            var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(ventana, ventana.DecorView);
            if (controller != null)
                controller.AppearanceLightStatusBars = iconosOscuros;
#endif
        }
    }
}
