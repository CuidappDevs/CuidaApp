using CUIDAPP.Localization;
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

                Color R(string k) => (Color)Application.Current!.Resources[k];
                var primario = R("ColorPrimary");
                var radio = new Border
                {
                    Stroke = esSeleccionado ? primario : R("ColorBorder"),
                    StrokeThickness = 2,
                    BackgroundColor = Tema.C("ColorSurface"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    WidthRequest = 22,
                    HeightRequest = 22,
                    VerticalOptions = LayoutOptions.Center,
                    Content = esSeleccionado ? new Microsoft.Maui.Controls.Shapes.Ellipse { Fill = primario, WidthRequest = 10, HeightRequest = 10, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center } : null
                };
                var fila = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
                fila.Add(radio, 0);
                fila.Add(new Label { Text = Localizador.D(motivo.Descripcion), FontSize = 15, FontFamily = esSeleccionado ? "OpenSansSemibold" : "OpenSansRegular", TextColor = esSeleccionado ? primario : R("ColorTextStrong"), VerticalOptions = LayoutOptions.Center }, 1);
                var card = new Border
                {
                    Stroke = esSeleccionado ? primario : Colors.Transparent,
                    StrokeThickness = 2,
                    BackgroundColor = esSeleccionado ? Tema.C("ColorPrimarySoft") : Tema.C("ColorSurface"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
                    Padding = new Thickness(16, 14),
                    Content = fila
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

            var confirmar = await Alerta.MostrarAsync(Localizador.T("confirmar_cancelacion"), Localizador.T("seguro_que_deseas_cancelar_este"), Localizador.T("si_cancelar"), Localizador.T("no"));
            if (!confirmar)
                return;

            BtnConfirmar.IsEnabled = false;
            BtnConfirmar.Text = Localizador.T("cancelando");

            var esOtro = motivoSeleccionado.Descripcion.Equals("Otro", StringComparison.OrdinalIgnoreCase);
            var textoMotivo = esOtro ? EntryOtroMotivo.Text?.Trim() : null;

            var success = await _apiService.CancelarTrabajoCuidadorAsync(trabajo.Id, motivoSeleccionado.Id, textoMotivo);

            if (success)
            {
                DeadManService.Detener();
                await Shell.Current.GoToAsync("../..");
            }
            else
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_cancelar_el"), Localizador.T("ok"));
                BtnConfirmar.Text = Localizador.T("confirmar_cancelacion");
                BtnConfirmar.IsEnabled = true;
            }
        }
    }
}
