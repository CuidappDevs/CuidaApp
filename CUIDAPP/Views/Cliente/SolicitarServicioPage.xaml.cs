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
                _ = DisplayAlert(Localizador.T("limite_alcanzado"), Localizador.F("puedes_agregar_hasta_tareas", MaxTareas), Localizador.T("ok"));
                return;
            }

            tareas.Add(texto);
            EntryTarea.Text = "";
            RenderizarTareas();
        }

        private void RenderizarTareas()
        {
            ListaTareas.Clear();
            foreach (var tarea in tareas.ToList())
            {
                var quitar = new Label { Text = "✕", FontSize = 16, TextColor = Color.FromArgb("#9CA3AF"), VerticalOptions = LayoutOptions.Center, Padding = new Thickness(8, 0) };
                quitar.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(() =>
                    {
                        tareas.Remove(tarea);
                        RenderizarTareas();
                    })
                });

                var fila = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                fila.Add(new Label { Text = "☐  " + tarea, FontSize = 14, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#111827"), VerticalOptions = LayoutOptions.Center });
                fila.Add(quitar, 1);

                ListaTareas.Add(new Border
                {
                    Stroke = Colors.Transparent,
                    BackgroundColor = Color.FromArgb("#F8FAFC"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(12, 10),
                    Content = fila
                });
            }
        }

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
            LblTarifa.Text = Localizador.F("rd_hr", c.TarifaHora);

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
                ? Localizador.F("rd", (cuidador.TarifaHora * horas))
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
                await DisplayAlert(Localizador.T("error"), Localizador.T("selecciona_a_donde_debe_ir"), Localizador.T("ok"));
                return;
            }

            if (PickerHoraFin.Time <= PickerHoraInicio.Time)
            {
                await DisplayAlert(Localizador.T("error"), Localizador.T("la_hora_de_fin_debe"), Localizador.T("ok"));
                return;
            }

            var fechaSeleccionada = (PickerFecha.Date ?? ServerClock.Today).Date;
            if (fechaSeleccionada == ServerClock.Today && PickerHoraInicio.Time <= ServerClock.Now.TimeOfDay)
            {
                await DisplayAlert(Localizador.T("horario_invalido"), Localizador.F("ya_son_las_elige_una", ServerClock.Now), Localizador.T("ok"));
                return;
            }

            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
            {
                await DisplayAlert(Localizador.T("error"), Localizador.T("tu_sesion_expiro_vuelve_a"), Localizador.T("ok"));
                return;
            }

            BtnEnviar.IsEnabled = false;
            BtnEnviar.Text = Localizador.T("enviando");

            try
            {
                // El cliente puede tener varios servicios activos a la vez (con distintos
                // cuidadores o lugares); solo evitamos duplicar una solicitud al mismo
                // cuidador mientras ya tenga una pendiente o en curso con él.
                var serviciosActivos = await _apiService.ObtenerTrabajosActivosPorClienteAsync(clienteId);
                if (serviciosActivos.Any(t => t.CuidadorId == cuidador.Id && t.Estado is 1 or 2 or 3))
                {
                    await DisplayAlert(Localizador.T("ya_tienes_una_solicitud_con"), Localizador.T("ya_tienes_un_servicio_pendiente"), Localizador.T("ok"));
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
                    await DisplayAlert(Localizador.T("solicitud_enviada"), Localizador.F("le_avisamos_a_te_notificaremos", cuidador.NombreCompleto), Localizador.T("ok"));
                    await Shell.Current.GoToAsync("../../..");
                }
                else
                {
                    await DisplayAlert(Localizador.T("error"), Localizador.F("no_se_pudo_enviar_la_3", error), Localizador.T("ok"));
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(Localizador.T("error_inesperado"), ex.ToString(), Localizador.T("ok"));
            }
            finally
            {
                BtnEnviar.IsEnabled = true;
                BtnEnviar.Text = Localizador.T("enviar_solicitud");
            }
        }
    }
}
