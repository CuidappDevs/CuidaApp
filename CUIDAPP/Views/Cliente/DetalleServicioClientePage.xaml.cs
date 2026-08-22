using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class DetalleServicioClientePage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private TrabajoCliente? trabajo;
        private bool relojIniciado;
        private int trabajoId;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("TrabajoId", out var value) && value is int id)
                trabajoId = id;
        }

        public DetalleServicioClientePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoTiempoReal;
            RealtimeService.ActividadAgregada += OnActividadAgregadaTiempoReal;
            RealtimeService.AlertaGeocerca += OnAlertaGeocercaTiempoReal;
            IniciarRelojSiHaceFalta();
            await CargarTrabajo();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
            RealtimeService.ActividadAgregada -= OnActividadAgregadaTiempoReal;
            RealtimeService.AlertaGeocerca -= OnAlertaGeocercaTiempoReal;
        }

        private async void OnAlertaGeocercaTiempoReal(int idTrabajo, double distanciaMetros)
        {
            if (idTrabajo != trabajoId)
                return;

            await DisplayAlert("⚠️ Tu cuidador se alejó del sitio", $"Se alejó {distanciaMetros:N0} m del domicilio donde se está realizando el servicio.", "Entendido");
        }

        private async void OnTrabajoActualizadoTiempoReal(int idActualizado, int estado)
        {
            if (idActualizado != trabajoId)
                return;

            await CargarTrabajo();
        }

        private void OnActividadAgregadaTiempoReal(int idActividad, string descripcion, DateTime fechaHora)
        {
            if (idActividad != trabajoId)
                return;

            LblSinActividadesCliente.IsVisible = false;
            ListaActividadesCliente.Insert(0, CrearTarjetaActividad(descripcion, fechaHora));
        }

        private void IniciarRelojSiHaceFalta()
        {
            if (relojIniciado)
                return;
            relojIniciado = true;

            // Reloj puramente local (sin red): solo recalcula el texto en base a la hora
            // real de inicio que ya tenemos, no consulta nada al servidor.
            Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                if (trabajo?.Estado == 3 && trabajo.FechaInicioReal.HasValue)
                {
                    var transcurrido = DateTime.Now - trabajo.FechaInicioReal.Value;
                    if (transcurrido < TimeSpan.Zero)
                        transcurrido = TimeSpan.Zero;
                    LblTiempoTranscurrido.Text = transcurrido.ToString(@"hh\:mm\:ss");
                }
                return true;
            });
        }

        private async Task CargarTrabajo()
        {
            if (trabajoId == 0)
                return;

            LoadingIndicator.IsRunning = true;
            ContenedorInfo.IsVisible = false;

            trabajo = await _apiService.ObtenerTrabajoClientePorIdAsync(trabajoId);

            LoadingIndicator.IsRunning = false;

            if (trabajo == null)
                return;

            ContenedorInfo.IsVisible = true;
            Renderizar(trabajo);
        }

        private void Renderizar(TrabajoCliente t)
        {
            LblCuidadorNombre.Text = t.CuidadorNombre;
            LblTipoServicio.Text = t.TipoServicio;

            if (!string.IsNullOrWhiteSpace(t.CuidadorFotoUrl))
                ImgCuidador.Source = $"{ApiService.ServerOrigin}{t.CuidadorFotoUrl}";

            LblFechaHora.Text = $"{t.Fecha:dddd, d 'de' MMMM} · {FormatearHora(t.HoraInicio)} - {FormatearHora(t.HoraFin)}";
            LblDireccion.Text = string.IsNullOrWhiteSpace(t.Direccion) ? "Sin dirección" : t.Direccion;
            LblPago.Text = $"RD${t.Tarifa:N2}";

            var (colorFondo, colorTexto, texto) = t.Estado switch
            {
                1 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), "Esperando respuesta del cuidador"),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), "Aceptado, tu cuidador asistirá en la fecha programada"),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), "En progreso"),
                4 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), "Servicio completado"),
                5 => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), "Cancelado"),
                6 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"), "Rechazado por el cuidador"),
                _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), "Desconocido")
            };
            BadgeEstado.BackgroundColor = colorFondo;
            DotEstado.BackgroundColor = colorTexto;
            LblEstado.TextColor = colorTexto;
            LblEstado.Text = texto;

            BtnCancelar.IsVisible = t.Estado == 1;
            BtnCalificar.IsVisible = t.Estado == 4;

            CardPin.IsVisible = t.Estado == 2 && !string.IsNullOrWhiteSpace(t.PinInicio);
            if (CardPin.IsVisible)
                LblPin.Text = t.PinInicio;

            CardEnProgreso.IsVisible = t.Estado == 3 && !string.IsNullOrWhiteSpace(t.PinFin);
            if (CardEnProgreso.IsVisible)
                LblPinFin.Text = t.PinFin;

            CardActividadesCliente.IsVisible = t.Estado == 3;
            if (CardActividadesCliente.IsVisible)
                _ = CargarActividadesAsync();

            BtnChat.IsVisible = t.Estado is 2 or 3;

            RenderizarPasos(t.Estado);
        }

        private async void OnChatClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", trabajo.Id },
                { "OtroNombre", trabajo.CuidadorNombre },
                { "OtroFotoUrl", trabajo.CuidadorFotoUrl ?? "" }
            };
            await Shell.Current.GoToAsync("ChatPage", parametros);
        }

        private async Task CargarActividadesAsync()
        {
            if (trabajo == null)
                return;

            var actividades = await _apiService.ObtenerActividadesAsync(trabajo.Id);

            ListaActividadesCliente.Clear();
            LblSinActividadesCliente.IsVisible = actividades.Count == 0;

            foreach (var actividad in actividades.OrderByDescending(a => a.FechaHora))
                ListaActividadesCliente.Add(CrearTarjetaActividad(actividad.Descripcion, actividad.FechaHora));
        }

        private static View CrearTarjetaActividad(string descripcion, DateTime fechaHora)
        {
            return new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(14, 10),
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label { Text = descripcion, FontSize = 14, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#111827") },
                        new Label { Text = fechaHora.ToString("h:mm tt"), FontSize = 11, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#9CA3AF") }
                    }
                }
            };
        }

        private void RenderizarPasos(int estado)
        {
            ListaPasos.Clear();

            var pasos = new List<(string Texto, bool Completado)>
            {
                ("Solicitud enviada", true),
                ("Cuidador aceptó", estado >= 2 && estado != 6),
                ("Servicio en progreso", estado >= 3 && estado != 6 && estado != 5),
                ("Servicio completado", estado == 4)
            };

            if (estado == 6)
                pasos = new List<(string, bool)> { ("Solicitud enviada", true), ("Rechazado por el cuidador", true) };
            else if (estado == 5)
                pasos = new List<(string, bool)> { ("Solicitud enviada", true), ("Cancelado", true) };

            foreach (var (texto, completado) in pasos)
            {
                var punto = new Border
                {
                    Stroke = Colors.Transparent,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                    BackgroundColor = completado ? Color.FromArgb("#5A31F4") : Color.FromArgb("#E5E7EB"),
                    WidthRequest = 12,
                    HeightRequest = 12,
                    VerticalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };

                var label = new Label
                {
                    Text = texto,
                    FontSize = 14,
                    FontFamily = completado ? "OpenSansSemibold" : "OpenSansRegular",
                    TextColor = completado ? Color.FromArgb("#111827") : Color.FromArgb("#9CA3AF"),
                    VerticalOptions = LayoutOptions.Center
                };

                var fila = new HorizontalStackLayout { Spacing = 0, Children = { punto, label }, Margin = new Thickness(0, 8) };
                ListaPasos.Add(fila);
            }
        }

        private static string FormatearHora(TimeSpan hora)
        {
            return DateTime.Today.Add(hora).ToString("h:mm tt");
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnCalificarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", trabajo.Id },
                { "CalificadoId", trabajo.CuidadorId },
                { "CalificadoNombre", trabajo.CuidadorNombre }
            };
            await Shell.Current.GoToAsync("CalificarPage", parametros);
        }

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var confirmar = await DisplayAlert("Cancelar solicitud", "¿Seguro que deseas cancelar esta solicitud de servicio?", "Sí, cancelar", "No");
            if (!confirmar)
                return;

            BtnCancelar.IsEnabled = false;
            BtnCancelar.Text = "Cancelando...";

            var success = await _apiService.ActualizarEstadoTrabajoAsync(trabajo.Id, 5);

            if (success)
            {
                await CargarTrabajo();
            }
            else
            {
                await DisplayAlert("Error", "No se pudo cancelar la solicitud. Intenta de nuevo.", "OK");
                BtnCancelar.IsEnabled = true;
                BtnCancelar.Text = "Cancelar solicitud";
            }
        }
    }
}
