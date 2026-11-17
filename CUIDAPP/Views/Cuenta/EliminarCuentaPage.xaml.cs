using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Cuenta;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Views.Cuenta
{
    /// <summary>
    /// Eliminar la cuenta (cliente o Care Partner). Primero el servidor revisa lo que lo impide
    /// (disputas, servicios o contratos activos, deudas). Sin bloqueos: código de 6 dígitos por correo
    /// y confirmación. La cuenta se desactiva al instante y se anonimiza a los 30 días; mientras tanto,
    /// iniciando sesión se puede recuperar.
    /// </summary>
    public partial class EliminarCuentaPage : ContentPage
    {
        private readonly ApiService _api = new();
        private int usuarioId;
        private bool cargando;
        private bool codigoEnviado;
        private RevisionEliminacion? revision;

        private readonly Editor txtMotivo = new() { MaxLength = 400, HeightRequest = 80, Placeholder = Localizador.T("eliminar_motivo_placeholder") };
        private readonly Entry txtCodigo = new()
        {
            MaxLength = 6, Keyboard = Keyboard.Numeric, FontSize = 22, FontFamily = "OpenSansSemibold",
            HorizontalTextAlignment = TextAlignment.Center, CharacterSpacing = 8, Placeholder = "••••••"
        };

        private static bool EsCuidador => Preferences.Default.Get("RolId", 0) == 3;

        public EliminarCuentaPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            LblTitulo.Text = Localizador.T("eliminar_titulo");
            LblSubtitulo.Text = Localizador.T("eliminar_subtitulo");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            usuarioId = Preferences.Default.Get("UserId", 0);
            await Task.Yield();
            await CargarAsync(revisar: true);
        }

        private async Task CargarAsync(bool revisar)
        {
            if (cargando) return;
            cargando = true;
            try
            {
                if (revisar || revision == null)
                    revision = await _api.RevisarEliminacionAsync(usuarioId);

                Contenido.Clear();
                if (revision == null)
                {
                    Contenido.Add(Tarjeta(Titulo(Localizador.T("error")), Texto(Localizador.T("eliminar_err_revisar"), true),
                        Boton(Localizador.T("reintentar"), TipoBoton.Secundario, () => RecargarAsync())));
                    return;
                }

                Contenido.Add(QueSucede());

                var bloqueos = revision.Bloqueos.Where(b => b.Bloquea).ToList();
                var avisos = revision.Bloqueos.Where(b => !b.Bloquea).ToList();
                if (bloqueos.Count > 0)
                    Contenido.Add(ListaPendientes(bloqueos, true));
                if (avisos.Count > 0)
                    Contenido.Add(ListaPendientes(avisos, false));

                if (revision.PuedeEliminar)
                    Contenido.Add(Confirmacion());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("eliminar_err_revisar"), Localizador.T("ok"));
            }
            finally
            {
                Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });
                Cargando.IsVisible = Cargando.IsRunning = false;
                Desplazamiento.IsVisible = true;
                cargando = false;
            }
        }

        private Task RecargarAsync() => CargarAsync(revisar: true);

        // ---------- piezas

        /// <summary>Lo que se borra y lo que se conserva, en dos bloques.</summary>
        private static View QueSucede()
        {
            View Punto(string texto, bool bueno)
            {
                var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 10 };
                fila.Add(new Ellipse
                {
                    Fill = Tema.C(bueno ? "ColorDanger" : "ColorTextMuted"), WidthRequest = 6, HeightRequest = 6,
                    VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 7, 0, 0)
                }, 0);
                fila.Add(Texto(texto, false, 13.5), 1);
                return fila;
            }

            var titulo = Titulo(Localizador.T("eliminar_que_pasa"));
            return Tarjeta(
                titulo,
                Texto(Localizador.T("eliminar_que_pasa_texto"), true),
                Bloque(
                    Titulo(Localizador.T("eliminar_se_borra"), 14),
                    Punto(Localizador.T("eliminar_se_borra_1"), true),
                    Punto(Localizador.T(EsCuidador ? "eliminar_se_borra_2_cuidador" : "eliminar_se_borra_2_cliente"), true),
                    Punto(Localizador.T("eliminar_se_borra_3"), true)),
                Bloque(
                    Titulo(Localizador.T("eliminar_se_guarda"), 14),
                    Punto(Localizador.T("eliminar_se_guarda_1"), false),
                    Punto(Localizador.T("eliminar_se_guarda_2"), false)));
        }

        private View ListaPendientes(List<BloqueoEliminacion> lista, bool bloquean)
        {
            var piezas = new List<View>
            {
                Titulo(Localizador.T(bloquean ? "eliminar_bloqueos_titulo" : "eliminar_avisos_titulo")),
                Texto(Localizador.T(bloquean ? "eliminar_bloqueos_texto" : "eliminar_avisos_texto"), true)
            };
            foreach (var b in lista.GroupBy(b => b.Codigo).Select(g => (Codigo: g.Key, Cantidad: g.Count(), Primero: g.First())))
                piezas.Add(FilaPendiente(b.Codigo, b.Cantidad, b.Primero, bloquean));
            return Tarjeta(piezas.ToArray());
        }

        private View FilaPendiente(string codigo, int cantidad, BloqueoEliminacion b, bool bloquea)
        {
            var clave = "eliminar_bloqueo_" + codigo.ToLowerInvariant();
            var titulo = Localizador.T(clave);
            if (titulo == clave) titulo = string.IsNullOrWhiteSpace(b.Detalle) ? codigo : b.Detalle!;
            if (cantidad > 1) titulo = $"{titulo} ({cantidad})";
            var ayudaClave = clave + "_ayuda";
            var ayuda = Localizador.T(ayudaClave);

            var icono = new Border
            {
                WidthRequest = 32, HeightRequest = 32, StrokeThickness = 0,
                BackgroundColor = Tema.C(bloquea ? "ColorDangerSoft" : "ColorWarningSoft"),
                StrokeShape = new RoundRectangle { CornerRadius = 10 }, VerticalOptions = LayoutOptions.Start,
                Content = new Label
                {
                    Text = "!", FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = Tema.C(bloquea ? "ColorDanger" : "ColorWarning"),
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                }
            };
            var textos = new VerticalStackLayout { Spacing = 2 };
            textos.Add(Titulo(titulo, 14));
            if (ayuda != ayudaClave) textos.Add(Texto(ayuda, true, 12.5));

            var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 12 };
            fila.Add(icono, 0);
            fila.Add(textos, 1);

            var piezas = new List<View> { fila };
            var destino = codigo switch
            {
                "DEUDA_CLIENTE" => "DeudasClientePage",
                "CONTRATO_ACTIVO" => "MisContratosPage",
                "SERVICIO_ACTIVO" when EsCuidador => "TrabajosPage",
                "DISPUTA_ABIERTA" or "DEUDA_CUIDADOR" or "PAGO_POR_COBRAR" => "MisReportesPage",
                _ => null
            };
            if (destino != null)
                piezas.Add(Boton(Localizador.T(destino == "MisReportesPage" ? "eliminar_ir_soporte" : "eliminar_ir_resolver"), TipoBoton.Secundario,
                    () => Shell.Current.GoToAsync(destino)));
            return Bloque(piezas.ToArray());
        }

        private View Confirmacion()
        {
            var piezas = new List<View>
            {
                Titulo(Localizador.T("eliminar_confirmar_titulo")),
                Texto(Localizador.T("eliminar_motivo_label"), true, 13),
                CampoTexto(txtMotivo, 80)
            };

            if (!codigoEnviado)
            {
                piezas.Add(Texto(Localizador.F("eliminar_codigo_explica", Preferences.Default.Get("UserEmail", "")), true, 13));
                piezas.Add(Boton(Localizador.T("eliminar_enviar_codigo"), TipoBoton.Principal, EnviarCodigoAsync));
            }
            else
            {
                piezas.Add(Separador());
                piezas.Add(Texto(Localizador.F("eliminar_codigo_enviado", Preferences.Default.Get("UserEmail", "")), false, 13));
                piezas.Add(CampoTexto(txtCodigo, 56));
                piezas.Add(Boton(Localizador.T("eliminar_boton_final"), TipoBoton.Peligro, ConfirmarAsync));
                var reenviar = Texto(Localizador.T("eliminar_reenviar_codigo"), false, 13);
                reenviar.TextColor = Tema.C("ColorPrimary");
                reenviar.FontFamily = "OpenSansSemibold";
                reenviar.HorizontalOptions = LayoutOptions.Center;
                reenviar.Padding = new Thickness(12, 6);
                reenviar.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await EnviarCodigoAsync()) });
                piezas.Add(reenviar);
            }
            return Tarjeta(piezas.ToArray());
        }

        // ---------- acciones

        private async Task EnviarCodigoAsync()
        {
            var (ok, error) = await _api.EnviarCodigoEliminacionAsync(usuarioId);
            if (!ok)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("cuenta_err_generico"), Localizador.T("ok"));
                await RecargarAsync(); // puede que haya aparecido un bloqueo
                return;
            }
            codigoEnviado = true;
            txtCodigo.Text = "";
            await CargarAsync(revisar: false);
            txtCodigo.Focus();
        }

        private async Task ConfirmarAsync()
        {
            var codigo = (txtCodigo.Text ?? "").Trim();
            if (codigo.Length != 6 || !codigo.All(char.IsDigit))
            {
                await Alerta.MostrarAsync(Localizador.T("codigo_incompleto"), Localizador.T("eliminar_codigo_6"), Localizador.T("ok"));
                return;
            }
            if (!await Alerta.MostrarAsync(Localizador.T("eliminar_seguro_titulo"), Localizador.T("eliminar_seguro_texto"),
                    Localizador.T("eliminar_boton_final"), Localizador.T("cancelar"), TipoAlerta.Advertencia))
                return;

            var motivo = string.IsNullOrWhiteSpace(txtMotivo.Text) ? null : txtMotivo.Text.Trim();
            var (fecha, error) = await _api.ConfirmarEliminacionAsync(usuarioId, codigo, motivo);
            if (fecha == null)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("cuenta_err_generico"), Localizador.T("ok"));
                return;
            }

            await Alerta.MostrarAsync(Localizador.T("eliminar_hecho_titulo"), Localizador.F("eliminar_hecho_texto", Localizador.FechaLarga(fecha.Value)), Localizador.T("ok"));

            // La cuenta ya está desactivada: se cierra la sesión en este teléfono.
            DeadManService.Detener();
            Recordatorios.CancelarTodos();
            Preferences.Default.Clear();
            await Shell.Current.GoToAsync("//MainPage");
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
