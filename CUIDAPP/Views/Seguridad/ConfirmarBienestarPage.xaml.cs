using CUIDAPP.Localization;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Seguridad
{
    /// <summary>Pantalla de confirmación de bienestar: cuenta regresiva de 30 s tras detectar una posible caída.</summary>
    public partial class ConfirmarBienestarPage : ContentPage
    {
        private bool _terminada;

        public ConfirmarBienestarPage()
        {
            InitializeComponent();
            LblSegundos.Text = Math.Max(DeadManService.SegundosRestantes, 0).ToString();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            DeadManService.Tick += OnTick;
            DeadManService.Finalizada += OnFinalizada;

            // Si la cuenta ya terminó mientras la app estaba en segundo plano, no hay nada que confirmar.
            if (!DeadManService.CuentaRegresivaActiva && !_terminada)
                _ = CerrarAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            DeadManService.Tick -= OnTick;
            DeadManService.Finalizada -= OnFinalizada;
            DeadManService.PaginaVisible = false;
        }

        // No se puede salir con el botón "atrás" mientras corre la cuenta: hay que confirmar.
        protected override bool OnBackButtonPressed() => true;

        private void OnTick(int segundos) => LblSegundos.Text = segundos.ToString();

        private void OnFinalizada(ResultadoDeadMan resultado)
        {
            _terminada = true;

            if (resultado == ResultadoDeadMan.Confirmada)
            {
                _ = CerrarAsync();
                return;
            }

            // Se agotó el tiempo: se muestra qué pasó y el botón pasa a cerrar la pantalla.
            PanelCuenta.IsVisible = false;
            PanelResultado.IsVisible = true;
            BtnBien.Text = Localizador.T("dm_cerrar");

            if (resultado == ResultadoDeadMan.AlertaEnviada)
            {
                LblIconoResultado.Text = "🚨";
                LblTituloResultado.Text = Localizador.T("alerta_enviada");
                LblTextoResultado.Text = Localizador.T("dm_enviada_texto");
            }
            else
            {
                LblIconoResultado.Text = "⚠️";
                LblTituloResultado.Text = Localizador.T("error");
                LblTextoResultado.Text = Localizador.T("dm_error_envio");
            }
        }

        private async void OnEstoyBienClicked(object sender, EventArgs e)
        {
            if (_terminada)
            {
                await CerrarAsync();
                return;
            }

            DeadManService.Confirmar();
        }

        private async Task CerrarAsync()
        {
            try { await Navigation.PopModalAsync(); }
            catch (Exception ex) { Console.WriteLine($"[DeadMan] No se pudo cerrar la pantalla: {ex.Message}"); }
        }
    }
}
