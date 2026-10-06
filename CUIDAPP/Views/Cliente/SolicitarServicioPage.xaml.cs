using CUIDAPP.Localization;
using CUIDAPP.Models.Busqueda;
using CUIDAPP.Models.Cliente;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class SolicitarServicioPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private CuidadorCercano? cuidador;
        private UbicacionCliente? ubicacionElegida;
        private readonly List<string> tareas = new();
        private const int MaxTareas = 20;

        private void OnAgregarTareaClicked(object sender, EventArgs e)
        {
            var texto = EntryTarea.Text?.Trim();
            if (string.IsNullOrEmpty(texto))
                return;

            if (tareas.Count >= MaxTareas)
            {
                _ = Alerta.MostrarAsync(Localizador.T("limite_alcanzado"), Localizador.F("puedes_agregar_hasta_tareas", MaxTareas), Localizador.T("ok"));
                return;
            }

            tareas.Add(texto);
            EntryTarea.Text = "";
            agregadaRecien = true;
            _ = BtnAgregarTarea.ScaleTo(0.85, 80, Easing.CubicOut).ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() => BtnAgregarTarea.ScaleTo(1, 160, Easing.CubicOut)));
            RenderizarTareas();
        }

        private void RenderizarTareas()
        {
            ListaTareas.Clear();
            foreach (var tarea in tareas.ToList())
            {
                var quitar = new Border
                {
                    Stroke = Colors.Transparent,
                    BackgroundColor = (Color)Application.Current!.Resources["ColorBackground"],
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    WidthRequest = 32,
                    HeightRequest = 32,
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Microsoft.Maui.Controls.Shapes.Path
                    {
                        Data = Geo("M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z"),
                        Fill = (Color)Application.Current!.Resources["ColorTextMuted"],
                        Aspect = Stretch.Uniform, WidthRequest = 12, HeightRequest = 12,
                        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                    }
                };

                var check = new Border
                {
                    Stroke = (Color)Application.Current!.Resources["ColorPrimaryLight"],
                    StrokeThickness = 2,
                    BackgroundColor = Colors.White,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    WidthRequest = 22,
                    HeightRequest = 22,
                    VerticalOptions = LayoutOptions.Center
                };

                var fila = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                fila.Add(check, 0);
                fila.Add(new Label { Text = tarea, FontSize = 14, FontFamily = "OpenSansRegular", TextColor = (Color)Application.Current!.Resources["ColorTextStrong"], VerticalOptions = LayoutOptions.Center }, 1);
                fila.Add(quitar, 2);

                var tarjeta = new Border
                {
                    Stroke = Colors.Transparent,
                    BackgroundColor = Colors.White,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                    Padding = new Thickness(14, 8, 8, 8),
                    Content = fila
                };

                quitar.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        // Sale deslizándose antes de quitarse de la lista
                        await Task.WhenAll(tarjeta.FadeTo(0, 140, Easing.CubicOut), tarjeta.TranslateTo(24, 0, 140, Easing.CubicOut));
                        tareas.Remove(tarea);
                        RenderizarTareas();
                    })
                });

                ListaTareas.Add(tarjeta);
            }

            // La última tarea agregada entra con animación
            if (ListaTareas.Count > 0 && ListaTareas[^1] is VisualElement ultima && agregadaRecien)
            {
                ultima.Opacity = 0;
                ultima.TranslationY = -8;
                _ = ultima.FadeTo(1, 220, Easing.CubicOut);
                _ = ultima.TranslateTo(0, 0, 260, Easing.CubicOut);
            }
            agregadaRecien = false;
        }

        private bool agregadaRecien;

        private static Microsoft.Maui.Controls.Shapes.Geometry Geo(string d) =>
            (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(d)!;

        private void OnTareaFocused(object? sender, FocusEventArgs e) => BordeTarea.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];

        private void OnTareaUnfocused(object? sender, FocusEventArgs e) => BordeTarea.Stroke = Colors.Transparent;

        private void OnBotonPresionado(object? sender, EventArgs e) => _ = BtnEnviar.ScaleTo(0.97, 100, Easing.CubicOut);

        private void OnBotonSoltado(object? sender, EventArgs e) => _ = BtnEnviar.ScaleTo(1, 160, Easing.CubicOut);

        // Estado de envío: botón gris azulado con spinner en lugar del texto.
        private void MostrarEnviando(bool enviando)
        {
            BtnEnviar.IsEnabled = !enviando;
            BtnEnviar.Text = enviando ? "" : Localizador.T("enviar_solicitud");
            BtnEnviar.BackgroundColor = enviando ? Color.FromArgb("#7F97BC") : (Color)Application.Current!.Resources["ColorPrimary"];
            SpinnerEnviar.IsVisible = SpinnerEnviar.IsRunning = enviando;
            SemanticProperties.SetDescription(SpinnerEnviar, Localizador.T("enviando"));
        }


        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            if (entradaHecha) return;
            entradaHecha = true;
            try
            {
                _ = TarjetaCuidador.FadeTo(1, 300, Easing.CubicOut);
                _ = TarjetaCuidador.TranslateTo(0, 0, 360, Easing.CubicOut);
                await Task.Delay(80);
                _ = Formulario.FadeTo(1, 300, Easing.CubicOut);
                await Formulario.TranslateTo(0, 0, 360, Easing.CubicOut);
            }
            finally
            {
                TarjetaCuidador.Opacity = 1; TarjetaCuidador.TranslationY = 0;
                Formulario.Opacity = 1; Formulario.TranslationY = 0;
                PieAccion.TranslationY = 0;
            }
        }

        private bool entradaHecha;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Cuidador", out var value) && value is CuidadorCercano c)
            {
                cuidador = c;
                Renderizar(c);
            }
        }

        public SolicitarServicioPage()
        {
            InitializeComponent();

            // Borde a borde: el encabezado empieza debajo de la barra de estado y el contenido
            // termina con espacio para la barra de gestos.
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();


            // Estado inicial de la entrada (antes del primer frame).
            TarjetaCuidador.Opacity = 0;
            TarjetaCuidador.TranslationY = 12;
            Formulario.Opacity = 0;
            Formulario.TranslationY = 20;
            PickerFecha.MinimumDate = ServerClock.Today;
            PickerFecha.Date = ServerClock.Today;
            PickerHoraInicio.Time = new TimeSpan(9, 0, 0);
            PickerHoraFin.Time = new TimeSpan(11, 0, 0);

            PickerHoraInicio.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TimePicker.Time)) ActualizarTotal(); };
            PickerHoraFin.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TimePicker.Time)) ActualizarTotal(); };
        }

        private void Renderizar(CuidadorCercano c)
        {
            LblNombre.Text = c.NombreCompleto;
            LblEspecialidad.Text = Localizador.D(c.Especialidad);
            LblTarifa.Text = $"{Localizador.F("rd_monto", c.TarifaHora)} {Localizador.T("sufijo_por_hora")}";

            if (!string.IsNullOrWhiteSpace(c.FotoUrl))
                ImgFoto.Source = $"{ApiService.ServerOrigin}{c.FotoUrl}";

            ActualizarTotal();
        }

        private void ActualizarTotal()
        {
            if (cuidador == null)
                return;

            var horaInicio = PickerHoraInicio.Time ?? TimeSpan.Zero;
            var horaFin = PickerHoraFin.Time ?? TimeSpan.Zero;
            var horas = (decimal)(horaFin - horaInicio).TotalHours;

            LblTotal.Text = horas > 0
                ? Localizador.F("rd_monto", (cuidador.TarifaHora * horas))
                : "--";
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnElegirUbicacionTapped(object sender, EventArgs e)
        {
            var tcs = new TaskCompletionSource<UbicacionCliente?>();
            SeleccionUbicacionBroker.Pendiente = tcs;

            await Shell.Current.GoToAsync("MisUbicacionesPage", new Dictionary<string, object> { { "ModoSeleccion", true } });

            var seleccionada = await tcs.Task;
            SeleccionUbicacionBroker.Pendiente = null;

            if (seleccionada == null)
                return;

            ubicacionElegida = seleccionada;
            LblUbicacionNombre.Text = seleccionada.Nombre;
            LblUbicacionDireccion.Text = seleccionada.Direccion;
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (cuidador == null)
                return;

            if (ubicacionElegida == null)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("selecciona_a_donde_debe_ir"), Localizador.T("ok"));
                return;
            }

            if (PickerHoraFin.Time <= PickerHoraInicio.Time)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("la_hora_de_fin_debe"), Localizador.T("ok"));
                return;
            }

            var fechaSeleccionada = (PickerFecha.Date ?? ServerClock.Today).Date;
            if (fechaSeleccionada == ServerClock.Today && PickerHoraInicio.Time <= ServerClock.Now.TimeOfDay)
            {
                await Alerta.MostrarAsync(Localizador.T("horario_invalido"), Localizador.F("ya_son_las_elige_una", ServerClock.Now), Localizador.T("ok"));
                return;
            }

            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("tu_sesion_expiro_vuelve_a"), Localizador.T("ok"));
                return;
            }

            MostrarEnviando(true);

            try
            {
                // El cliente puede tener varios servicios activos a la vez (con distintos
                // cuidadores o lugares); solo evitamos duplicar una solicitud al mismo
                // cuidador mientras ya tenga una pendiente o en curso con él.
                var serviciosActivos = await _apiService.ObtenerTrabajosActivosPorClienteAsync(clienteId);
                if (serviciosActivos.Any(t => t.CuidadorId == cuidador.Id && t.Estado is 1 or 2 or 3))
                {
                    await Alerta.MostrarAsync(Localizador.T("ya_tienes_una_solicitud_con"), Localizador.T("ya_tienes_un_servicio_pendiente"), Localizador.T("ok"));
                    return;
                }

                var horaInicio = PickerHoraInicio.Time ?? TimeSpan.Zero;
                var horaFin = PickerHoraFin.Time ?? TimeSpan.Zero;
                var horas = (decimal)(horaFin - horaInicio).TotalHours;
                var tarifaTotal = cuidador.TarifaHora * horas;

                var request = new CrearTrabajoRequest
                {
                    ClienteId = clienteId,
                    CuidadorId = cuidador.Id,
                    TipoServicio = cuidador.Especialidad,
                    Fecha = PickerFecha.Date ?? DateTime.Today,
                    HoraInicio = horaInicio,
                    HoraFin = horaFin,
                    Direccion = ubicacionElegida.Direccion,
                    Tarifa = tarifaTotal,
                    Latitud = ubicacionElegida.Latitud,
                    Longitud = ubicacionElegida.Longitud,
                    Tareas = tareas.Count > 0 ? tareas.ToList() : null
                };

                var (success, error) = await _apiService.CrearTrabajoAsync(request);

                if (success)
                {
                    await Alerta.MostrarAsync(Localizador.T("solicitud_enviada"), Localizador.F("le_avisamos_a_te_notificaremos", cuidador.NombreCompleto), Localizador.T("ok"));
                    await Shell.Current.GoToAsync("../../..");
                }
                else
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("no_se_pudo_enviar_la_3", error), Localizador.T("ok"));
                }
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error_inesperado"), ex.ToString(), Localizador.T("ok"));
            }
            finally
            {
                MostrarEnviando(false);
            }
        }
    }
}
