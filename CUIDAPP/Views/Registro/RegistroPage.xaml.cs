using CUIDAPP.Localization;
using Microsoft.Maui.Controls;
using CUIDAPP.Services;
using CUIDAPP.Models.Auth;
using CUIDAPP.Models.Cliente;

namespace CUIDAPP.Views.Registro
{
    public partial class RegistroPage : ContentPage
    {
        private int currentStepIndex = 0;
        private List<View> currentFlow = new List<View>();
        private decimal? direccionLatitud;
        private decimal? direccionLongitud;

        private string selectedRole = "Cuidador"; // Default
        // Especialidad elegida en el paso "¿Qué trabajo haces?": es el Nombre del tipo de trabajo
        // y se envía tal cual como texto al registrar (PerfilCuidador.Especialidad).
        private string? especialidadSeleccionada;
        private List<Models.Cuidador.TipoTrabajo> tiposTrabajo = new();
        private int selectedPay = 2; // Default Billetera
        private readonly ApiService _apiService = new ApiService();
        private List<Nacionalidad> nacionalidades = new();

        // Archivos elegidos localmente (aún no subidos). Se suben todos juntos al finalizar el registro.
        private FileResult? fotoFile;
        private FileResult? cedulaFile;
        private FileResult? antecedentesFile;

        // Carpeta única para este registro (una carpeta por usuario en el servidor con sus documentos).
        private readonly string carpetaUsuario = Guid.NewGuid().ToString("N");

        public RegistroPage()
        {
            InitializeComponent();
            SetupFlows();
            _ = CargarTiposTrabajoAsync();
            _ = CargarNacionalidadesAsync();
            RenderizarDocumentosExtra();
            UpdatePayOptionsUI();
            UpdateRoleOptionsUI();
        }

        private void SetupFlows()
        {
            // Ocultar todo primero
            PasoBienvenida.IsVisible = false;
            PasoRol.IsVisible = false;
            PasoCredenciales.IsVisible = false;
            PasoNombre.IsVisible = false;
            PasoFoto.IsVisible = false;
            PasoTrabajo.IsVisible = false;
            PasoDocumentos.IsVisible = false;
            PasoCobro.IsVisible = false;
            PasoDireccion.IsVisible = false;

            if (selectedRole == "Cliente")
            {
                currentFlow = new List<View> { PasoBienvenida, PasoRol, PasoCredenciales, PasoNombre, PasoFoto, PasoDireccion };
                BioContainer.IsVisible = false; // Cliente no necesita bio
            }
            else
            {
                currentFlow = new List<View> { PasoBienvenida, PasoRol, PasoCredenciales, PasoNombre, PasoFoto, PasoTrabajo, PasoDocumentos, PasoCobro };
                BioContainer.IsVisible = true; // Cuidador sí necesita bio
            }

            _ = UpdateStepUI(false);
        }

        // ---- Términos y Condiciones (antes del paso 1) ----
        private bool terminosLeidos;

        private void OnTerminosScrolled(object? sender, ScrolledEventArgs e)
        {
            ActualizarBarraLectura();
            EvaluarFinalTerminos();
        }

        // Barra fina bajo el título que avanza con el scroll.
        private void ActualizarBarraLectura()
        {
            var recorrido = ScrollTerminos.ContentSize.Height - ScrollTerminos.Height;
            var progreso = recorrido <= 0 ? 1 : Math.Clamp(ScrollTerminos.ScrollY / recorrido, 0, 1);
            BarraLecturaTerminos.WidthRequest = PanelTerminos.Width * progreso;
        }

        private void OnBotonTerminosPresionado(object? sender, EventArgs e) => _ = (sender as VisualElement)?.ScaleTo(0.97, 100, Easing.CubicOut);

        private void OnBotonTerminosSoltado(object? sender, EventArgs e) => _ = (sender as VisualElement)?.ScaleTo(1, 160, Easing.CubicOut);

        private void OnTerminosLayoutChanged(object? sender, EventArgs e) => EvaluarFinalTerminos();

        // Aceptar y Rechazar se habilitan solo cuando el usuario llegó al final (o si todo cabe sin scroll).
        private void EvaluarFinalTerminos()
        {
            if (terminosLeidos)
                return;

            var visible = ScrollTerminos.Height;
            var contenido = ScrollTerminos.ContentSize.Height;
            if (visible <= 0 || contenido <= 0)
                return;

            if (ScrollTerminos.ScrollY + visible < contenido - 24)
                return;

            terminosLeidos = true;
            BtnAceptarTerminos.IsEnabled = true;
            BtnRechazarTerminos.IsEnabled = true;
            ActualizarBarraLectura();

            // Los botones se "encienden" y la pista se desvanece.
            _ = BtnAceptarTerminos.FadeTo(1, 220, Easing.CubicOut);
            _ = BtnRechazarTerminos.FadeTo(1, 220, Easing.CubicOut);
            _ = LblHintTerminos.FadeTo(0, 160, Easing.CubicOut).ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() => LblHintTerminos.IsVisible = false));
        }

        private void OnAceptarTerminosTapped(object sender, EventArgs e)
        {
            if (!terminosLeidos)
                return;

            PanelTerminos.IsVisible = false; // se muestra el paso 1 (Hello!)
        }

        // Rechazar (o la flecha atrás de esta pantalla) devuelve al login.
        private async void OnRechazarTerminosTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // El paso de bienvenida ocupa todo el alto visible: el botón queda abajo y la ilustración al centro.
        private void OnScrollRegistroSizeChanged(object? sender, EventArgs e)
        {
            if (ScrollRegistro.Height > 0)
            {
                // padding 10 + 20 del contenedor
                foreach (var paso in new VisualElement[] { PasoBienvenida, PasoRol, PasoCredenciales, PasoNombre, PasoFoto, PasoTrabajo, PasoDocumentos, PasoDireccion, PasoCobro })
                    paso.MinimumHeightRequest = ScrollRegistro.Height - 30;
            }
        }

        // ---- Ilustración de bienvenida (bucle sutil mientras la página está visible) ----
        protected override void OnAppearing()
        {
            base.OnAppearing();
            IniciarAnimacionBienvenida();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            foreach (var n in new[] { "HolaSaludo", "HolaFlotar", "HolaParpadeo" })
                this.AbortAnimation(n);
        }

        private void IniciarAnimacionBienvenida()
        {
            // Saludo: dos sacudidas rápidas de la mano y una pausa.
            new Animation(t =>
            {
                var fase = t < 0.45 ? Math.Sin(t / 0.45 * Math.PI * 4) : 0;
                // Vaivén solo hacia afuera (entre ~10° y ~34°): nunca cruza hacia la cabeza.
                HolaBrazo.Rotation = 22 + 12 * fase;
                // La palma gira un poco en cada sacudida, como al saludar de verdad.
                HolaMano.Rotation = 10 * fase;
            }, 0, 1).Commit(this, "HolaSaludo", length: 2600, easing: Easing.Linear, repeat: () => true);

            // Corazón, destellos y anillo: flotan y respiran.
            new Animation(t =>
            {
                var s = Math.Sin(t * Math.PI * 2);
                HolaCorazon.TranslationY = -6 * s;
                HolaCorazon.Scale = 1 + 0.08 * s;
                HolaDestello1.Opacity = 0.55 + 0.45 * s;
                HolaDestello1.Rotation = 20 * s;
                HolaDestello2.Opacity = 0.55 - 0.45 * s;
                HolaDestello2.Scale = 1 - 0.2 * s;
                HolaAnillo.Scale = 1 + 0.03 * s;
                HolaAnillo.Opacity = 0.3 + 0.12 * s;
            }, 0, 1).Commit(this, "HolaFlotar", length: 3200, easing: Easing.Linear, repeat: () => true);

            // Parpadeo breve cada ~3.5 s.
            new Animation(t =>
            {
                var cierre = t > 0.94 ? Math.Sin((t - 0.94) / 0.06 * Math.PI) : 0;
                HolaOjoIzq.ScaleY = HolaOjoDer.ScaleY = 1 - 0.9 * cierre;
            }, 0, 1).Commit(this, "HolaParpadeo", length: 3500, easing: Easing.Linear, repeat: () => true);
        }

        protected override bool OnBackButtonPressed()
        {
            if (PanelTerminos.IsVisible)
            {
                OnRechazarTerminosTapped(this, EventArgs.Empty);
                return true;
            }

            return base.OnBackButtonPressed();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            if (currentStepIndex > 0)
            {
                currentStepIndex--;
                await UpdateStepUI(true);
            }
            else
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        private async void OnNextTapped(object sender, EventArgs e)
        {
            if (!await ValidateCurrentStep())
                return;

            if (currentStepIndex < currentFlow.Count - 1)
            {
                currentStepIndex++;
                await UpdateStepUI(true);
            }
            else
            {
                // Aqui conectaríamos con la API
                await FinishRegistration();
            }
        }

        private async Task<bool> ValidateCurrentStep()
        {
            var currentView = currentFlow[currentStepIndex];

            if (currentView == PasoCredenciales)
            {
                if (string.IsNullOrWhiteSpace(EntryEmail.Text) || !EntryEmail.Text.Contains("@"))
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("por_favor_ingresa_un_correo"), Localizador.T("ok"));
                    return false;
                }
                if (!ClaveEsSegura(EntryPassword.Text))
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("clave_no_cumple"), Localizador.T("ok"));
                    return false;
                }
                if (EntryPassword.Text != EntryPasswordConfirmar.Text)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("las_claves_no_coinciden"), Localizador.T("ok"));
                    return false;
                }
            }
            else if (currentView == PasoNombre)
            {
                if (string.IsNullOrWhiteSpace(EntryNombre.Text))
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("el_nombre_completo_es_obligatorio"), Localizador.T("ok"));
                    return false;
                }
                var digitosTelefono = (EntryTelefono.Text ?? "").Count(char.IsDigit);
                if (digitosTelefono < 7 || digitosTelefono > 15)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("telefono_invalido"), Localizador.T("ok"));
                    return false;
                }
                // Nacionalidad obligatoria solo si la lista cargó (si el API falla, el registro sigue sin ella).
                if (nacionalidades.Count > 0 && NacionalidadSeleccionadaId() is null)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("selecciona_tu_nacionalidad_error"), Localizador.T("ok"));
                    return false;
                }
                if (NacionalidadSeleccionadaId() is not null)
                {
                    var doc = DocumentoLimpio();
                    bool valido = EsDominicana()
                        ? doc.Length == 11 && doc.All(char.IsDigit)
                        : doc.Length >= 5 && doc.All(char.IsLetterOrDigit);
                    if (!valido)
                    {
                        await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T(EsDominicana() ? "cedula_invalida" : "pasaporte_invalido"), Localizador.T("ok"));
                        return false;
                    }
                }
            }
            else if (currentView == PasoTrabajo)
            {
                if (string.IsNullOrWhiteSpace(especialidadSeleccionada))
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("selecciona_un_tipo_de_trabajo"), Localizador.T("ok"));
                    return false;
                }
                if (string.IsNullOrWhiteSpace(EntryTarifa.Text) || !decimal.TryParse(EntryTarifa.Text, out decimal tarifa) || tarifa <= 0)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("por_favor_ingresa_una_tarifa"), Localizador.T("ok"));
                    return false;
                }
            }
            else if (currentView == PasoDireccion)
            {
                if (string.IsNullOrWhiteSpace(EntryDireccion.Text))
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("selecciona_tu_direccion_en_el"), Localizador.T("ok"));
                    return false;
                }
                if (string.IsNullOrWhiteSpace(EntryEmergenciaNombre.Text) ||
                    string.IsNullOrWhiteSpace(EntryEmergenciaTelefono.Text))
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("por_favor_completa_todos_los"), Localizador.T("ok"));
                    return false;
                }
            }
            else if (currentView == PasoFoto)
            {
                if (fotoFile == null)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("selecciona_tu_foto_de_perfil"), Localizador.T("ok"));
                    return false;
                }
            }
            else if (currentView == PasoDocumentos)
            {
                if (cedulaFile == null || antecedentesFile == null)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("selecciona_ambos_documentos"), Localizador.T("ok"));
                    return false;
                }
            }

            return true;
        }

        private async Task FinishRegistration()
        {
            bool esCuidador = selectedRole == "Cuidador";

            // Archivos a subir, en orden. Los opcionales que fallen no bloquean el registro.
            var subidas = new List<(FileResult Archivo, string Clave, string ErrorClave, bool Obligatorio, DocumentoExtra? Extra)>();
            if (fotoFile != null) subidas.Add((fotoFile, "progreso_foto", "err_subir_foto", true, null));
            if (esCuidador)
            {
                if (cedulaFile != null) subidas.Add((cedulaFile, EsExtranjero() ? "progreso_pasaporte" : "progreso_cedula", "err_subir_cedula", true, null));
                if (antecedentesFile != null) subidas.Add((antecedentesFile, "progreso_antecedentes", "err_subir_carta", true, null));
                foreach (var doc in documentosExtra.Where(d => d.Archivo != null && DocumentoExtraVisible(d)))
                    subidas.Add((doc.Archivo!, "progreso_opcional", "", false, doc));
            }

            // Ventana de carga: el cuidador ve una barra con el avance real (sus documentos pueden tardar);
            // el cliente, que solo sube su foto, un spinner.
            OverlayTitle.Text = Localizador.T(esCuidador ? "progreso_titulo" : "cargando");
            OverlayMessage.Text = Localizador.T(esCuidador ? "progreso_subtitulo" : "subiendo_documentos");
            OverlayIcon.IsVisible = false;
            OverlaySpinner.IsVisible = OverlaySpinner.IsRunning = !esCuidador;
            BloqueProgreso.IsVisible = esCuidador;
            PintarProgreso(0, subidas.Count > 0 ? Localizador.F(subidas[0].Clave, 1, subidas.Count) : "");
            OverlayExito.IsVisible = true;
            await OverlayExito.FadeTo(1, 300);

            // Peso de cada archivo para que la barra avance según los bytes, no solo por cantidad de archivos.
            var tamanos = subidas.Select(x => { try { return Math.Max(1L, new FileInfo(x.Archivo.FullPath).Length); } catch { return 1L; } }).ToList();
            double total = tamanos.Sum(), hechos = 0;

            string fotoUrl = "";
            string cedulaUrl = "";
            string antecedentesUrl = "";
            var documentosExtraSubidos = new List<DocumentoExtraDto>();

            for (int n = 0; n < subidas.Count; n++)
            {
                var (archivo, clave, errorClave, obligatorio, extra) = subidas[n];
                var texto = Localizador.F(clave, n + 1, subidas.Count);
                var baseHechos = hechos;
                var tamano = tamanos[n];
                PintarProgreso(baseHechos / total, texto);

                var progreso = new Progress<double>(f => PintarProgreso((baseHechos + f * tamano) / total, texto));
                var url = await _apiService.UploadFileAsync(archivo.FullPath, carpetaUsuario, esCuidador ? progreso : null) ?? "";
                hechos += tamano;

                if (url == "" && obligatorio)
                {
                    await MostrarErrorSubida(Localizador.T(errorClave));
                    return;
                }

                if (archivo == fotoFile) fotoUrl = url;
                else if (archivo == cedulaFile) cedulaUrl = url;
                else if (archivo == antecedentesFile) antecedentesUrl = url;
                else if (extra != null && url != "") documentosExtraSubidos.Add(new DocumentoExtraDto { TipoDocumento = extra.Tipo, UrlArchivo = url });
            }

            PintarProgreso(1, Localizador.T("enviando_datos_al_servidor"));
            OverlayMessage.Text = Localizador.T("enviando_datos_al_servidor");

            bool success = false;

            if (selectedRole == "Cliente")
            {
                var request = new RegisterClientRequest
                {
                    Email = EntryEmail.Text ?? "",
                    Password = EntryPassword.Text ?? "",
                    NombreCompleto = EntryNombre.Text ?? "",
                    NacionalidadId = NacionalidadSeleccionadaId(),
                    DocumentoIdentidad = NacionalidadSeleccionadaId() is null ? null : EntryDocumento.Text?.Trim(),
                    Telefono = EntryTelefono.Text?.Trim(),
                    DireccionPrincipal = EntryDireccion.Text ?? "",
                    ContactoEmergenciaNombre = EntryEmergenciaNombre.Text ?? "",
                    ContactoEmergenciaTelefono = EntryEmergenciaTelefono.Text ?? "",
                    FotoUrl = fotoUrl
                };
                int nuevoUserId;
                (success, nuevoUserId) = await _apiService.RegisterClienteAsync(request);

                // Si eligió la dirección en el mapa, esa se convierte en su primera ubicación
                // guardada ("Casa"), lista para usar al reservar servicios.
                if (success && nuevoUserId > 0 && direccionLatitud.HasValue && direccionLongitud.HasValue)
                {
                    await _apiService.CrearUbicacionClienteAsync(nuevoUserId, "Casa", EntryDireccion.Text ?? "", direccionLatitud.Value, direccionLongitud.Value, true);
                }
            }
            else
            {
                string especialidad = especialidadSeleccionada ?? "";
                string metodoCobro = selectedPay == 1 ? "Cuenta bancaria" : (selectedPay == 2 ? "Billetera CuidApp" : "Efectivo");
                decimal.TryParse(EntryTarifa.Text, out decimal tarifa);

                var request = new RegisterCaregiverRequest
                {
                    Email = EntryEmail.Text ?? "",
                    Password = EntryPassword.Text ?? "",
                    NombreCompleto = EntryNombre.Text ?? "",
                    NacionalidadId = NacionalidadSeleccionadaId(),
                    DocumentoIdentidad = NacionalidadSeleccionadaId() is null ? null : EntryDocumento.Text?.Trim(),
                    Telefono = EntryTelefono.Text?.Trim(),
                    Bio = EntryBio.Text ?? "",
                    Especialidad = especialidad,
                    TarifaHora = tarifa,
                    MetodoCobro = metodoCobro,
                    FotoUrl = fotoUrl,
                    CedulaUrl = cedulaUrl,
                    CartaAntecedentesUrl = antecedentesUrl,
                    DocumentosExtra = documentosExtraSubidos
                };
                success = await _apiService.RegisterCuidadorAsync(request);
            }

            if (success)
            {
                // Cambiar el overlay a modo Éxito
                OverlayTitle.Text = Localizador.T("exito_2");
                OverlayMessage.Text = Localizador.T("registro_completado");
                OverlayIcon.IsVisible = true;
                OverlaySpinner.IsVisible = OverlaySpinner.IsRunning = false;
                BloqueProgreso.IsVisible = false;

                await Task.Delay(1500); // 1.5s para que lo vea
                
                await OverlayExito.FadeTo(0, 200);
                OverlayExito.IsVisible = false;

                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await OverlayExito.FadeTo(0, 200);
                OverlayExito.IsVisible = false;
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("ocurrio_un_error_al_conectar"), Localizador.T("ok"));
            }
        }

        // Barra de progreso de la ventana de carga (0..1). La animación es corta para que avance suave.
        private void PintarProgreso(double fraccion, string paso)
        {
            fraccion = Math.Clamp(fraccion, 0, 1);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblProgresoPaso.Text = paso;
                LblProgresoPorcentaje.Text = $"{(int)Math.Round(fraccion * 100)}%";
                var ancho = (PistaProgreso.Width > 0 ? PistaProgreso.Width : 240) * fraccion;
                BarraProgreso.AbortAnimation("Progreso");
                var desde = BarraProgreso.WidthRequest < 0 ? 0 : BarraProgreso.WidthRequest;
                BarraProgreso.Animate("Progreso", v => BarraProgreso.WidthRequest = v, desde, ancho, length: 180, easing: Easing.CubicOut);
            });
        }

        private async Task MostrarErrorSubida(string mensaje)
        {
            await OverlayExito.FadeTo(0, 200);
            OverlayExito.IsVisible = false;
            await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("verifica_tu_conexion_e_intenta", mensaje), Localizador.T("ok"));
        }

        private async void OnElegirDireccionMapaTapped(object sender, EventArgs e)
        {
            var tcs = new TaskCompletionSource<UbicacionCliente?>();
            SeleccionUbicacionBroker.PendientePunto = tcs;

            await Shell.Current.GoToAsync("SeleccionarPuntoMapaPage", new Dictionary<string, object> { { "SoloSeleccionar", true } });

            var seleccionado = await tcs.Task;
            SeleccionUbicacionBroker.PendientePunto = null;

            if (seleccionado == null)
                return;

            EntryDireccion.Text = seleccionado.Direccion;
            direccionLatitud = seleccionado.Latitud;
            direccionLongitud = seleccionado.Longitud;
        }

        private async void OnPickFotoTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = Localizador.T("picker_imagen_perfil"),
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    fotoFile = result;
                    LblFotoFileName.Text = result.FileName;
                    LblFotoFileName.TextColor = Tema.C("ColorSuccess");
                    ImgFotoPreview.Source = ImageSource.FromFile(result.FullPath);
                    ImgFotoPreview.IsVisible = true;
                    CirculoFoto.StrokeDashArray = null;
                    CirculoFoto.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];
                }
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("no_se_pudo_seleccionar_la", ex.Message), Localizador.T("ok"));
            }
        }

        private async void OnPickCedulaTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = Localizador.T("picker_doc_cedula")
                });

                if (result != null)
                {
                    cedulaFile = result;
                    LblCedulaFileName.Text = result.FileName;
                    LblCedulaFileName.TextColor = Tema.C("ColorSuccess");
                    MarcarDocumentoListo(BtnPickCedula, CedulaEstadoCaja, CedulaEstado);
                }
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("no_se_pudo_seleccionar_el", ex.Message), Localizador.T("ok"));
            }
        }

        private async void OnPickAntecedentesTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = Localizador.T("picker_carta")
                });

                if (result != null)
                {
                    antecedentesFile = result;
                    LblAntecedentesFileName.Text = result.FileName;
                    LblAntecedentesFileName.TextColor = Tema.C("ColorSuccess");
                    MarcarDocumentoListo(BtnPickAntecedentes, AntecedentesEstadoCaja, AntecedentesEstado);
                }
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("no_se_pudo_seleccionar_el", ex.Message), Localizador.T("ok"));
            }
        }

        private void OnRoleOptionTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string role)
            {
                selectedRole = role;
                UpdateRoleOptionsUI();
                
                // Regenerar flujo
                SetupFlows();
            }
        }

        private void OnPayOptionTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string param && int.TryParse(param, out int payIndex))
            {
                selectedPay = payIndex;
                UpdatePayOptionsUI();
            }
        }

        private void UpdateRoleOptionsUI()
        {
            PintarOpcionRol(RoleOptionCliente, RoleIconBoxCliente, RoleIconCliente, RoleTextCliente, RoleCheckCliente, RoleCheckMarkCliente, selectedRole == "Cliente");
            PintarOpcionRol(RoleOptionCuidador, RoleIconBoxCuidador, RoleIconCuidador, RoleTextCuidador, RoleCheckCuidador, RoleCheckMarkCuidador, selectedRole == "Cuidador");
        }

        private static void PintarOpcionRol(Border tarjeta, Border cajaIcono, Microsoft.Maui.Controls.Shapes.Path icono, Label titulo,
                                            Border check, Microsoft.Maui.Controls.Shapes.Path marca, bool activo)
        {
            Color R(string k) => (Color)Application.Current!.Resources[k];
            var primario = R("ColorPrimary");

            tarjeta.Stroke = activo ? primario : R("ColorBorder");
            tarjeta.StrokeThickness = activo ? 2 : 1.5;
            tarjeta.BackgroundColor = activo ? Tema.C("ColorPrimarySoft") : Tema.C("ColorSurface");
            cajaIcono.BackgroundColor = activo ? primario : R("ColorBackground");
            icono.Fill = activo ? Colors.White : R("ColorTextMuted");
            titulo.TextColor = activo ? primario : R("ColorTextStrong");
            check.Stroke = activo ? primario : R("ColorBorder");
            check.BackgroundColor = activo ? primario : Tema.C("ColorSurface");

            var estabaActivo = marca.Opacity > 0.5;
            marca.Opacity = activo ? 1 : 0;
            if (activo && !estabaActivo)
            {
                // Respuesta al seleccionar: la tarjeta "late" y el check aparece.
                marca.Scale = 0.6;
                _ = marca.ScaleTo(1, 220, Easing.CubicOut);
                _ = tarjeta.ScaleTo(0.98, 90, Easing.CubicOut).ContinueWith(_ =>
                    MainThread.BeginInvokeOnMainThread(() => tarjeta.ScaleTo(1, 160, Easing.CubicOut)));
            }
        }

        // Opciones de respaldo si el API no responde: las mismas que la app tenía fijas,
        // así el registro nunca se queda sin poder elegir.
        private static readonly List<Models.Cuidador.TipoTrabajo> TiposTrabajoRespaldo = new()
        {
            new() { Id = 1, Nombre = "Limpieza del hogar", Icono = "limpieza", Activo = true },
            new() { Id = 2, Nombre = "Niñera / Cuidadora", Icono = "ninos", Activo = true },
            new() { Id = 3, Nombre = "Cuidadora de adultos", Icono = "adultos", Activo = true }
        };

        // Nacionalidades (tabla Nacionalidades). Campo opcional: si la API falla, el registro sigue sin ella.
        private async Task CargarNacionalidadesAsync()
        {
            CargandoNacionalidades.IsVisible = CargandoNacionalidades.IsRunning = true;
            FlechaNacionalidad.IsVisible = false;
            nacionalidades = await _apiService.ObtenerNacionalidadesAsync();
            CargandoNacionalidades.IsVisible = CargandoNacionalidades.IsRunning = false;
            FlechaNacionalidad.IsVisible = true;

            PickerNacionalidad.ItemsSource = nacionalidades.Select(n => Localizador.D(n.Nombre)).ToList();
            PickerNacionalidad.IsEnabled = nacionalidades.Count > 0;
        }

        private bool EsDominicana()
        {
            var i = PickerNacionalidad.SelectedIndex;
            return i >= 0 && i < nacionalidades.Count && nacionalidades[i].CodigoIso == "DO";
        }

        // Sin guiones ni espacios (para validar).
        private string DocumentoLimpio() => new string((EntryDocumento.Text ?? "").Where(char.IsLetterOrDigit).ToArray());

        // Al elegir nacionalidad aparece el documento: cédula (teclado telefónico: el numérico de Android rechaza los guiones; 000-0000000-0) si es dominicana, si no pasaporte.
        private async void OnNacionalidadCambiada(object? sender, EventArgs e)
        {
            if (NacionalidadSeleccionadaId() is null) return;

            ActualizarDocumentosSegunNacionalidad();
            bool dominicana = EsDominicana();
            bool cambiaTipo = !DocumentoContainer.IsVisible || (EntryDocumento.Keyboard == Keyboard.Telephone) != dominicana;
            if (!cambiaTipo) return;

            LblDocumento.Text = Localizador.T(dominicana ? "cedula_de_identidad" : "pasaporte");
            EntryDocumento.Placeholder = Localizador.T(dominicana ? "escribe_tu_cedula" : "escribe_tu_pasaporte");
            AyudaDocumento.Text = Localizador.T(dominicana ? "ayuda_cedula" : "ayuda_pasaporte");
            EntryDocumento.Keyboard = dominicana ? Keyboard.Telephone : Keyboard.Create(KeyboardFlags.CapitalizeCharacter);
            EntryDocumento.MaxLength = dominicana ? 13 : 20;
            EntryDocumento.Text = "";

            if (!DocumentoContainer.IsVisible)
            {
                DocumentoContainer.Opacity = 0;
                DocumentoContainer.TranslationY = -8;
                DocumentoContainer.IsVisible = true;
                await Task.WhenAll(
                    DocumentoContainer.FadeTo(1, 220, Easing.CubicOut),
                    DocumentoContainer.TranslateTo(0, 0, 220, Easing.CubicOut));
            }
        }

        private bool formateandoDocumento;

        // Cédula: pone los guiones solo (000-0000000-0). Pasaporte: mayúsculas, sin espacios.
        // El texto se reescribe en el siguiente ciclo: cambiarlo dentro del propio TextChanged
        // hace tronar a Android (EmojiCompat procesa un texto que ya cambió de largo).
        private void OnDocumentoCambiado(object? sender, TextChangedEventArgs e)
        {
            if (formateandoDocumento) return;
            Dispatcher.Dispatch(FormatearDocumento);
        }

        private void FormatearDocumento()
        {
            var actual = EntryDocumento.Text ?? "";
            string texto;
            if (EsDominicana())
            {
                var d = new string(actual.Where(char.IsDigit).Take(11).ToArray());
                texto = d.Length <= 3 ? d
                      : d.Length <= 10 ? $"{d[..3]}-{d[3..]}"
                      : $"{d[..3]}-{d[3..10]}-{d[10..]}";
            }
            else
            {
                texto = new string(actual.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            }

            if (texto == actual) return;

            formateandoDocumento = true;
            EntryDocumento.Text = texto;
            formateandoDocumento = false;
            EntryDocumento.CursorPosition = Math.Min(texto.Length, EntryDocumento.Text?.Length ?? 0);
        }

        private int? NacionalidadSeleccionadaId()
        {
            var i = PickerNacionalidad.SelectedIndex;
            return i >= 0 && i < nacionalidades.Count ? nacionalidades[i].Id : null;
        }

        private async Task CargarTiposTrabajoAsync()
        {
            CargandoTiposTrabajo.IsVisible = CargandoTiposTrabajo.IsRunning = true;
            var lista = await _apiService.ObtenerTiposTrabajoAsync();
            CargandoTiposTrabajo.IsVisible = CargandoTiposTrabajo.IsRunning = false;

            tiposTrabajo = lista.Count > 0 ? lista : TiposTrabajoRespaldo;

            // Por defecto, el primer tipo disponible queda elegido
            if (especialidadSeleccionada == null || !tiposTrabajo.Any(t => t.Activo && t.Nombre == especialidadSeleccionada))
                especialidadSeleccionada = tiposTrabajo.FirstOrDefault(t => t.Activo)?.Nombre;

            RenderizarTiposTrabajo();
        }

        private readonly List<(Models.Cuidador.TipoTrabajo Tipo, Border Tarjeta, Border Caja, Microsoft.Maui.Controls.Shapes.Path Icono, Label Texto, Border Check, Microsoft.Maui.Controls.Shapes.Path Marca)> tarjetasTrabajo = new();

        private void RenderizarTiposTrabajo()
        {
            ListaTiposTrabajo.Clear();
            tarjetasTrabajo.Clear();
            Color R(string k) => (Color)Application.Current!.Resources[k];

            foreach (var tipo in tiposTrabajo)
            {
                var icono = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(IconoTipoTrabajo(tipo.Icono))!,
                    Aspect = Stretch.Uniform, WidthRequest = 22, HeightRequest = 22,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                };
                var caja = new Border
                {
                    Stroke = Colors.Transparent,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                    WidthRequest = 48, HeightRequest = 48, VerticalOptions = LayoutOptions.Center,
                    Content = icono
                };
                var texto = new Label { Text = Localizador.D(tipo.Nombre), FontSize = 16, FontFamily = "OpenSansSemibold", VerticalOptions = LayoutOptions.Center };
                var marca = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString("M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z")!,
                    Fill = Colors.White, Aspect = Stretch.Uniform, WidthRequest = 14, HeightRequest = 14,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Opacity = 0
                };
                var check = new Border
                {
                    StrokeThickness = 1.5,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    WidthRequest = 24, HeightRequest = 24, VerticalOptions = LayoutOptions.Center,
                    Content = marca
                };

                var centro = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Children = { texto } };

                var grid = new Grid
                {
                    ColumnSpacing = 14,
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
                };
                grid.Add(caja, 0);
                grid.Add(centro, 1);

                var tarjeta = new Border
                {
                    StrokeThickness = 1.5,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                    Padding = new Thickness(16),
                    BackgroundColor = Tema.C("ColorSurface"),
                    Content = grid
                };

                if (tipo.Activo)
                {
                    grid.Add(check, 2);
                    var nombre = tipo.Nombre;
                    tarjeta.GestureRecognizers.Add(new TapGestureRecognizer
                    {
                        Command = new Command(() =>
                        {
                            especialidadSeleccionada = nombre;
                            PintarTiposTrabajo();
                        })
                    });
                }
                else
                {
                    // Inactivo en la tabla: se muestra, pero "No disponible" y sin poder elegirse
                    tarjeta.Opacity = 0.55;
                    tarjeta.Stroke = R("ColorBorder");
                    caja.BackgroundColor = R("ColorBackground");
                    icono.Fill = R("ColorTextMuted");
                    texto.TextColor = R("ColorTextStrong");
                    centro.Add(new Border
                    {
                        Stroke = Colors.Transparent,
                        BackgroundColor = R("ColorBackground"),
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                        Padding = new Thickness(8, 2),
                        HorizontalOptions = LayoutOptions.Start,
                        Content = new Label { Text = Localizador.T("no_disponible"), FontSize = 10, FontFamily = "OpenSansSemibold", TextColor = R("ColorTextMuted") }
                    });
                }

                tarjetasTrabajo.Add((tipo, tarjeta, caja, icono, texto, check, marca));
                ListaTiposTrabajo.Add(tarjeta);
            }

            PintarTiposTrabajo();
        }

        private void PintarTiposTrabajo()
        {
            foreach (var (tipo, tarjeta, caja, icono, texto, check, marca) in tarjetasTrabajo)
            {
                if (!tipo.Activo)
                    continue;
                PintarOpcionRol(tarjeta, caja, icono, texto, check, marca, tipo.Nombre == especialidadSeleccionada);
            }
        }

        // Clave de la columna Icono -> ícono vectorial
        private static string IconoTipoTrabajo(string? clave) => clave?.Trim().ToLowerInvariant() switch
        {
            "limpieza" => "M19.36 2.72L20.78 4.14L15.06 9.85C16.13 11.39 16.28 13.24 15.38 14.44L9.06 8.12C10.26 7.22 12.11 7.37 13.65 8.44L19.36 2.72ZM5.93 17.57C3.92 15.56 2.69 13.16 2.35 10.92L7.23 8.34L15.16 16.27L12.58 21.15C10.34 20.81 7.94 19.58 5.93 17.57Z",
            "ninos" => "M12 2C13.66 2 15 3.34 15 5C15 6.66 13.66 8 12 8C10.34 8 9 6.66 9 5C9 3.34 10.34 2 12 2ZM16 9H8C7.45 9 7 9.45 7 10V15H9V22H15V15H17V10C17 9.45 16.55 9 16 9Z",
            "adultos" or "adulto_mayor" => "M12 21.35L10.55 20.03C5.4 15.36 2 12.28 2 8.5C2 5.42 4.42 3 7.5 3C9.24 3 10.91 3.81 12 5.09C13.09 3.81 14.76 3 16.5 3C19.58 3 22 5.42 22 8.5C22 12.28 18.6 15.36 13.45 20.04L12 21.35Z",
            "hospital" => "M19 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3ZM18 14H14V18H10V14H6V10H10V6H14V10H18V14Z",
            "cocina" => "M8.1 13.34L10.93 10.51L3.91 3.5C2.35 5.06 2.35 7.59 3.91 9.16L8.1 13.34ZM14.88 11.53C16.41 12.24 18.56 11.74 20.15 10.15C22.06 8.24 22.43 5.5 20.96 4.03C19.5 2.57 16.76 2.93 14.84 4.84C13.25 6.43 12.75 8.58 13.46 10.11L3.7 19.87L5.11 21.28L12 14.41L18.88 21.29L20.29 19.88L13.41 13L14.88 11.53Z",
            _ => "M20 6H16V4C16 2.9 15.1 2 14 2H10C8.9 2 8 2.9 8 4V6H4C2.9 6 2.01 6.9 2.01 8L2 19C2 20.1 2.9 21 4 21H20C21.1 21 22 20.1 22 19V8C22 6.9 21.1 6 20 6ZM10 4H14V6H10V4ZM20 19H4V8H20V19Z"
        };

        private void UpdatePayOptionsUI()
        {
            PintarOpcionRol(PayOption1, PayIconBox1, PayIcon1, PayText1, PayCheck1, PayCheckMark1, selectedPay == 1);
            PintarOpcionRol(PayOption2, PayIconBox2, PayIcon2, PayText2, PayCheck2, PayCheckMark2, selectedPay == 2);
            PintarOpcionRol(PayOption3, PayIconBox3, PayIcon3, PayText3, PayCheck3, PayCheckMark3, selectedPay == 3);
            ActualizarAvisoCobro();
        }

        // ---- Campos: borde resaltado al enfocar ----
        private static Border? BordeDe(object? sender)
        {
            var e = (sender as Element)?.Parent;
            while (e != null && e is not Border) e = e.Parent;
            return e as Border;
        }

        private void OnCampoFocused(object? sender, FocusEventArgs e)
        {
            if (BordeDe(sender) is Border b)
            {
                b.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];
                b.BackgroundColor = Tema.C("ColorSurface");
            }
        }

        private void OnCampoUnfocused(object? sender, FocusEventArgs e)
        {
            if (BordeDe(sender) is Border b)
            {
                b.Stroke = (Color)Application.Current!.Resources["ColorBorder"];
                b.BackgroundColor = (Color)Application.Current!.Resources["ColorBackground"];
            }
        }

        private void OnToggleRegistroPassword(object? sender, EventArgs e) => EntryPassword.IsPassword = !EntryPassword.IsPassword;

        private void OnToggleRegistroPasswordConfirmar(object? sender, EventArgs e) => EntryPasswordConfirmar.IsPassword = !EntryPasswordConfirmar.IsPassword;

        // ---------- Contraseña segura: mínimo 6 caracteres, al menos un número y un carácter especial.
        private static bool TieneLargo(string c) => c.Length >= 6;
        private static bool TieneNumero(string c) => c.Any(char.IsDigit);
        private static bool TieneEspecial(string c) => c.Any(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
        private static bool ClaveEsSegura(string? c) => c != null && TieneLargo(c) && TieneNumero(c) && TieneEspecial(c);

        private void OnPasswordCambiado(object? sender, TextChangedEventArgs e)
        {
            var clave = EntryPassword.Text ?? "";
            PintarRegla(ReglaLargoCaja, ReglaLargoTexto, TieneLargo(clave));
            PintarRegla(ReglaNumeroCaja, ReglaNumeroTexto, TieneNumero(clave));
            PintarRegla(ReglaEspecialCaja, ReglaEspecialTexto, TieneEspecial(clave));

            var confirmacion = EntryPasswordConfirmar.Text ?? "";
            LblCoincideClave.IsVisible = confirmacion.Length > 0;
            bool coinciden = confirmacion == clave;
            LblCoincideClave.Text = Localizador.T(coinciden ? "las_claves_coinciden" : "las_claves_no_coinciden");
            LblCoincideClave.TextColor = coinciden ? Tema.C("ColorSuccess") : (Color)Application.Current!.Resources["ColorDanger"];
        }

        private static void PintarRegla(Border caja, Label texto, bool cumple)
        {
            var verde = Tema.C("ColorSuccess");
            var antes = caja.BackgroundColor;
            caja.BackgroundColor = cumple ? verde : (Color)Application.Current!.Resources["ColorBorder"];
            texto.TextColor = cumple ? verde : (Color)Application.Current!.Resources["ColorTextMuted"];
            if (cumple && antes != verde)
            {
                caja.Scale = 0.6;
                _ = caja.ScaleTo(1, 200, Easing.CubicOut);
            }
        }

        // ---------- Aviso del método de cobro (efectivo requiere un paso extra con la administración).
        private void ActualizarAvisoCobro()
        {
            bool efectivo = selectedPay == 3;
            var clave = efectivo ? "aviso_cobro_efectivo" : "aviso_cobro_validacion";
            if (AvisoCobroTexto.Text == Localizador.T(clave)) return;

            AvisoCobroTexto.Text = Localizador.T(clave);
            var color = efectivo ? Color.FromArgb("#8A5A00") : (Color)Application.Current!.Resources["ColorPrimary"];
            AvisoCobroTexto.TextColor = color;
            AvisoCobroIcono.Fill = color;
            AvisoCobro.BackgroundColor = efectivo ? Tema.C("ColorWarningSoft") : Tema.C("ColorPrimarySoft");
            AvisoCobro.Opacity = 0;
            AvisoCobro.TranslationY = 6;
            _ = AvisoCobro.FadeTo(1, 200, Easing.CubicOut);
            _ = AvisoCobro.TranslateTo(0, 0, 200, Easing.CubicOut);
        }

        // ---------- Documentos opcionales (DocumentosCuidador). Tipo = texto que se guarda en la BD (en español).
        private sealed class DocumentoExtra
        {
            public required string Tipo { get; init; }
            public required string ClaveTitulo { get; init; }
            public required string ClaveDesc { get; init; }
            public required string Icono { get; init; }
            public bool SoloExtranjero { get; init; }
            public FileResult? Archivo { get; set; }
        }

        private const string IconoDocumento = "M14 2H6C4.9 2 4 2.9 4 4V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V8L14 2ZM16 18H8V16H16V18ZM16 14H8V12H16V14ZM13 9V3.5L18.5 9H13Z";

        private readonly List<DocumentoExtra> documentosExtra = new()
        {
            new() { Tipo = "Certificado Apostillado", ClaveTitulo = "doc_extra_apostillado", ClaveDesc = "doc_extra_apostillado_desc", SoloExtranjero = true,
                    Icono = "M12 1L3 5V11C3 16.55 6.84 21.74 12 23C17.16 21.74 21 16.55 21 11V5L12 1ZM10 17L6 13L7.41 11.59L10 14.17L16.59 7.58L18 9L10 17Z" },
            new() { Tipo = "Declaración Jurada", ClaveTitulo = "doc_extra_declaracion", ClaveDesc = "doc_extra_declaracion_desc", SoloExtranjero = true, Icono = IconoDocumento },
            new() { Tipo = "Certificado médico", ClaveTitulo = "doc_extra_medico", ClaveDesc = "doc_extra_medico_desc",
                    Icono = "M19 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3ZM18 14H14V18H10V14H6V10H10V6H14V10H18V14Z" },
            new() { Tipo = "Certificado profesional", ClaveTitulo = "doc_extra_profesional", ClaveDesc = "doc_extra_profesional_desc",
                    Icono = "M5 13.18V17.18L12 21L19 17.18V13.18L12 17L5 13.18ZM12 3L1 9L12 15L21 10.09V17H23V9L12 3Z" },
            new() { Tipo = "Curso o capacitación", ClaveTitulo = "doc_extra_curso", ClaveDesc = "doc_extra_curso_desc",
                    Icono = "M18 2H6C4.9 2 4 2.9 4 4V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V4C20 2.9 19.1 2 18 2ZM6 4H11V12L8.5 10.5L6 12V4Z" },
            new() { Tipo = "Carta de recomendación", ClaveTitulo = "doc_extra_recomendacion", ClaveDesc = "doc_extra_recomendacion_desc",
                    Icono = "M20 4H4C2.9 4 2 4.9 2 6V18C2 19.1 2.9 20 4 20H20C21.1 20 22 19.1 22 18V6C22 4.9 21.1 4 20 4ZM20 8L12 13L4 8V6L12 11L20 6V8Z" },
        };

        private bool EsExtranjero() => NacionalidadSeleccionadaId() is not null && !EsDominicana();

        private bool DocumentoExtraVisible(DocumentoExtra d) => !d.SoloExtranjero || EsExtranjero();

        // El documento de identidad obligatorio es el pasaporte si es extranjero; los opcionales de extranjeros aparecen o se quitan.
        private void ActualizarDocumentosSegunNacionalidad()
        {
            LblCedulaTitulo.Text = Localizador.T(EsExtranjero() ? "pasaporte_pagina_datos" : "cedula_de_identidad_ambos_lados");
            RenderizarDocumentosExtra();
        }

        private void RenderizarDocumentosExtra()
        {
            ListaDocumentosExtra.Clear();
            foreach (var doc in documentosExtra)
            {
                if (!DocumentoExtraVisible(doc))
                {
                    doc.Archivo = null;
                    continue;
                }
                ListaDocumentosExtra.Add(CrearTarjetaDocumentoExtra(doc));
            }
        }

        private Border CrearTarjetaDocumentoExtra(DocumentoExtra doc)
        {
            var primario = (Color)Application.Current!.Resources["ColorPrimary"];
            var geometria = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter();

            var estado = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Microsoft.Maui.Controls.Shapes.Geometry)geometria.ConvertFromInvariantString("M19 13H13V19H11V13H5V11H11V5H13V11H19V13Z")!,
                Fill = primario, Aspect = Stretch.Uniform, WidthRequest = 14, HeightRequest = 14,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
            };
            var estadoCaja = new Border
            {
                WidthRequest = 32, HeightRequest = 32, StrokeThickness = 0, VerticalOptions = LayoutOptions.Center,
                BackgroundColor = (Color)Application.Current!.Resources["ColorBackground"],
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(), Content = estado
            };

            var archivo = new Label
            {
                Text = doc.Archivo?.FileName ?? Localizador.T(doc.ClaveDesc),
                FontFamily = "OpenSansRegular", FontSize = 12, LineBreakMode = LineBreakMode.MiddleTruncation,
                TextColor = doc.Archivo != null ? Tema.C("ColorSuccess") : (Color)Application.Current!.Resources["ColorTextMuted"]
            };

            var chip = new Border
            {
                StrokeThickness = 0, BackgroundColor = Tema.C("ColorSubtle"), Padding = new Thickness(8, 2),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }, VerticalOptions = LayoutOptions.Center,
                Content = new Label { Text = Localizador.T("opcional"), FontFamily = "OpenSansSemibold", FontSize = 10, TextColor = (Color)Application.Current!.Resources["ColorTextMuted"] }
            };
            var tituloFila = new HorizontalStackLayout { Spacing = 8 };
            tituloFila.Add(new Label { Text = Localizador.T(doc.ClaveTitulo), FontFamily = "OpenSansSemibold", FontSize = 14, TextColor = (Color)Application.Current!.Resources["ColorTextStrong"], VerticalOptions = LayoutOptions.Center });
            tituloFila.Add(chip);

            var textos = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
            textos.Add(tituloFila);
            textos.Add(archivo);

            var icono = new Border
            {
                WidthRequest = 42, HeightRequest = 42, StrokeThickness = 0, BackgroundColor = Tema.C("ColorPrimarySoft"), VerticalOptions = LayoutOptions.Center,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)geometria.ConvertFromInvariantString(doc.Icono)!,
                    Fill = primario, Aspect = Stretch.Uniform, WidthRequest = 20, HeightRequest = 20,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                }
            };

            var fila = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
            fila.Add(icono, 0);
            fila.Add(textos, 1);
            fila.Add(estadoCaja, 2);

            var tarjeta = new Border
            {
                Stroke = (Color)Application.Current!.Resources["ColorBorder"], StrokeThickness = 1.5, Padding = new Thickness(14),
                BackgroundColor = Tema.C("ColorSurface"), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
                Content = fila
            };
            if (doc.Archivo != null)
                MarcarDocumentoListo(tarjeta, estadoCaja, estado);

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                try
                {
                    var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = Localizador.T(doc.ClaveTitulo) });
                    if (result == null) return;
                    doc.Archivo = result;
                    archivo.Text = result.FileName;
                    archivo.TextColor = Tema.C("ColorSuccess");
                    MarcarDocumentoListo(tarjeta, estadoCaja, estado);
                }
                catch (Exception ex)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("no_se_pudo_seleccionar_el", ex.Message), Localizador.T("ok"));
                }
            };
            tarjeta.GestureRecognizers.Add(tap);
            return tarjeta;
        }

        // Documento elegido: la tarjeta pasa a verde con un check.
        private static void MarcarDocumentoListo(Border tarjeta, Border estadoCaja, Microsoft.Maui.Controls.Shapes.Path estado)
        {
            var verde = Tema.C("ColorSuccess");
            tarjeta.Stroke = verde;
            tarjeta.BackgroundColor = Tema.C("ColorSuccessSoft");
            estadoCaja.BackgroundColor = verde;
            estado.Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString("M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z")!;
            estado.Fill = Colors.White;
            estadoCaja.Scale = 0.7;
            _ = estadoCaja.ScaleTo(1, 220, Easing.CubicOut);
        }

        private async Task UpdateStepUI(bool animate)
        {
            StepIndicator.Text = Localizador.F("de", currentStepIndex + 1, currentFlow.Count);
            TopTitle.IsVisible = currentStepIndex > 0;

            var targetView = currentFlow[currentStepIndex];

            // Ocultar todos los demas suavemente
            foreach (var view in currentFlow)
            {
                if (view != targetView && view.IsVisible)
                {
                    if (animate)
                    {
                        await Task.WhenAll(view.FadeTo(0, 120, Easing.CubicOut), view.TranslateTo(-16, 0, 120, Easing.CubicOut));
                        view.TranslationX = 0;
                    }
                    view.IsVisible = false;
                }
            }

            if (!targetView.IsVisible)
            {
                targetView.Opacity = animate ? 0 : 1;
                targetView.IsVisible = true;

                if (animate)
                {
                    targetView.TranslationX = 24;
                    await Task.WhenAll(
                        targetView.FadeTo(1, 220, Easing.CubicOut),
                        targetView.TranslateTo(0, 0, 260, Easing.CubicOut));
                }
            }
        }
    }
}
