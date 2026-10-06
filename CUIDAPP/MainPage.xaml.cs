using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Auth;
using CUIDAPP.Services;

namespace CUIDAPP
{
    public partial class MainPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        /// <summary>
        /// true mientras la app abre directo el panel de una sesión guardada: el login se
        /// mantiene invisible para que no se vea un instante antes del panel.
        /// </summary>
        public static bool OcultarAlAbrir;

        public MainPage()
        {
            InitializeComponent();

            // Estado inicial antes del primer frame: formulario fuera de pantalla para que la
            // transición desde el splash sea continua (mismo azul).
            FormSheet.SizeChanged += (_, _) => TransicionHoja.AjustarAlto(Encabezado, FormSheet, Height, 260 + BarraEstado.Alto());
            TransicionHoja.Preparar(FormSheet, LogoBadge, FormContenido);

            // El encabezado empieza debajo de la barra de estado (la app es borde a borde).
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
        }

        // La pantalla ocupa al menos el alto visible: el encabezado toma el espacio sobrante
        // y el formulario queda pegado abajo. En pantallas chicas se hace scroll.
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            TransicionHoja.AjustarAlto(Encabezado, FormSheet, height, 260 + BarraEstado.Alto());
        }

        private bool _primeraVez = true;
        private bool _saliendo;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Content.Opacity = OcultarAlAbrir ? 0 : 1;
            if (OcultarAlAbrir)
                return;
            BarraEstado.Azul();
            _saliendo = false;
            IniciarBucle();

            // Al volver de otra pantalla (p. ej. recuperar contraseña), la hoja vuelve a subir.
            if (!_primeraVez)
                TransicionHoja.Preparar(FormSheet, LogoBadge, FormContenido);
            _primeraVez = false;

            _ = SelectorIdiomaLogin.FadeTo(1, 400, Easing.CubicOut);
            await TransicionHoja.EntrarAsync(FormSheet, LogoBadge, FormContenido);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            DetenerBucle();
        }

        /// <summary>Navega con la transición de hoja en lugar del deslizamiento del sistema.</summary>
        private async Task NavegarConHojaAsync(string ruta)
        {
            if (_saliendo) return;
            _saliendo = true;
            await TransicionHoja.SalirAsync(FormSheet, LogoBadge, FormContenido);
            await Shell.Current.GoToAsync(ruta, animate: false);
        }

        // Movimiento ambiental sutil: el logo flota y los círculos respiran.
        private void IniciarBucle()
        {
            new Animation(t => LogoBadge.TranslationY = -4 * Math.Sin(t * Math.PI * 2), 0, 1)
                .Commit(this, "BucleLogo", length: 4000, easing: Easing.Linear, repeat: () => true);

            new Animation(t =>
            {
                var s = Math.Sin(t * Math.PI * 2);
                CirculoA.Scale = 1 + 0.06 * s;
                CirculoA.TranslationX = -6 * s;
                CirculoB.Scale = 1 - 0.05 * s;
                CirculoB.TranslationY = -8 * s;
            }, 0, 1).Commit(this, "BucleCirculos", length: 9000, easing: Easing.Linear, repeat: () => true);
        }

        private void DetenerBucle()
        {
            this.AbortAnimation("BucleLogo");
            this.AbortAnimation("BucleCirculos");
        }

        // Borde resaltado al enfocar un campo
        private void OnEntryFocused(object? sender, FocusEventArgs e)
        {
            if ((sender as Element)?.Parent?.Parent is Border borde)
            {
                borde.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];
                borde.BackgroundColor = (Color)Application.Current!.Resources["ColorSurface"];
            }
        }

        private void OnEntryUnfocused(object? sender, FocusEventArgs e)
        {
            if ((sender as Element)?.Parent?.Parent is Border borde)
            {
                borde.Stroke = (Color)Application.Current!.Resources["ColorBorder"];
                borde.BackgroundColor = (Color)Application.Current!.Resources["ColorBackground"];
            }
        }

        // Respuesta táctil del botón
        // Estado de carga del botón: se atenúa a gris azulado y el texto se cambia por un spinner.
        private void MostrarCargandoLogin(bool cargando)
        {
            BtnLogin.IsEnabled = !cargando;
            BtnLogin.Text = cargando ? "" : Localizador.T("iniciar_sesion");
            SemanticProperties.SetDescription(SpinnerLogin, Localizador.T("ingresando"));
            BtnLogin.BackgroundColor = cargando ? Color.FromArgb("#7F97BC") : (Color)Application.Current!.Resources["ColorPrimary"];
            SpinnerLogin.IsVisible = SpinnerLogin.IsRunning = cargando;

            if (cargando)
            {
                SpinnerLogin.Opacity = 0;
                SpinnerLogin.Scale = 0.6;
                _ = SpinnerLogin.FadeTo(1, 160, Easing.CubicOut);
                _ = SpinnerLogin.ScaleTo(1, 200, Easing.CubicOut);
            }
        }

        private void OnBotonPresionado(object? sender, EventArgs e) => _ = BtnLogin.ScaleTo(0.97, 100, Easing.CubicOut);

        private void OnBotonSoltado(object? sender, EventArgs e) => _ = BtnLogin.ScaleTo(1, 160, Easing.CubicOut);

        private async void OnRegisterTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("RegistroPage");
        }

        private async void OnForgotPasswordTapped(object sender, EventArgs e)
        {
            await NavegarConHojaAsync("ForgotPasswordPage");
        }

        private void OnTogglePasswordVisibility(object sender, EventArgs e)
        {
            EntryPassword.IsPassword = !EntryPassword.IsPassword;
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryEmail.Text) || string.IsNullOrWhiteSpace(EntryPassword.Text))
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("ingresa_tu_correo_y_contrasena"), Localizador.T("ok"));
                return;
            }

            MostrarCargandoLogin(true);

            try
            {
                var request = new LoginRequest
                {
                    Email = EntryEmail.Text.Trim(),
                    Password = EntryPassword.Text
                };

                var result = await _apiService.LoginAsync(request);

                if (result == null)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("credenciales_invalidas_o_no_se"), Localizador.T("ok"));
                    return;
                }

                Preferences.Default.Set("AuthToken", result.Token);
                Preferences.Default.Set("UserEmail", result.Email);
                Preferences.Default.Set("UserNombre", result.NombreCompleto ?? "");
                Preferences.Default.Set("UserFotoUrl", result.FotoUrl ?? "");
                Preferences.Default.Set("UserId", result.UserId);
                Preferences.Default.Set("RolId", result.RolId);
                Preferences.Default.Set("EstadoAprobacion", result.EstadoAprobacion ?? 0);

                _ = RealtimeService.ConectarAsync(result.UserId);
                _ = ServerClock.SincronizarAsync();
                ConexionServiceManager.Iniciar();

                switch (result.RolId)
                {
                    case 3: // Cuidador
                        if (result.EstadoAprobacion == 2)
                            await Shell.Current.GoToAsync("CuidadorDashboardPage");
                        else
                            await Shell.Current.GoToAsync("VerificacionPendientePage");
                        break;
                    case 2: // Cliente
                        await Shell.Current.GoToAsync("ClienteDashboardPage");
                        break;
                    default: // Admin u otro rol
                        await Alerta.MostrarAsync(Localizador.T("bienvenido"), Localizador.T("inicio_de_sesion_exitoso"), Localizador.T("ok"));
                        break;
                }
            }
            finally
            {
                MostrarCargandoLogin(false);
            }
        }
    }
}
