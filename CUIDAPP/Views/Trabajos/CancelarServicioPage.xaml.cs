using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Trabajos
{
    public partial class CancelarServicioPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private Trabajo? trabajo;
        private List<MotivoCancelacion> motivos = new();
        private MotivoCancelacion? motivoSeleccionado;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Trabajo", out var value) && value is Trabajo t)
                trabajo = t;
        }

        public CancelarServicioPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            motivos = await _apiService.ObtenerMotivosCancelacionAsync();
            RenderizarMotivos();
        }

        private void RenderizarMotivos()
        {
            ListaMotivos.Clear();

            foreach (var motivo in motivos)
            {
                var esSeleccionado = motivoSeleccionado?.Id == motivo.Id;

                var card = new Border
                {
                    Stroke = esSeleccionado ? Color.FromArgb("#5A31F4") : Color.FromArgb("#E5E7EB"),
                    StrokeThickness = esSeleccionado ? 2 : 1,
                    BackgroundColor = esSeleccionado ? Color.FromArgb("#F3E8FF") : Colors.White,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                    Padding = new Thickness(15, 13),
                    Content = new Label
                    {
                        Text = motivo.Descripcion,
                        FontSize = 14,
                        FontFamily = esSeleccionado ? "OpenSansSemibold" : "OpenSansRegular",
                        TextColor = esSeleccionado ? Color.FromArgb("#4024A2") : Color.FromArgb("#374151")
                    }
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => SeleccionarMotivo(motivo);
                card.GestureRecognizers.Add(tap);

                ListaMotivos.Add(card);
            }
        }

        private void SeleccionarMotivo(MotivoCancelacion motivo)
        {
            motivoSeleccionado = motivo;
            RenderizarMotivos();

            var esOtro = motivo.Descripcion.Equals("Otro", StringComparison.OrdinalIgnoreCase);
            ContenedorOtro.IsVisible = esOtro;

            ActualizarBoton();
        }

        private void OnOtroMotivoTextChanged(object sender, TextChangedEventArgs e) => ActualizarBoton();

        private void ActualizarBoton()
        {
            if (motivoSeleccionado == null)
            {
                BtnConfirmar.IsEnabled = false;
                return;
            }

            var esOtro = motivoSeleccionado.Descripcion.Equals("Otro", StringComparison.OrdinalIgnoreCase);
            BtnConfirmar.IsEnabled = !esOtro || !string.IsNullOrWhiteSpace(EntryOtroMotivo.Text);
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnConfirmarClicked(object sender, EventArgs e)
        {
            if (trabajo == null || motivoSeleccionado == null)
                return;

            var confirmar = await DisplayAlert("Confirmar cancelación", "¿Seguro que deseas cancelar este servicio? Esta acción no se puede deshacer.", "Sí, cancelar", "No");
            if (!confirmar)
                return;

            BtnConfirmar.IsEnabled = false;
            BtnConfirmar.Text = "Cancelando...";

            var esOtro = motivoSeleccionado.Descripcion.Equals("Otro", StringComparison.OrdinalIgnoreCase);
            var textoMotivo = esOtro ? EntryOtroMotivo.Text?.Trim() : null;

            var success = await _apiService.CancelarTrabajoCuidadorAsync(trabajo.Id, motivoSeleccionado.Id, textoMotivo);

            if (success)
            {
                await Shell.Current.GoToAsync("../..");
            }
            else
            {
                await DisplayAlert("Error", "No se pudo cancelar el servicio. Intenta de nuevo.", "OK");
                BtnConfirmar.Text = "Confirmar cancelación";
                BtnConfirmar.IsEnabled = true;
            }
        }
    }
}
