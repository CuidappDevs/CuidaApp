using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Views.Contratos
{
    /// <summary>
    /// El cliente pide permiso a la administración para contratar Care Partners fijos y valida cada
    /// domicilio donde quiere el servicio (comprobante + quién vive ahí).
    /// </summary>
    public partial class PermisoContratarPage : ContentPage
    {
        private readonly ApiService _api = new();
        private int clienteId;
        private PermisoYDomicilios? datos;

        // Formulario del permiso
        private readonly Editor txtMotivo = new() { Placeholder = Localizador.T("permiso_motivo_placeholder"), MaxLength = 600, HeightRequest = 110 };
        private readonly Entry txtReferencia = new() { Placeholder = Localizador.T("permiso_referencia_placeholder"), MaxLength = 300 };
        private string? cedulaUrl;
        private readonly Label lblCedula = Texto(Localizador.T("permiso_cedula_sin"), true, 13);

        // Formulario del domicilio elegido
        private DomicilioPermiso? domicilioElegido;
        private readonly Entry txtQuienVive = new() { Placeholder = Localizador.T("domicilio_quien_vive_placeholder"), MaxLength = 300 };
        private string? comprobanteUrl;
        private readonly Label lblComprobante = Texto(Localizador.T("domicilio_comprobante_sin"), true, 13);

        public PermisoContratarPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            clienteId = Preferences.Default.Get("UserId", 0);
            await CargarAsync();
        }

        private async Task CargarAsync()
        {
            datos = await _api.ObtenerPermisoContratoAsync(clienteId) ?? new PermisoYDomicilios();
            Pintar();
            Cargando.IsVisible = Cargando.IsRunning = false;
            Desplazamiento.IsVisible = true;
        }

        private void Pintar()
        {
            Contenido.Clear();
            var permiso = datos?.Permiso;
            var estado = permiso?.Estado ?? 0;

            // ---------- 1. Permiso
            var bloque = new List<View>
            {
                new HorizontalStackLayout { Spacing = 10, Children = { Titulo(Localizador.T("permiso_paso_1")), Pastilla(Revision(estado), ColoresRevision(estado)) } }
            };
            var puedeSolicitar = estado == 0 || (estado is 3 or 4 && (permiso?.PuedeVolverASolicitar ?? DateTime.MinValue) <= DateTime.Now);

            if (estado == 1)
                bloque.Add(Texto(Localizador.T("permiso_en_revision_texto"), true));
            else if (estado == 2)
                bloque.Add(Texto(Localizador.T("permiso_aprobado_texto"), true));
            else if (estado is 3 or 4)
            {
                bloque.Add(Texto(string.IsNullOrWhiteSpace(permiso?.ComentarioAdmin) ? Localizador.T("permiso_rechazado_texto") : permiso!.ComentarioAdmin!));
                if (!puedeSolicitar && permiso?.PuedeVolverASolicitar is DateTime cuando)
                    bloque.Add(Texto(Localizador.F("permiso_volver_a_pedir", Localizador.DiaMes(cuando)), true, 13));
            }

            if (puedeSolicitar)
            {
                bloque.Add(Texto(Localizador.T("permiso_explicacion"), true));
                bloque.Add(Texto(Localizador.T("permiso_motivo"), false, 13));
                bloque.Add(CampoTexto(txtMotivo, 110));
                bloque.Add(Texto(Localizador.T("permiso_referencia"), false, 13));
                bloque.Add(CampoTexto(txtReferencia));
                bloque.Add(lblCedula);
                bloque.Add(Boton(Localizador.T("permiso_adjuntar_cedula"), TipoBoton.Secundario, async () =>
                {
                    var url = await ElegirYSubirFotoAsync();
                    if (url == null) return;
                    cedulaUrl = url;
                    lblCedula.Text = Localizador.T("permiso_cedula_lista");
                }));
                bloque.Add(Boton(Localizador.T("permiso_enviar"), TipoBoton.Principal, EnviarPermisoAsync));
            }
            Contenido.Add(Tarjeta(bloque.ToArray()));

            // ---------- 2. Domicilios
            var domicilios = new List<View>
            {
                Titulo(Localizador.T("permiso_paso_2")),
                Texto(Localizador.T("domicilios_explicacion"), true)
            };
            if (datos?.Domicilios.Count == 0)
            {
                domicilios.Add(Texto(Localizador.T("domicilios_sin_ubicaciones"), true));
                domicilios.Add(Boton(Localizador.T("mis_ubicaciones"), TipoBoton.Secundario, () => Shell.Current.GoToAsync("MisUbicacionesPage")));
            }
            foreach (var d in datos?.Domicilios ?? new())
            {
                var fila = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10, Padding = new Thickness(0, 4) };
                var textos = new VerticalStackLayout { Spacing = 2 };
                textos.Add(Titulo(d.Nombre, 15));
                textos.Add(Texto(d.Direccion, true, 12.5));
                if (d.Estado == 3 && !string.IsNullOrWhiteSpace(d.ComentarioAdmin)) textos.Add(Texto(d.ComentarioAdmin!, false, 12.5).ConColor("ColorDanger"));
                fila.Add(textos, 0);
                fila.Add(Pastilla(Revision(d.Estado), ColoresRevision(d.Estado)).Centrado(), 1);
                if (d.Estado is 0 or 3)
                {
                    fila.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { domicilioElegido = d; comprobanteUrl = null; lblComprobante.Text = Localizador.T("domicilio_comprobante_sin"); Pintar(); }) });
                    textos.Add(Texto(Localizador.T("domicilio_tocar_para_validar"), false, 12.5).ConColor("ColorPrimary"));
                }
                domicilios.Add(fila);
            }
            Contenido.Add(Tarjeta(domicilios.ToArray()));

            // ---------- Formulario del domicilio
            if (domicilioElegido != null)
            {
                Contenido.Add(Tarjeta(
                    Titulo(Localizador.F("domicilio_validar_titulo", domicilioElegido.Nombre)),
                    Texto(Localizador.T("domicilio_quien_vive"), false, 13),
                    CampoTexto(txtQuienVive),
                    lblComprobante,
                    Boton(Localizador.T("domicilio_adjuntar_comprobante"), TipoBoton.Secundario, async () =>
                    {
                        var url = await ElegirYSubirFotoAsync();
                        if (url == null) return;
                        comprobanteUrl = url;
                        lblComprobante.Text = Localizador.T("domicilio_comprobante_listo");
                    }),
                    Boton(Localizador.T("domicilio_enviar"), TipoBoton.Principal, EnviarDomicilioAsync)));
            }

            Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });
        }

        private async Task EnviarPermisoAsync()
        {
            var motivo = txtMotivo.Text?.Trim() ?? "";
            if (motivo.Length < 10)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("permiso_motivo_corto"), Localizador.T("ok"));
                return;
            }
            var (ok, error) = await _api.SolicitarPermisoContratoAsync(clienteId, motivo, cedulaUrl, txtReferencia.Text?.Trim());
            if (!ok)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok"));
                return;
            }
            await Alerta.MostrarAsync(Localizador.T("permiso_enviado_titulo"), Localizador.T("permiso_enviado_texto"), Localizador.T("entendido"), TipoAlerta.Exito);
            await CargarAsync();
        }

        private async Task EnviarDomicilioAsync()
        {
            if (domicilioElegido == null) return;
            var quien = txtQuienVive.Text?.Trim() ?? "";
            if (quien.Length < 3 || comprobanteUrl == null)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("domicilio_faltan_datos"), Localizador.T("ok"));
                return;
            }
            var (ok, error) = await _api.SolicitarDomicilioContratoAsync(clienteId, domicilioElegido.UbicacionId, comprobanteUrl, quien);
            if (!ok)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok"));
                return;
            }
            domicilioElegido = null;
            txtQuienVive.Text = "";
            await Alerta.MostrarAsync(Localizador.T("domicilio_enviado_titulo"), Localizador.T("domicilio_enviado_texto"), Localizador.T("entendido"), TipoAlerta.Exito);
            await CargarAsync();
        }

        /// <summary>Foto de cámara o galería, subida a /uploads. Null si cancela o falla (ya avisado).</summary>
        private async Task<string?> ElegirYSubirFotoAsync()
        {
            FileResult? foto = null;
            try
            {
                var tomar = Localizador.T("tomar_foto");
                var galeria = Localizador.T("elegir_de_galeria");
                var opcion = await DisplayActionSheet(Localizador.T("adjuntar_foto_titulo"), Localizador.T("cancelar"), null, tomar, galeria);
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
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("no_se_pudo_seleccionar_el", ex.Message), Localizador.T("ok"));
                return null;
            }
            if (foto == null) return null;

            var (url, error) = await _api.UploadFileConDiagnosticoAsync(foto.FullPath, $"contratos/{clienteId}");
            if (url == null)
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("err_subir_archivo_servidor"), Localizador.T("ok"));
            return url;
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
