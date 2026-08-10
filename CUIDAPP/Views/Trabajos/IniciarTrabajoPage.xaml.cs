using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Trabajos
{
    public partial class IniciarTrabajoPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private Trabajo? trabajo;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Trabajo", out var value) && value is Trabajo t)
            {
                trabajo = t;
                LblCliente.Text = $"Servicio para {t.ClienteNombre}";
            }
        }

        public IniciarTrabajoPage()
        {
            InitializeComponent();
        }

        private void OnPin1Changed(object sender, TextChangedEventArgs e) => ManejarCambioDigito(EntryPin1, EntryPin2, e);
        private void OnPin2Changed(object sender, TextChangedEventArgs e) => ManejarCambioDigito(EntryPin2, EntryPin3, e, EntryPin1);
        private void OnPin3Changed(object sender, TextChangedEventArgs e) => ManejarCambioDigito(EntryPin3, EntryPin4, e, EntryPin2);
        private void OnPin4Changed(object sender, TextChangedEventArgs e) => ManejarCambioDigito(EntryPin4, null, e, EntryPin3);

        private void ManejarCambioDigito(Entry actual, Entry? siguiente, TextChangedEventArgs e, Entry? anterior = null)
        {
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
            else if (texto.Length == 0 && anterior != null && string.IsNullOrEmpty(e.OldTextValue))
            {
                // Nada que hacer: campo vacío al iniciar, evita mover foco espuriamente.
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
                return;

            var pin = ObtenerPin();
            if (pin.Length != 4)
                return;

            BtnConfirmar.IsEnabled = false;
            BtnConfirmar.Text = "Verificando...";
            LblError.IsVisible = false;

            var (success, error) = await _apiService.IniciarTrabajoAsync(trabajo.Id, pin);

            if (success)
            {
                await Shell.Current.GoToAsync("../..");
            }
            else
            {
                LblError.Text = "El código no es correcto. Verifícalo con el cliente e intenta de nuevo.";
                LblError.IsVisible = true;
                EntryPin1.Text = EntryPin2.Text = EntryPin3.Text = EntryPin4.Text = "";
                EntryPin1.Focus();
                BtnConfirmar.Text = "Confirmar inicio";
                BtnConfirmar.IsEnabled = false;
            }
        }
    }
}
