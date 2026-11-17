using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Cuenta;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Views.Cuenta
{
    /// <summary>
    /// Saldo pendiente del cliente: servicios en efectivo que no pagó completos. Mientras haya saldo
    /// pendiente (o en revisión) no puede pedir servicios ni contratos. Paga por transferencia a CuidaApp
    /// y sube el comprobante; Finanzas lo confirma en el panel. Si no está de acuerdo, puede objetarlo.
    /// </summary>
    public partial class DeudasClientePage : ContentPage
    {
        private readonly ApiService _api = new();
        private int clienteId;
        private bool cargando;
        private int? objetando; // deuda con el formulario de objeción abierto
        private readonly Editor txtObjecion = new() { MaxLength = 400, HeightRequest = 90, Placeholder = Localizador.T("deuda_objetar_placeholder") };

        public DeudasClientePage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            LblTitulo.Text = Localizador.T("deuda_titulo");
            LblSubtitulo.Text = Localizador.T("deuda_subtitulo");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            clienteId = Preferences.Default.Get("UserId", 0);
            await Task.Yield();
            await CargarAsync();
        }

        private async Task CargarAsync()
        {
            if (cargando) return;
            cargando = true;
            try
            {
                var resumen = await _api.ObtenerDeudasClienteAsync(clienteId);
                Contenido.Clear();
                if (resumen == null)
                {
                    Contenido.Add(Tarjeta(Titulo(Localizador.T("error")), Texto(Localizador.T("deuda_err_cargar"), true),
                        Boton(Localizador.T("reintentar"), TipoBoton.Secundario, CargarAsync)));
                    return;
                }

                var abiertas = resumen.Deudas.Where(d => d.Abierta).ToList();
                var cerradas = resumen.Deudas.Where(d => !d.Abierta).ToList();

                Contenido.Add(abiertas.Count == 0 ? AlDia() : Resumen(resumen.TotalPendiente));

                if (abiertas.Count > 0)
                {
                    var instrucciones = await _api.ObtenerInstruccionesTransferenciaAsync();
                    Contenido.Add(ComoPagar(instrucciones));
                    foreach (var d in abiertas)
                        Contenido.Add(TarjetaDeuda(d));
                }

                if (cerradas.Count > 0)
                {
                    Contenido.Add(Titulo(Localizador.T("deuda_historial"), 15));
                    foreach (var d in cerradas)
                        Contenido.Add(TarjetaDeuda(d));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("deuda_err_cargar"), Localizador.T("ok"));
            }
            finally
            {
                Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });
                Cargando.IsVisible = Cargando.IsRunning = false;
                Desplazamiento.IsVisible = true;
                cargando = false;
            }
        }

        // ---------- piezas

        private static View Resumen(decimal total)
        {
            var monto = new Label
            {
                Text = Monto(total), FontSize = 32, FontFamily = "OpenSansSemibold",
                TextColor = Tema.C("ColorDanger"), HorizontalOptions = LayoutOptions.Center
            };
            var tarjeta = Tarjeta(
                new Label { Text = Localizador.T("deuda_total_pendiente"), FontSize = 13, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextMuted"), HorizontalOptions = LayoutOptions.Center },
                monto,
                Aviso(Localizador.T("deuda_bloqueo_texto"), "ColorDangerSoft"));
            return tarjeta;
        }

        private static View AlDia()
        {
            var icono = new Border
            {
                WidthRequest = 64, HeightRequest = 64, StrokeThickness = 0, BackgroundColor = Tema.C("ColorSuccessSoft"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(), HorizontalOptions = LayoutOptions.Center,
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString("M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z")!,
                    Fill = Tema.C("ColorSuccess"), Aspect = Stretch.Uniform, WidthRequest = 28, HeightRequest = 28,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                }
            };
            var titulo = Titulo(Localizador.T("deuda_al_dia_titulo"), 17);
            titulo.HorizontalOptions = LayoutOptions.Center;
            var texto = Texto(Localizador.T("deuda_al_dia_texto"), true);
            texto.HorizontalTextAlignment = TextAlignment.Center;
            return Tarjeta(icono, titulo, texto);
        }

        private static View ComoPagar(string? instrucciones)
        {
            var piezas = new List<View>
            {
                Titulo(Localizador.T("deuda_como_pagar")),
                Texto(Localizador.T("deuda_como_pagar_texto"), true),
                Aviso(Localizador.T("deuda_no_efectivo"), "ColorDangerSoft")
            };
            if (!string.IsNullOrWhiteSpace(instrucciones))
                piezas.Add(Bloque(Texto(instrucciones)));
            return Tarjeta(piezas.ToArray());
        }

        private static Border Aviso(string texto, string fondo) => new()
        {
            Stroke = Colors.Transparent, BackgroundColor = Tema.C(fondo),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(12, 10), Content = Texto(texto, false, 13)
        };

        private static (string Texto, (string, string) Colores) EstadoDeuda(DeudaCliente d) => d.Estado switch
        {
            1 when !string.IsNullOrWhiteSpace(d.ComprobanteUrl) => (Localizador.T("deuda_estado_comprobante"), ("ColorPrimarySoft", "ColorPrimary")),
            1 => (Localizador.T("deuda_estado_pendiente"), ("ColorDangerSoft", "ColorDanger")),
            2 => (Localizador.T("deuda_estado_pagada"), ("ColorSuccessSoft", "ColorSuccess")),
            3 => (Localizador.T("deuda_estado_revision"), ("ColorWarningSoft", "ColorWarning")),
            _ => (Localizador.T("deuda_estado_condonada"), ("ColorSubtle", "ColorTextMuted"))
        };

        private View TarjetaDeuda(DeudaCliente d)
        {
            var (estado, colores) = EstadoDeuda(d);
            var cabecera = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 10 };
            var fecha = d.FechaServicio ?? d.FechaCreacion;
            cabecera.Add(new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Titulo(Localizador.FechaLarga(fecha), 15),
                    Texto(string.IsNullOrWhiteSpace(d.NombreCuidador) ? Localizador.F("deuda_servicio_n", d.TrabajoId) : d.NombreCuidador!, true, 13)
                }
            }, 0);
            var pastilla = Pastilla(estado, colores);
            pastilla.VerticalOptions = LayoutOptions.Start;
            cabecera.Add(pastilla, 1);

            var piezas = new List<View>
            {
                cabecera,
                Fila(Localizador.T("deuda_total_servicio"), Monto(d.MontoTotal)),
                Fila(Localizador.T("deuda_recibido"), Monto(d.MontoRecibido)),
                Fila(Localizador.T(d.Abierta ? "deuda_pendiente" : "deuda_diferencia"), Monto(d.Monto))
            };

            if (d.Abierta && d.FechaLimite is { } limite)
                piezas.Add(d.Vencida
                    ? Aviso(Localizador.T("deuda_plazo_vencido"), "ColorDangerSoft")
                    : Aviso(Localizador.F("deuda_plazo", FechaHora(limite)), "ColorWarningSoft"));

            if (!string.IsNullOrWhiteSpace(d.NotaAdmin))
                piezas.Add(Bloque(Texto(Localizador.T("deuda_nota_soporte"), true, 12), Texto(d.NotaAdmin!)));

            if (d.Estado == 1 && !string.IsNullOrWhiteSpace(d.ComprobanteUrl))
                piezas.Add(Aviso(Localizador.T("deuda_comprobante_enviado"), "ColorPrimarySoft"));
            if (d.Estado == 3)
                piezas.Add(Aviso(Localizador.T("deuda_revision_texto"), "ColorWarningSoft"));

            if (d.Abierta)
            {
                piezas.Add(Boton(Localizador.T(string.IsNullOrWhiteSpace(d.ComprobanteUrl) ? "deuda_subir_comprobante" : "deuda_cambiar_comprobante"),
                    TipoBoton.Principal, () => SubirComprobanteAsync(d)));

                if (d.Estado == 1)
                {
                    if (objetando == d.Id)
                    {
                        piezas.Add(Separador());
                        piezas.Add(Titulo(Localizador.T("deuda_objetar_titulo"), 15));
                        piezas.Add(Texto(Localizador.T("deuda_objetar_texto"), true, 13));
                        piezas.Add(CampoTexto(txtObjecion, 90));
                        var botones = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 10 };
                        botones.Add(Boton(Localizador.T("cancelar"), TipoBoton.Secundario, async () => { objetando = null; await CargarAsync(); }), 0);
                        botones.Add(Boton(Localizador.T("deuda_objetar_enviar"), TipoBoton.Peligro, () => ObjetarAsync(d)), 1);
                        piezas.Add(botones);
                    }
                    else
                    {
                        piezas.Add(Boton(Localizador.T("deuda_no_estoy_de_acuerdo"), TipoBoton.Secundario, async () =>
                        {
                            objetando = d.Id;
                            txtObjecion.Text = "";
                            await CargarAsync();
                        }));
                    }
                }
            }

            var tarjeta = Tarjeta(piezas.ToArray());
            if (!d.Abierta) tarjeta.Opacity = 0.85;
            return tarjeta;
        }

        private static string FechaHora(DateTime f)
            => Localizador.F("fecha_y_hora", Localizador.DiaMes(f), f.ToString("h:mm tt", Localizador.Cultura));

        // ---------- acciones

        private async Task SubirComprobanteAsync(DeudaCliente d)
        {
            var url = await ElegirYSubirFotoAsync();
            if (url == null) return;
            var (ok, error) = await _api.EnviarComprobanteDeudaAsync(d.Id, clienteId, url, null);
            if (!ok)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("cuenta_err_generico"), Localizador.T("ok"));
                return;
            }
            await Alerta.MostrarAsync(Localizador.T("deuda_comprobante_listo_titulo"), Localizador.T("deuda_comprobante_listo_texto"), Localizador.T("ok"), TipoAlerta.Exito);
            await CargarAsync();
        }

        private async Task ObjetarAsync(DeudaCliente d)
        {
            var motivo = txtObjecion.Text?.Trim();
            if (string.IsNullOrWhiteSpace(motivo) || motivo.Length < 5)
            {
                await Alerta.MostrarAsync(Localizador.T("deuda_objetar_titulo"), Localizador.T("deuda_objetar_corto"), Localizador.T("ok"));
                return;
            }
            var (ok, error) = await _api.ObjetarDeudaAsync(d.Id, clienteId, motivo);
            if (!ok)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("cuenta_err_generico"), Localizador.T("ok"));
                return;
            }
            objetando = null;
            await Alerta.MostrarAsync(Localizador.T("deuda_objetada_titulo"), Localizador.T("deuda_objetada_texto"), Localizador.T("ok"), TipoAlerta.Exito);
            await CargarAsync();
        }

        private async Task<string?> ElegirYSubirFotoAsync()
        {
            FileResult? foto;
            try
            {
                var tomar = Localizador.T("tomar_foto");
                var galeria = Localizador.T("elegir_de_galeria");
                var opcion = await DisplayActionSheet(Localizador.T("deuda_subir_comprobante"), Localizador.T("cancelar"), null, galeria, tomar);
                if (opcion == tomar)
                {
                    if (!MediaPicker.Default.IsCaptureSupported)
                    {
                        await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("camara_no_disponible"), Localizador.T("ok"));
                        return null;
                    }
                    if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
                        return null;
                    foto = await MediaPicker.Default.CapturePhotoAsync();
                }
                else if (opcion == galeria)
                    foto = await MediaPicker.Default.PickPhotoAsync();
                else
                    return null;
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("no_se_pudo_seleccionar_el", ex.Message), Localizador.T("ok"));
                return null;
            }
            if (foto == null) return null;

            var (url, error) = await _api.UploadFileConDiagnosticoAsync(foto.FullPath, $"deudas/{clienteId}");
            if (url == null)
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("err_subir_archivo_servidor"), Localizador.T("ok"));
            return url;
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
