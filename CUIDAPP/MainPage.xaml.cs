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
            SeparadorGoogle.IsVisible = BtnGoogle.IsVisible = GoogleLogin.Disponible;

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

                var loginResult = await _apiService.LoginAsync(request);

                // Cuenta en eliminación (plazo de 30 días): se ofrece recuperarla y, si acepta, se entra.
                if (loginResult.Status == LoginStatus.PendingDeletion)
                {
                    if (!await RecuperarCuentaAsync(request, loginResult.FechaEliminacion))
                        return;
                    loginResult = await _apiService.LoginAsync(request);
                }

                if (loginResult.Status != LoginStatus.Success || loginResult.Auth == null)
                {
                    await MostrarFalloLoginAsync(loginResult);
                    return;
                }

                await EntrarAsync(loginResult.Auth);
            }
            finally
            {
                MostrarCargandoLogin(false);
            }
        }

        // ---------- Continuar con Google
        private bool googleOcupado;

        private async void OnGoogleTapped(object? sender, TappedEventArgs e)
        {
            if (googleOcupado) return;
            googleOcupado = true;
            _ = BtnGoogle.ScaleTo(0.97, 80, Easing.CubicOut).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => BtnGoogle.ScaleTo(1, 140, Easing.CubicOut)));
            try
            {
                var (resultado, idToken) = await GoogleLogin.ObtenerIdTokenAsync();
                switch (resultado)
                {
                    case GoogleLogin.Resultado.Cancelado:
                        return;
                    case GoogleLogin.Resultado.SinCuentas:
                        await Alerta.MostrarAsync(Localizador.T("login_google"), Localizador.T("login_google_sin_cuentas"), Localizador.T("ok"));
                        return;
                    case not GoogleLogin.Resultado.Ok:
                        await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("login_google_error"), Localizador.T("ok"), TipoAlerta.Error);
                        return;
                }

                MostrarCargandoGoogle(true);
                var login = await _apiService.LoginGoogleAsync(idToken!);
                switch (login.Status)
                {
                    case LoginStatus.Success when login.Auth != null:
                        await EntrarAsync(login.Auth);
                        break;
                    case LoginStatus.GoogleRegistroRequerido:
                        // Sin cuenta: registro normal con el correo y el nombre de Google; no pide contraseña.
                        await Shell.Current.GoToAsync("RegistroPage", new Dictionary<string, object>
                        {
                            { "GoogleIdToken", idToken! },
                            { "GoogleEmail", login.GoogleEmail ?? "" },
                            { "GoogleNombre", login.GoogleNombre ?? "" }
                        });
                        break;
                    case LoginStatus.PendingDeletion:
                        // Con Google no hay contraseña que confirmar: se recupera con "Olvidé mi contraseña".
                        var mensaje = login.FechaEliminacion is { } f
                            ? Localizador.F("login_google_eliminacion", Localizador.FechaLarga(f))
                            : Localizador.T("eliminar_recuperar_texto_sin_fecha");
                        await Alerta.MostrarAsync(Localizador.T("eliminar_recuperar_titulo"), mensaje, Localizador.T("ok"), TipoAlerta.Advertencia);
                        break;
                    case LoginStatus.GoogleInvalido:
                        await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("login_google_invalido"), Localizador.T("ok"), TipoAlerta.Error);
                        break;
                    default:
                        await MostrarFalloLoginAsync(login);
                        break;
                }
            }
            finally
            {
                MostrarCargandoGoogle(false);
                googleOcupado = false;
            }
        }

        private void MostrarCargandoGoogle(bool cargando)
        {
            BtnGoogle.IsEnabled = !cargando;
            ContenidoGoogle.IsVisible = !cargando;
            SpinnerGoogle.IsVisible = SpinnerGoogle.IsRunning = cargando;
        }

        /// <summary>Guarda la sesión y abre el inicio según el rol (login con correo o con Google).</summary>
        private async Task EntrarAsync(AuthResponse result)
        {
            Preferences.Default.Set("AuthToken", result.Token);
            Preferences.Default.Set("UserEmail", result.Email);
            Preferences.Default.Set("UserNombre", result.NombreCompleto ?? "");
            Preferences.Default.Set("UserFotoUrl", result.FotoUrl ?? "");
            Preferences.Default.Set("UserId", result.UserId);
            Preferences.Default.Set("RolId", result.RolId);
            Preferences.Default.Set("EstadoAprobacion", result.EstadoAprobacion ?? 0);

            _ = RealtimeService.ConectarAsync(result.UserId);
            _ = ServerClock.SincronizarAsync();
            _ = Recordatorios.SincronizarServiciosAsync();
            ConexionServiceManager.Iniciar();

            switch (result.RolId)
            {
                case 3: // Cuidador
                    // Sin aprobación formal pero con todos los documentos aprobados también entra a su panel.
                    if (result.EstadoAprobacion != 2 && (await _apiService.ObtenerEstadoVerificacionAsync(result.UserId))?.PuedeTrabajar == true)
                    {
                        result.EstadoAprobacion = 2;
                        Preferences.Default.Set("EstadoAprobacion", 2);
                    }

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
            await NotificacionDestino.ProcesarPendienteAsync();
        }

        private async Task<bool> RecuperarCuentaAsync(LoginRequest request, DateTime? fechaEliminacion)
        {
            var mensaje = fechaEliminacion is { } f
                ? Localizador.F("eliminar_recuperar_texto", Localizador.FechaLarga(f))
                : Localizador.T("eliminar_recuperar_texto_sin_fecha");
            if (!await Alerta.MostrarAsync(Localizador.T("eliminar_recuperar_titulo"), mensaje,
                    Localizador.T("eliminar_recuperar_boton"), Localizador.T("cancelar"), TipoAlerta.Pregunta))
                return false;

            var (ok, error) = await _apiService.CancelarEliminacionAsync(request.Email, request.Password);
            if (!ok)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("cuenta_err_generico"), Localizador.T("ok"));
                return false;
            }
            await Alerta.MostrarAsync(Localizador.T("eliminar_recuperada_titulo"), Localizador.T("eliminar_recuperada_texto"), Localizador.T("ok"), TipoAlerta.Exito);
            return true;
        }

        private Task MostrarFalloLoginAsync(LoginResult resultado)
        {
            switch (resultado.Status)
            {
                case LoginStatus.AccountSuspended when resultado.Suspension is { } suspension:
                    var fin = suspension.Tipo == "TEMPORAL" && suspension.FechaFinUtc.HasValue
                        ? Localizador.F("login_suspension_temporal", suspension.FechaFinUtc.Value.ToLocalTime().ToString("g", Localizador.Cultura))
                        : Localizador.T("login_suspension_indefinida");
                    return Alerta.MostrarAsync(Localizador.T("login_cuenta_suspendida"), Localizador.F("login_suspension_motivo", suspension.Motivo, fin), Localizador.T("ok"), TipoAlerta.Advertencia);
                case LoginStatus.AccountInactive:
                case LoginStatus.PendingDeletion:
                    return Alerta.MostrarAsync(Localizador.T("login_cuenta_no_disponible"), Localizador.T("login_cuenta_no_activa"), Localizador.T("ok"), TipoAlerta.Advertencia);
                case LoginStatus.InvalidCredentials:
                    return Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("login_credenciales_incorrectas"), Localizador.T("ok"), TipoAlerta.Error);
                case LoginStatus.Validation:
                    return Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("login_faltan_datos"), Localizador.T("ok"), TipoAlerta.Error);
                case LoginStatus.Network:
                    return Alerta.MostrarAsync(Localizador.T("login_sin_conexion"), Localizador.T("login_sin_conexion_texto"), Localizador.T("ok"), TipoAlerta.Error);
                default:
                    return Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("login_error_servidor"), Localizador.T("ok"), TipoAlerta.Error);
            }
        }
    }
}
