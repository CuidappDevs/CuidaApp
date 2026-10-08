using CUIDAPP.Localization;
using CUIDAPP.Models.Cuidador;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Perfil
{
    /// <summary>
    /// El cuidador elige entre visibilidad manual (interruptor del panel) o por horario: en ese modo el
    /// servidor lo pone visible y lo oculta solo según los días y horas que configure aquí.
    /// </summary>
    public partial class MiHorarioPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private int cuidadorId;
        private bool modoAuto;
        private bool estabaActivo;

        // Lunes primero; el valor es DayOfWeek (0 domingo).
        private static readonly int[] OrdenDias = { 1, 2, 3, 4, 5, 6, 0 };
        private readonly Dictionary<int, (Switch Activo, TimePicker Inicio, TimePicker Fin, View Horas)> filas = new();

        public MiHorarioPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();
            ConstruirDias();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            cuidadorId = Preferences.Default.Get("UserId", 0);

            var horario = await _apiService.ObtenerHorarioAsync(cuidadorId) ?? new HorarioCuidador();
            estabaActivo = horario.Activo;
            CargarEnPantalla(horario);

            Cargando.IsVisible = Cargando.IsRunning = false;
            Contenido.IsVisible = true;
            Contenido.Opacity = 0;
            Contenido.TranslationY = 16;
            _ = Contenido.FadeTo(1, 300, Easing.CubicOut);
            _ = Contenido.TranslateTo(0, 0, 360, Easing.CubicOut);
        }

        private void ConstruirDias()
        {
            for (int i = 0; i < OrdenDias.Length; i++)
            {
                var dia = OrdenDias[i];
                var interruptor = new Switch { OnColor = Tema.C("ColorPrimary"), VerticalOptions = LayoutOptions.Center };
                var inicio = new TimePicker { Time = new TimeSpan(8, 0, 0), Format = "h:mm tt", FontSize = 14, TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center };
                var fin = new TimePicker { Time = new TimeSpan(17, 0, 0), Format = "h:mm tt", FontSize = 14, TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center };

                var horas = new HorizontalStackLayout
                {
                    Spacing = 6,
                    HorizontalOptions = LayoutOptions.End,
                    Children =
                    {
                        inicio,
                        new Label { Text = Localizador.T("horario_hasta"), FontSize = 13, TextColor = Tema.C("ColorTextMuted"), VerticalOptions = LayoutOptions.Center },
                        fin
                    }
                };

                var nombre = new Label { Text = Localizador.T($"dia_{dia}"), FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center };
                var fila = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 8,
                    Padding = new Thickness(14, 8),
                    MinimumHeightRequest = 56
                };
                fila.Add(interruptor, 0);
                fila.Add(nombre, 1);
                fila.Add(horas, 2);

                interruptor.Toggled += (_, e) => horas.IsVisible = e.Value;
                horas.IsVisible = false;
                filas[dia] = (interruptor, inicio, fin, horas);

                ListaDias.Add(fila);
                if (i < OrdenDias.Length - 1)
                    ListaDias.Add(new ContentView { HeightRequest = 1, BackgroundColor = Tema.C("ColorBorder"), Margin = new Thickness(14, 0) });
            }
        }

        private void CargarEnPantalla(HorarioCuidador horario)
        {
            foreach (var (dia, f) in filas)
            {
                var franja = horario.Franjas.Where(x => x.DiaSemana == dia).OrderBy(x => x.Inicio).ToList();
                f.Activo.IsToggled = franja.Count > 0;
                if (franja.Count > 0)
                {
                    // La app muestra una franja por día (de la primera hora a la última).
                    f.Inicio.Time = franja.First().Inicio;
                    f.Fin.Time = franja.Max(x => x.Fin);
                }
            }
            // Sin horario guardado: propone lunes a viernes de 8 a 5.
            if (horario.Franjas.Count == 0)
                foreach (var dia in new[] { 1, 2, 3, 4, 5 })
                    filas[dia].Activo.IsToggled = true;

            ElegirModo(horario.Activo);
        }

        private void ElegirModo(bool auto)
        {
            modoAuto = auto;
            PintarOpcion(OpcionManual, MarcaManual, !auto);
            PintarOpcion(OpcionAuto, MarcaAuto, auto);
            BloqueDias.IsVisible = auto;
        }

        private static void PintarOpcion(Border opcion, Microsoft.Maui.Controls.Shapes.Ellipse marca, bool elegida)
        {
            opcion.Stroke = Tema.C(elegida ? "ColorPrimary" : "ColorBorder");
            opcion.BackgroundColor = Tema.C(elegida ? "ColorPrimarySoft" : "ColorSurface");
            marca.Stroke = Tema.C(elegida ? "ColorPrimary" : "ColorBorder");
            marca.Fill = elegida ? new SolidColorBrush(Tema.C("ColorPrimary")) : new SolidColorBrush(Colors.Transparent);
        }

        private void OnManualTapped(object sender, TappedEventArgs e) => ElegirModo(false);
        private void OnAutoTapped(object sender, TappedEventArgs e) => ElegirModo(true);

        private void OnCopiarLunesTapped(object sender, TappedEventArgs e)
        {
            var lunes = filas[1];
            foreach (var (dia, f) in filas)
                if (dia != 1 && f.Activo.IsToggled)
                {
                    f.Inicio.Time = lunes.Inicio.Time;
                    f.Fin.Time = lunes.Fin.Time;
                }
        }

        private async void OnGuardarTapped(object sender, TappedEventArgs e)
        {
            if (GuardandoIndicador.IsRunning)
                return;
            await BtnGuardar.ScaleTo(0.97, 80, Easing.CubicOut);
            _ = BtnGuardar.ScaleTo(1, 140, Easing.CubicOut);

            // Las franjas se guardan siempre: si vuelve al modo manual, quedan listas para la próxima vez.
            var horario = new HorarioCuidador { Activo = modoAuto };
            foreach (var dia in OrdenDias)
            {
                var f = filas[dia];
                if (!f.Activo.IsToggled) continue;
                if (Hora(f.Fin) <= Hora(f.Inicio))
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("horario_err_rango", Localizador.T($"dia_{dia}").ToLower(Localizador.Cultura)), Localizador.T("ok"));
                    return;
                }
                horario.Franjas.Add(new FranjaHorario { DiaSemana = dia, HoraInicio = Hora(f.Inicio).ToString(@"hh\:mm"), HoraFin = Hora(f.Fin).ToString(@"hh\:mm") });
            }
            if (modoAuto && horario.Franjas.Count == 0)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("horario_err_sin_dias"), Localizador.T("ok"));
                return;
            }

            LblGuardar.IsVisible = false;
            GuardandoIndicador.IsVisible = GuardandoIndicador.IsRunning = true;
            var (ok, error) = await _apiService.GuardarHorarioAsync(cuidadorId, horario);
            LblGuardar.IsVisible = true;
            GuardandoIndicador.IsVisible = GuardandoIndicador.IsRunning = false;

            if (!ok)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("no_se_pudo_actualizar_tu"), Localizador.T("ok"));
                return;
            }

            EstadoCuidador.EstablecerHorarioAutomatico(modoAuto);
            Recordatorios.ProgramarHorario(horario);

            if (modoAuto)
                // Para que le lleguen los avisos de inicio y fin, la app tiene que quedar en segundo plano.
                await Alerta.MostrarAsync(Localizador.T(estabaActivo ? "horario_guardado_titulo" : "horario_segundo_plano_titulo"),
                    Localizador.T("horario_segundo_plano_texto"), Localizador.T("entendido"), TipoAlerta.Info);
            else
                await Alerta.MostrarAsync(Localizador.T("horario_guardado_titulo"), Localizador.T("horario_guardado_manual"), Localizador.T("entendido"), TipoAlerta.Info);

            await Shell.Current.GoToAsync("..");
        }

        private static TimeSpan Hora(TimePicker tp) => tp.Time ?? TimeSpan.Zero;

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
