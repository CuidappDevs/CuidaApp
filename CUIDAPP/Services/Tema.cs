namespace CUIDAPP.Services
{
    public enum ModoTema { Claro, Oscuro, Sistema }

    /// <summary>
    /// Tema claro / oscuro de la app.
    /// - La paleta vive en Resources/Styles/Colors.xaml (valores claros) y aquí (valores oscuros):
    ///   al aplicar el tema se escriben los valores en ese diccionario, así que las pantallas que
    ///   se crean después ya usan los colores correctos ({StaticResource ColorX} o <see cref="C"/>).
    /// - Al cambiar de tema se recrea la interfaz (no hace falta que cada pantalla reaccione en vivo).
    /// - Nunca escribir colores fijos para fondos, textos o bordes: usar las claves Color* (ver AGENTS.md).
    /// </summary>
    public static class Tema
    {
        private const string Preferencia = "Tema";

        // Clave -> (claro, oscuro)
        private static readonly Dictionary<string, (string Claro, string Oscuro)> Paleta = new()
        {
            ["ColorPrimary"] = ("#1C4D96", "#3D74CF"),
            ["ColorPrimaryAlt"] = ("#215E99", "#4A82D6"),
            ["ColorPrimaryDark"] = ("#0F4761", "#9DBDEB"),
            ["ColorPrimaryLight"] = ("#4C94D8", "#6FAEE8"),
            ["ColorAccent"] = ("#0F9ED5", "#3DB8E8"),
            ["ColorTextStrong"] = ("#0A2F41", "#E7EDF4"),
            ["ColorTextMuted"] = ("#4B5563", "#9AA7B6"),
            ["ColorSurface"] = ("#FFFFFF", "#17202B"),
            ["ColorBackground"] = ("#F5F8FC", "#0D131A"),
            ["ColorBorder"] = ("#D9E2EC", "#2B3746"),
            ["ColorDanger"] = ("#C62828", "#EF6B6B"),
            ["ColorSuccess"] = ("#2E7D32", "#388E3C"),
            ["ColorWarning"] = ("#8A4B00", "#F2B45C"),
            ["ColorInfo"] = ("#156082", "#5BB0D6"),
            // Fondos suaves (chips, íconos, avisos) y neutros
            ["ColorPrimarySoft"] = ("#EAF1FB", "#1A2A42"),
            ["ColorSuccessSoft"] = ("#E3F4E8", "#15291B"),
            ["ColorDangerSoft"] = ("#FDECEC", "#351B1E"),
            ["ColorWarningSoft"] = ("#FFF4DC", "#33280F"),
            ["ColorSubtle"] = ("#EEF2F6", "#222D3A"),
            ["ColorPlaceholder"] = ("#8A97A6", "#6C7B8C"),
            ["ColorChevron"] = ("#B0BEC5", "#546273"),
            ["ColorShadow"] = ("#0A2F41", "#000000"),
        };

        public static ModoTema Modo
        {
            get => Enum.TryParse<ModoTema>(Preferences.Default.Get(Preferencia, nameof(ModoTema.Claro)), out var m) ? m : ModoTema.Claro;
            private set => Preferences.Default.Set(Preferencia, value.ToString());
        }

        public static bool EsOscuro { get; private set; }

        /// <summary>Estilo de Mapbox para los mapas (Leaflet) según el tema.</summary>
        public static string EstiloMapa => EsOscuro ? "dark-v11" : "light-v11";

        /// <summary>Color de la paleta actual (para colores armados por código).</summary>
        public static Color C(string clave)
            => Application.Current?.Resources.TryGetValue(clave, out var valor) == true && valor is Color c ? c : Colors.Magenta;

        /// <summary>Calcula el tema y escribe la paleta. Se llama al arrancar la app (antes de crear pantallas).</summary>
        public static void Aplicar(Application app)
        {
            EsOscuro = Modo == ModoTema.Oscuro || (Modo == ModoTema.Sistema && app.PlatformAppTheme == AppTheme.Dark);

            // Tema explícito: los controles nativos (Entry, Picker…) usan colores acordes.
            app.UserAppTheme = EsOscuro ? AppTheme.Dark : AppTheme.Light;

            var paleta = app.Resources.MergedDictionaries.FirstOrDefault(d => d.ContainsKey("ColorPrimary")) ?? app.Resources;
            foreach (var (clave, (claro, oscuro)) in Paleta)
                paleta[clave] = Color.FromArgb(EsOscuro ? oscuro : claro);
        }

        /// <summary>Cambia el tema y recrea la interfaz, volviendo a la pantalla indicada (p. ej. el perfil).</summary>
        public static async Task CambiarAsync(ModoTema modo, string? volverA = null)
        {
            var app = Application.Current;
            if (app == null || modo == Modo)
                return;

            Modo = modo;
            var antes = EsOscuro;
            Aplicar(app);
            if (antes != EsOscuro)
                await RecrearInterfazAsync(volverA);
        }

        /// <summary>Modo "Automático": el teléfono cambió entre claro y oscuro.</summary>
        public static async Task SistemaCambioAsync()
        {
            var app = Application.Current;
            if (app == null || Modo != ModoTema.Sistema)
                return;

            var antes = EsOscuro;
            Aplicar(app);
            if (antes != EsOscuro)
                await RecrearInterfazAsync(null);
        }

        private static async Task RecrearInterfazAsync(string? volverA)
        {
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window == null)
                return;

            var ruta = Helpers.SesionGuardada.RutaDeInicio();
            CUIDAPP.MainPage.OcultarAlAbrir = ruta != null;
            var shell = new AppShell();
            window.Page = shell;
            try
            {
                if (ruta != null)
                {
                    await shell.GoToAsync(ruta, animate: false);
                    if (!string.IsNullOrEmpty(volverA))
                        await shell.GoToAsync(volverA, animate: false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tema] No se pudo volver a la pantalla: {ex.Message}");
            }
            finally
            {
                CUIDAPP.MainPage.OcultarAlAbrir = false;
            }
        }
    }
}
