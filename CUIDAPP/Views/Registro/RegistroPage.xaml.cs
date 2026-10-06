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
        private int selectedJob = 2; // Default Niñera
        private int selectedPay = 2; // Default Billetera
        private readonly ApiService _apiService = new ApiService();

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
            UpdateJobOptionsUI();
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
                HolaBrazo.Rotation = -8 + 18 * fase;
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
                if (string.IsNullOrWhiteSpace(EntryPassword.Text) || EntryPassword.Text.Length < 6)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("la_contrasena_debe_tener_al_2"), Localizador.T("ok"));
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
            }
            else if (currentView == PasoTrabajo)
            {
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
            // Mostrar modal de carga con texto de "Cargando..."
            OverlayTitle.Text = Localizador.T("cargando");
            OverlayMessage.Text = Localizador.T("subiendo_documentos");
            OverlayIcon.IsVisible = false; // Ocultar el icono de check
            OverlayExito.IsVisible = true;
            await OverlayExito.FadeTo(1, 300);

            // Subir todos los archivos elegidos a la carpeta de este usuario en el servidor.
            string fotoUrl = "";
            string cedulaUrl = "";
            string antecedentesUrl = "";

            if (fotoFile != null)
            {
                fotoUrl = await _apiService.UploadFileAsync(fotoFile.FullPath, carpetaUsuario) ?? "";
                if (fotoUrl == "")
                {
                    await MostrarErrorSubida(Localizador.T("err_subir_foto"));
                    return;
                }
            }

            if (selectedRole == "Cuidador")
            {
                if (cedulaFile != null)
                {
                    cedulaUrl = await _apiService.UploadFileAsync(cedulaFile.FullPath, carpetaUsuario) ?? "";
                    if (cedulaUrl == "")
                    {
                        await MostrarErrorSubida(Localizador.T("err_subir_cedula"));
                        return;
                    }
                }

                if (antecedentesFile != null)
                {
                    antecedentesUrl = await _apiService.UploadFileAsync(antecedentesFile.FullPath, carpetaUsuario) ?? "";
                    if (antecedentesUrl == "")
                    {
                        await MostrarErrorSubida(Localizador.T("err_subir_carta"));
                        return;
                    }
                }
            }

            OverlayMessage.Text = Localizador.T("enviando_datos_al_servidor");

            bool success = false;

            if (selectedRole == "Cliente")
            {
                var request = new RegisterClientRequest
                {
                    Email = EntryEmail.Text ?? "",
                    Password = EntryPassword.Text ?? "",
                    NombreCompleto = EntryNombre.Text ?? "",
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
                string especialidad = selectedJob == 1 ? "Limpieza del hogar" : (selectedJob == 2 ? "Niñera / Cuidadora" : "Cuidadora de adultos");
                string metodoCobro = selectedPay == 1 ? "Cuenta bancaria" : (selectedPay == 2 ? "Billetera CuidApp" : "Efectivo");
                decimal.TryParse(EntryTarifa.Text, out decimal tarifa);

                var request = new RegisterCaregiverRequest
                {
                    Email = EntryEmail.Text ?? "",
                    Password = EntryPassword.Text ?? "",
                    NombreCompleto = EntryNombre.Text ?? "",
                    Bio = EntryBio.Text ?? "",
                    Especialidad = especialidad,
                    TarifaHora = tarifa,
                    MetodoCobro = metodoCobro,
                    FotoUrl = fotoUrl,
                    CedulaUrl = cedulaUrl,
                    CartaAntecedentesUrl = antecedentesUrl
                };
                success = await _apiService.RegisterCuidadorAsync(request);
            }

            if (success)
            {
                // Cambiar el overlay a modo Éxito
                OverlayTitle.Text = Localizador.T("exito_2");
                OverlayMessage.Text = Localizador.T("registro_completado");
                OverlayIcon.IsVisible = true;

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
                    LblFotoFileName.TextColor = Color.FromArgb("#2E7D32");
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
                    LblCedulaFileName.TextColor = Color.FromArgb("#2E7D32");
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
                    LblAntecedentesFileName.TextColor = Color.FromArgb("#2E7D32");
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

        private void OnJobOptionTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string param && int.TryParse(param, out int jobIndex))
            {
                selectedJob = jobIndex;
                UpdateJobOptionsUI();
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
            tarjeta.BackgroundColor = activo ? Color.FromArgb("#EAF1FB") : Colors.White;
            cajaIcono.BackgroundColor = activo ? primario : R("ColorBackground");
            icono.Fill = activo ? Colors.White : R("ColorTextMuted");
            titulo.TextColor = activo ? primario : R("ColorTextStrong");
            check.Stroke = activo ? primario : R("ColorBorder");
            check.BackgroundColor = activo ? primario : Colors.White;

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

        private void UpdateJobOptionsUI()
        {
            PintarOpcionRol(JobOption1, JobIconBox1, JobIcon1, JobText1, JobCheck1, JobCheckMark1, selectedJob == 1);
            PintarOpcionRol(JobOption2, JobIconBox2, JobIcon2, JobText2, JobCheck2, JobCheckMark2, selectedJob == 2);
            PintarOpcionRol(JobOption3, JobIconBox3, JobIcon3, JobText3, JobCheck3, JobCheckMark3, selectedJob == 3);
        }

        private void UpdatePayOptionsUI()
        {
            PintarOpcionRol(PayOption1, PayIconBox1, PayIcon1, PayText1, PayCheck1, PayCheckMark1, selectedPay == 1);
            PintarOpcionRol(PayOption2, PayIconBox2, PayIcon2, PayText2, PayCheck2, PayCheckMark2, selectedPay == 2);
            PintarOpcionRol(PayOption3, PayIconBox3, PayIcon3, PayText3, PayCheck3, PayCheckMark3, selectedPay == 3);
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
                b.BackgroundColor = Colors.White;
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

        // Documento elegido: la tarjeta pasa a verde con un check.
        private static void MarcarDocumentoListo(Border tarjeta, Border estadoCaja, Microsoft.Maui.Controls.Shapes.Path estado)
        {
            var verde = Color.FromArgb("#2E7D32");
            tarjeta.Stroke = verde;
            tarjeta.BackgroundColor = Color.FromArgb("#F1F8F2");
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
