using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Views.Contratos
{
    /// <summary>
    /// "Invitar a mi cuidadora": el cliente (con permiso para contratar) invita a la persona que ya
    /// trabaja con su familia fuera de CuidApp. Se genera un código de 8 letras que ella escribe al
    /// registrarse como Care Partner; pasa la misma verificación que todos y luego el cliente le
    /// propone el contrato fijo con su antigüedad ("Trabaja con nosotros desde").
    /// </summary>
    public partial class InvitarCuidadoraPage : ContentPage
    {
        private readonly ApiService _api = new();
        private int usuarioId;
        private bool pintado;

        private readonly Entry txtNombre = new() { MaxLength = 120, Keyboard = Keyboard.Text };
        private readonly Entry txtTelefono = new() { MaxLength = 20, Keyboard = Keyboard.Telephone };
        private readonly Entry txtEmail = new() { MaxLength = 150, Keyboard = Keyboard.Email };
        private readonly Switch swAntiguedad = new() { IsToggled = true, VerticalOptions = LayoutOptions.Center };
        private readonly DatePicker dpDesde = new() { Format = "D" };

        public InvitarCuidadoraPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            txtNombre.Placeholder = Localizador.T("invitar_nombre_placeholder");
            txtTelefono.Placeholder = Localizador.T("invitar_telefono_placeholder");
            txtEmail.Placeholder = Localizador.T("invitar_email_placeholder");
            foreach (var e in new[] { txtNombre, txtTelefono, txtEmail })
            {
                e.TextColor = Tema.C("ColorTextStrong");
                e.PlaceholderColor = Tema.C("ColorPlaceholder");
            }
            swAntiguedad.OnColor = Tema.C("ColorPrimary");
            swAntiguedad.Toggled += (_, e) => dpDesde.IsVisible = e.Value;
            dpDesde.TextColor = Tema.C("ColorTextStrong");
            dpDesde.MaximumDate = DateTime.Today;
            dpDesde.MinimumDate = DateTime.Today.AddYears(-60);
            dpDesde.Date = DateTime.Today.AddYears(-1);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            usuarioId = Preferences.Default.Get("UserId", 0);
            if (pintado) return;
            pintado = true;

            var filaAntiguedad = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 12 };
            filaAntiguedad.Add(Texto(Localizador.T("invitar_ya_trabaja")).Centrado(), 0);
            filaAntiguedad.Add(swAntiguedad, 1);

            Contenido.Add(Tarjeta(
                Titulo(Localizador.T("invitar_como_funciona")),
                Texto(Localizador.T("invitar_paso_1"), true),
                Texto(Localizador.T("invitar_paso_2"), true),
                Texto(Localizador.T("invitar_paso_3"), true)));

            Contenido.Add(Tarjeta(
                Titulo(Localizador.T("invitar_datos")),
                Texto(Localizador.T("invitar_nombre"), false, 13), CampoTexto(txtNombre),
                Texto(Localizador.T("invitar_telefono"), false, 13), CampoTexto(txtTelefono),
                Texto(Localizador.T("invitar_email"), false, 13), CampoTexto(txtEmail),
                Texto(Localizador.T("invitar_contacto_nota"), true, 12)));

            Contenido.Add(Tarjeta(
                Titulo(Localizador.T("invitar_antiguedad")),
                filaAntiguedad,
                Texto(Localizador.T("invitar_desde"), false, 13), CampoTexto(dpDesde),
                Texto(Localizador.T("invitar_antiguedad_nota"), true, 12)));

            Contenido.Add(Tarjeta(
                Boton(Localizador.T("invitar_crear"), TipoBoton.Principal, CrearAsync),
                Texto(Localizador.T("invitar_verificacion_nota"), true, 12)));
            Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });

            Cargando.IsVisible = Cargando.IsRunning = false;
            Desplazamiento.IsVisible = true;
        }

        private async Task CrearAsync()
        {
            var nombre = txtNombre.Text?.Trim() ?? "";
            var telefono = string.IsNullOrWhiteSpace(txtTelefono.Text) ? null : txtTelefono.Text.Trim();
            var email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim();
            if (nombre.Length < 2)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("invitar_falta_nombre"), Localizador.T("ok"));
                return;
            }
            if (telefono == null && email == null)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("contrato_err_contacto_requerido"), Localizador.T("ok"));
                return;
            }

            var desde = swAntiguedad.IsToggled ? dpDesde.Date?.Date : null;
            var (inv, error) = await _api.CrearInvitacionCuidadoraAsync(usuarioId, nombre, telefono, email, desde);
            if (inv == null)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok"));
                return;
            }

            await CompartirAsync(nombre, inv.Codigo);
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>Abre el menú de compartir (WhatsApp, SMS, correo…) con el código y las instrucciones.</summary>
        public static async Task CompartirAsync(string nombre, string codigo)
        {
            try
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = Localizador.T("invitar_compartir_titulo"),
                    Text = Localizador.F("invitar_mensaje", nombre.Split(' ')[0], codigo)
                });
            }
            catch
            {
                await Alerta.MostrarAsync(Localizador.T("invitar_codigo_titulo"), Localizador.F("invitar_codigo_texto", codigo), Localizador.T("ok"));
            }
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
