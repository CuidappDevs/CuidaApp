using CUIDAPP.Localization;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Trabajos
{
    public partial class FinalizarTrabajoPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private Trabajo? trabajo;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Trabajo", out var value) && value is Trabajo t)
            {
                trabajo = t;
                LblCliente.Text = Localizador.F("servicio_para", t.ClienteNombre);
                CardJustificacion.IsVisible = ServerClock.Now.TimeOfDay < t.HoraFin;
            }
        }

        public FinalizarTrabajoPage()
        {
            InitializeComponent();
        }

        // Casilla del PIN: borde azul al enfocar
        private void OnPinFocused(object? sender, FocusEventArgs e)
        {
            if ((sender as Element)?.Parent is Border b)
                b.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];
        }

        private void OnPinUnfocused(object? sender, FocusEventArgs e)
        {
            if ((sender as Element)?.Parent is Border b)
                b.Stroke = Colors.Transparent;
        }

        private void OnPin1Changed(object sender, TextChangedEventArgs e) => ManejarCambioDigito(EntryPin1, EntryPin2, e);
        private void OnPin2Changed(object sender, TextChangedEventArgs e) => ManejarCambioDigito(EntryPin2, EntryPin3, e, EntryPin1);
        private void OnPin3Changed(object sender, TextChangedEventArgs e) => ManejarCambioDigito(EntryPin3, EntryPin4, e, EntryPin2);
        private void OnPin4Changed(object sender, TextChangedEventArgs e) => ManejarCambioDigito(EntryPin4, null, e, EntryPin3);

        private void ManejarCambioDigito(Entry actual, Entry? siguiente, TextChangedEventArgs e, Entry? anterior = null)
        {
            if (!string.IsNullOrEmpty(e.NewTextValue) && actual.Parent is Border casilla)
            {
                _ = casilla.ScaleTo(1.08, 80, Easing.CubicOut).ContinueWith(_ =>
                    MainThread.BeginInvokeOnMainThread(() => casilla.ScaleTo(1, 140, Easing.CubicOut)));
            }
            LblError.IsVisible = false;

            var texto = e.NewTextValue ?? "";

            if (texto.Length > 1)
            {
                actual.Text = texto[^1..];
                return;
            }

            if (texto.Length == 1 && siguiente != null)
            {
                siguiente.Focus();
            }

            ActualizarEstadoBoton();
        }

        private void ActualizarEstadoBoton()
        {
            var pin = ObtenerPin();
            BtnConfirmar.IsEnabled = pin.Length == 4;
        }

        private string ObtenerPin()
        {
            return $"{EntryPin1.Text}{EntryPin2.Text}{EntryPin3.Text}{EntryPin4.Text}";
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnConfirmarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_encontro_la_informacion"), Localizador.T("ok"));
                return;
            }

            var pin = ObtenerPin();
            if (pin.Length != 4)
            {
                await Alerta.MostrarAsync(Localizador.T("codigo_incompleto"), Localizador.T("ingresa_los_4_digitos_del"), Localizador.T("ok"));
                return;
            }

            if (CardJustificacion.IsVisible && string.IsNullOrWhiteSpace(EditorJustificacion.Text))
            {
                await Alerta.MostrarAsync(Localizador.T("falta_la_justificacion"), Localizador.T("explicale_al_cliente_por_que"), Localizador.T("ok"));
                return;
            }

            BtnConfirmar.IsEnabled = false;
            BtnConfirmar.Text = Localizador.T("verificando");
            LblError.IsVisible = false;

            try
            {
                var justificacion = CardJustificacion.IsVisible ? EditorJustificacion.Text?.Trim() : null;
                var (success, error) = await _apiService.FinalizarTrabajoAsync(trabajo.Id, pin, justificacion);

                if (success)
                {
                    DeadManService.Detener();
                    await Alerta.MostrarAsync(Localizador.T("enviado"), Localizador.T("le_avisamos_al_cliente_para"), Localizador.T("ok"));
                    await Shell.Current.GoToAsync("../..");
                    return;
                }

                LblError.Text = string.IsNullOrWhiteSpace(error)
                    ? Localizador.T("el_codigo_no_es_correcto")
                    : error;
                LblError.IsVisible = true;
                EntryPin1.Text = EntryPin2.Text = EntryPin3.Text = EntryPin4.Text = "";
                EntryPin1.Focus();
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error_inesperado"), Localizador.F("no_se_pudo_verificar_el", ex.Message), Localizador.T("ok"));
            }
            finally
            {
                BtnConfirmar.Text = Localizador.T("confirmar_finalizacion");
                ActualizarEstadoBoton();
            }
        }
    }
}
