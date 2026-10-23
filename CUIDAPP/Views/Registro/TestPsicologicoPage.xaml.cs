using CUIDAPP.Localization;
using CUIDAPP.Models.Auth;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Views.Registro
{
    /// <summary>
    /// Test psicológico del registro (pantalla completa, modal). Una pregunta por pantalla, con el tiempo
    /// límite que fijó el panel; al agotarse se envía lo respondido. El servidor califica y la página
    /// devuelve el resultado y el token que el registro necesita para crear la cuenta.
    /// </summary>
    public partial class TestPsicologicoPage : ContentPage
    {
        private const string IconoCheck = "M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z";
        private const string IconoAlto = "M12 2C6.48 2 2 6.48 2 12S6.48 22 12 22 22 17.52 22 12 17.52 2 12 2ZM13 17H11V15H13V17ZM13 13H11V7H13V13Z";

        private readonly IniciarTestRequest _solicitud;
        private readonly ApiService _api = new();
        private readonly TaskCompletionSource<(ResultadoTest? Resultado, Guid? Token)> _terminado = new();
        private readonly Dictionary<int, int> _respuestas = new(); // preguntaId -> opcionId
        private readonly List<Action> _repintarOpciones = new();   // una por opción de la pregunta actual

        private TestParaResolver? _test;
        private ResultadoTest? _resultado;
        private int _indice;
        private DateTime _finUtc;
        private bool _iniciado, _enviando;

        /// <summary>Resultado (null si salió sin terminar o no hubo conexión) y token para el registro.</summary>
        public Task<(ResultadoTest? Resultado, Guid? Token)> Terminado => _terminado.Task;

        public TestPsicologicoPage(IniciarTestRequest solicitud)
        {
            InitializeComponent();
            _solicitud = solicitud;
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            if (_iniciado) return;
            _iniciado = true;
            await IniciarAsync();
        }

        private async Task IniciarAsync()
        {
            var (test, estado) = await _api.IniciarTestAsync(_solicitud);

            if (test == null)
            {
                // Ya no está libre (lo hizo en otro momento) o no hubo conexión.
                switch (estado?.Estado)
                {
                    case "APROBADO":
                        await CerrarAsync(new ResultadoTest { Resultado = ResultadoTest.Aprobado }, estado.Token);
                        return;
                    case "SIN_TEST":
                        await CerrarAsync(new ResultadoTest { Resultado = ResultadoTest.Aprobado }, null);
                        return;
                    case "BLOQUEADO":
                    case "DESCARTADO":
                        MostrarResultado(new ResultadoTest
                        {
                            Resultado = estado.Estado == "BLOQUEADO" ? ResultadoTest.Bloqueado : ResultadoTest.Descartado,
                            BloqueadoHasta = estado.BloqueadoHasta
                        });
                        return;
                    default:
                        await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("test_error_conexion"), Localizador.T("ok"), TipoAlerta.Error);
                        await CerrarAsync(null, null);
                        return;
                }
            }

            _test = test;
            _finUtc = DateTime.UtcNow.AddMinutes(test.MinutosLimite);
            PanelCargando.IsVisible = false;
            CajaTiempo.IsVisible = BloqueAvance.IsVisible = true;
            ActualizarTiempo();
            Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                if (_enviando || _resultado != null || _terminado.Task.IsCompleted) return false;
                ActualizarTiempo();
                if (DateTime.UtcNow >= _finUtc)
                {
                    _ = TiempoAgotadoAsync();
                    return false;
                }
                return true;
            });

            if (!string.IsNullOrWhiteSpace(test.Descripcion))
                await Alerta.MostrarAsync(test.Nombre, test.Descripcion, Localizador.T("test_empezar"), TipoAlerta.Info);

            PanelPregunta.IsVisible = true;
            await MostrarPreguntaAsync(0, animar: false);
        }

        private void ActualizarTiempo()
        {
            var resta = _finUtc - DateTime.UtcNow;
            if (resta < TimeSpan.Zero) resta = TimeSpan.Zero;
            LblTiempo.Text = $"{(int)resta.TotalMinutes}:{resta.Seconds:00}";
            // El último minuto se marca en ámbar.
            CajaTiempo.BackgroundColor = resta.TotalSeconds <= 60 ? Color.FromArgb("#F59E0B") : Color.FromArgb("#26FFFFFF");
        }

        private async Task TiempoAgotadoAsync()
        {
            await Alerta.MostrarAsync(Localizador.T("test_tiempo_titulo"), Localizador.T("test_tiempo_texto"), Localizador.T("ok"), TipoAlerta.Advertencia);
            await EnviarAsync();
        }

        // ---------- Preguntas

        private async Task MostrarPreguntaAsync(int indice, bool animar = true, int direccion = 1)
        {
            var test = _test!;
            if (animar)
            {
                _ = PanelPregunta.TranslateTo(-16 * direccion, 0, 120, Easing.CubicIn);
                await PanelPregunta.FadeTo(0, 120, Easing.CubicIn);
            }

            _indice = indice;
            var pregunta = test.Preguntas[indice];
            LblAvance.Text = Localizador.F("test_pregunta_de", indice + 1, test.Preguntas.Count);
            LblPregunta.Text = pregunta.Texto;
            LblSiguiente.Text = Localizador.T(indice == test.Preguntas.Count - 1 ? "test_terminar" : "siguiente");
            BtnAnterior.IsVisible = indice > 0;
            ActualizarBarraAvance();

            ListaOpciones.Clear();
            _repintarOpciones.Clear();
            foreach (var opcion in pregunta.Opciones)
                ListaOpciones.Add(CrearOpcion(pregunta, opcion));
            ActualizarBotonSiguiente();

            if (animar)
            {
                PanelPregunta.TranslationX = 16 * direccion;
                _ = PanelPregunta.TranslateTo(0, 0, 200, Easing.CubicOut);
                await PanelPregunta.FadeTo(1, 200, Easing.CubicOut);
            }
            await Scroll.ScrollToAsync(0, 0, false);
        }

        private void ActualizarBarraAvance()
        {
            var total = Math.Max(1, _test!.Preguntas.Count);
            var ancho = PistaAvance.Width > 0 ? PistaAvance.Width : Width - 40;
            BarraAvance.WidthRequest = Math.Max(0, ancho * (_indice + 1) / total);
        }

        private View CrearOpcion(PreguntaTest pregunta, OpcionTest opcion)
        {
            var marca = new Border
            {
                StrokeShape = new Ellipse(),
                StrokeThickness = 2,
                WidthRequest = 22,
                HeightRequest = 22,
                VerticalOptions = LayoutOptions.Center,
                Content = new Ellipse { WidthRequest = 10, HeightRequest = 10, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };
            var texto = new Label { Text = opcion.Texto, FontSize = 15, TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center, LineHeight = 1.2 };
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 14 };
            grid.Add(marca, 0);
            grid.Add(texto, 1);

            var tarjeta = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                StrokeThickness = 1.5,
                Padding = new Thickness(16, 14),
                Content = grid
            };
            SemanticProperties.SetDescription(tarjeta, opcion.Texto);

            void Pintar()
            {
                var elegida = _respuestas.TryGetValue(pregunta.Id, out var id) && id == opcion.Id;
                tarjeta.Stroke = Tema.C(elegida ? "ColorPrimary" : "ColorBorder");
                tarjeta.BackgroundColor = Tema.C(elegida ? "ColorPrimarySoft" : "ColorSurface");
                marca.Stroke = Tema.C(elegida ? "ColorPrimary" : "ColorBorder");
                ((Ellipse)marca.Content!).Fill = elegida ? Tema.C("ColorPrimary") : Colors.Transparent;
            }
            Pintar();
            _repintarOpciones.Add(Pintar);

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (_enviando) return;
                _respuestas[pregunta.Id] = opcion.Id;
                foreach (var repintar in _repintarOpciones)
                    repintar();
                ActualizarBotonSiguiente();
                await tarjeta.ScaleTo(0.98, 70, Easing.CubicOut);
                await tarjeta.ScaleTo(1, 120, Easing.CubicOut);
            };
            tarjeta.GestureRecognizers.Add(tap);
            return tarjeta;
        }

        private bool PreguntaRespondida() => _test != null && _respuestas.ContainsKey(_test.Preguntas[_indice].Id);

        private void ActualizarBotonSiguiente() => BtnSiguiente.Opacity = PreguntaRespondida() ? 1 : 0.5;

        private async void OnSiguienteTapped(object sender, TappedEventArgs e)
        {
            if (_enviando || _test == null) return;
            if (!PreguntaRespondida())
            {
                await Alerta.MostrarAsync(Localizador.T("test_titulo"), Localizador.T("test_elige_una_error"), Localizador.T("ok"));
                return;
            }

            await BtnSiguiente.ScaleTo(0.97, 80, Easing.CubicOut);
            _ = BtnSiguiente.ScaleTo(1, 140, Easing.CubicOut);

            if (_indice < _test.Preguntas.Count - 1)
                await MostrarPreguntaAsync(_indice + 1);
            else
                await EnviarAsync();
        }

        private async void OnAnteriorTapped(object sender, TappedEventArgs e)
        {
            if (_enviando || _indice == 0) return;
            await MostrarPreguntaAsync(_indice - 1, direccion: -1);
        }

        // ---------- Envío y resultado

        private async Task EnviarAsync()
        {
            if (_enviando || _test == null) return;
            _enviando = true;
            PanelPregunta.IsVisible = false;
            CajaTiempo.IsVisible = BloqueAvance.IsVisible = false;
            PanelEnviando.IsVisible = true;

            var solicitud = new FinalizarTestRequest
            {
                Token = _test.Token,
                Respuestas = _respuestas.Select(r => new RespuestaTest { PreguntaId = r.Key, OpcionId = r.Value }).ToList()
            };

            ResultadoTest? resultado = null;
            for (int intento = 0; intento < 3 && resultado == null; intento++)
            {
                resultado = await _api.FinalizarTestAsync(solicitud);
                if (resultado == null && intento < 2)
                {
                    var reintentar = await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("test_error_envio"), Localizador.T("test_reintentar"), Localizador.T("cancelar"), TipoAlerta.Error);
                    if (!reintentar) break;
                }
            }

            PanelEnviando.IsVisible = false;
            if (resultado == null)
            {
                await CerrarAsync(null, null);
                return;
            }
            MostrarResultado(resultado);
        }

        private void MostrarResultado(ResultadoTest resultado)
        {
            _resultado = resultado;
            PanelCargando.IsVisible = PanelPregunta.IsVisible = PanelEnviando.IsVisible = false;
            CajaTiempo.IsVisible = BloqueAvance.IsVisible = false;
            BtnCerrar.IsVisible = false;

            // A la candidata no se le muestra el puntaje: solo si puede seguir.
            if (resultado.PuedeSeguir)
            {
                CirculoResultado.BackgroundColor = Tema.C("ColorSuccessSoft");
                IconoResultado.Fill = Tema.C("ColorSuccess");
                IconoResultado.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(IconoCheck)!;
                LblResultadoTitulo.Text = Localizador.T("test_listo_titulo");
                LblResultadoTexto.Text = Localizador.T(resultado.Resultado == ResultadoTest.Revision ? "test_revision_texto" : "test_aprobado_texto");
                LblBtnResultado.Text = Localizador.T("test_continuar_registro");
                BtnResultado.BackgroundColor = Tema.C("ColorPrimary");
            }
            else
            {
                CirculoResultado.BackgroundColor = Tema.C("ColorDangerSoft");
                IconoResultado.Fill = Tema.C("ColorDanger");
                IconoResultado.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(IconoAlto)!;
                LblResultadoTitulo.Text = Localizador.T("test_no_continua_titulo");
                LblResultadoTexto.Text = resultado.Resultado == ResultadoTest.Bloqueado && resultado.BloqueadoHasta is DateTime hasta
                    ? Localizador.F("test_bloqueado_texto", Localizador.FechaLarga(hasta))
                    : Localizador.T("test_descartado_texto");
                LblBtnResultado.Text = Localizador.T("entendido");
                BtnResultado.BackgroundColor = Tema.C("ColorTextMuted");
            }

            PanelResultado.Opacity = 0;
            PanelResultado.Scale = 0.96;
            PanelResultado.IsVisible = true;
            _ = PanelResultado.FadeTo(1, 220, Easing.CubicOut);
            _ = PanelResultado.ScaleTo(1, 260, Easing.CubicOut);
        }

        private async void OnResultadoTapped(object sender, TappedEventArgs e)
        {
            await BtnResultado.ScaleTo(0.97, 80, Easing.CubicOut);
            _ = BtnResultado.ScaleTo(1, 140, Easing.CubicOut);
            await CerrarAsync(_resultado, _test?.Token);
        }

        // ---------- Salir

        private async void OnCerrarTapped(object sender, TappedEventArgs e) => await PreguntarSalirAsync();

        protected override bool OnBackButtonPressed()
        {
            if (_resultado != null)
                OnResultadoTapped(this, null!);
            else if (!_enviando)
                _ = PreguntarSalirAsync();
            return true;
        }

        private async Task PreguntarSalirAsync()
        {
            if (_enviando || _resultado != null) return;
            if (_test != null && !await Alerta.MostrarAsync(Localizador.T("test_salir_titulo"), Localizador.T("test_salir_texto"),
                    Localizador.T("test_salir"), Localizador.T("test_seguir"), TipoAlerta.Advertencia))
                return;
            await CerrarAsync(null, null);
        }

        private async Task CerrarAsync(ResultadoTest? resultado, Guid? token)
        {
            if (!_terminado.TrySetResult((resultado, token))) return;
            if (Navigation.ModalStack.Contains(this))
                await Navigation.PopModalAsync(true);
        }
    }
}
