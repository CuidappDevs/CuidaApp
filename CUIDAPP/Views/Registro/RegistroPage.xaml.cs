using Microsoft.Maui.Controls;
using CUIDAPP.Services;
using CUIDAPP.Models.Auth;

namespace CUIDAPP.Views.Registro
{
    public partial class RegistroPage : ContentPage
    {
        private int currentStepIndex = 0;
        private List<View> currentFlow = new List<View>();
        
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
                    await DisplayAlert("Error", "Por favor ingresa un correo electrónico válido.", "OK");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(EntryPassword.Text) || EntryPassword.Text.Length < 6)
                {
                    await DisplayAlert("Error", "La contraseña debe tener al menos 6 caracteres.", "OK");
                    return false;
                }
            }
            else if (currentView == PasoNombre)
            {
                if (string.IsNullOrWhiteSpace(EntryNombre.Text))
                {
                    await DisplayAlert("Error", "El nombre completo es obligatorio.", "OK");
                    return false;
                }
            }
            else if (currentView == PasoTrabajo)
            {
                if (string.IsNullOrWhiteSpace(EntryTarifa.Text) || !decimal.TryParse(EntryTarifa.Text, out decimal tarifa) || tarifa <= 0)
                {
                    await DisplayAlert("Error", "Por favor ingresa una tarifa por hora válida (ej. 15.00).", "OK");
                    return false;
                }
            }
            else if (currentView == PasoDireccion)
            {
                if (string.IsNullOrWhiteSpace(EntryDireccion.Text) ||
                    string.IsNullOrWhiteSpace(EntryEmergenciaNombre.Text) ||
                    string.IsNullOrWhiteSpace(EntryEmergenciaTelefono.Text))
                {
                    await DisplayAlert("Error", "Por favor completa todos los campos de contacto y emergencia.", "OK");
                    return false;
                }
            }
            else if (currentView == PasoFoto)
            {
                if (fotoFile == null)
                {
                    await DisplayAlert("Error", "Selecciona tu foto de perfil.", "OK");
                    return false;
                }
            }
            else if (currentView == PasoDocumentos)
            {
                if (cedulaFile == null || antecedentesFile == null)
                {
                    await DisplayAlert("Error", "Selecciona ambos documentos.", "OK");
                    return false;
                }
            }

            return true;
        }

        private async Task FinishRegistration()
        {
            // Mostrar modal de carga con texto de "Cargando..."
            OverlayTitle.Text = "Cargando...";
            OverlayMessage.Text = "Subiendo documentos...";
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
                    await MostrarErrorSubida("No se pudo subir tu foto de perfil.");
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
                        await MostrarErrorSubida("No se pudo subir tu cédula.");
                        return;
                    }
                }

                if (antecedentesFile != null)
                {
                    antecedentesUrl = await _apiService.UploadFileAsync(antecedentesFile.FullPath, carpetaUsuario) ?? "";
                    if (antecedentesUrl == "")
                    {
                        await MostrarErrorSubida("No se pudo subir tu carta de antecedentes.");
                        return;
                    }
                }
            }

            OverlayMessage.Text = "Enviando datos al servidor";

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
                success = await _apiService.RegisterClienteAsync(request);
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
                OverlayTitle.Text = "¡Éxito!";
                OverlayMessage.Text = "Registro completado";
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
                await DisplayAlert("Error", "Ocurrió un error al conectar con el servidor. Verifica que la API esté corriendo.", "OK");
            }
        }

        private async Task MostrarErrorSubida(string mensaje)
        {
            await OverlayExito.FadeTo(0, 200);
            OverlayExito.IsVisible = false;
            await DisplayAlert("Error", $"{mensaje} Verifica tu conexión e intenta de nuevo.", "OK");
        }

        private async void OnPickFotoTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecciona una imagen de perfil",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    fotoFile = result;
                    LblFotoFileName.Text = result.FileName;
                    LblFotoFileName.TextColor = Color.FromArgb("#10B981");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo seleccionar la imagen: {ex.Message}", "OK");
            }
        }

        private async void OnPickCedulaTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecciona el documento de Cédula"
                });

                if (result != null)
                {
                    cedulaFile = result;
                    LblCedulaFileName.Text = result.FileName;
                    LblCedulaFileName.TextColor = Color.FromArgb("#10B981");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo seleccionar el archivo: {ex.Message}", "OK");
            }
        }

        private async void OnPickAntecedentesTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecciona la Carta de Antecedentes"
                });

                if (result != null)
                {
                    antecedentesFile = result;
                    LblAntecedentesFileName.Text = result.FileName;
                    LblAntecedentesFileName.TextColor = Color.FromArgb("#10B981");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo seleccionar el archivo: {ex.Message}", "OK");
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
            RoleOptionCliente.Stroke = RoleOptionCuidador.Stroke = Color.FromArgb("#E5E7EB");
            RoleOptionCliente.BackgroundColor = RoleOptionCuidador.BackgroundColor = Colors.White;
            RoleIconCliente.Fill = RoleIconCuidador.Fill = Color.FromArgb("#374151");
            RoleTextCliente.TextColor = RoleTextCuidador.TextColor = Color.FromArgb("#374151");

            var activeStroke = Color.FromArgb("#4024A2");
            var activeBg = Color.FromArgb("#F3E8FF");

            if (selectedRole == "Cliente")
            {
                RoleOptionCliente.Stroke = activeStroke; RoleOptionCliente.BackgroundColor = activeBg;
                RoleIconCliente.Fill = activeStroke; RoleTextCliente.TextColor = activeStroke;
            }
            else
            {
                RoleOptionCuidador.Stroke = activeStroke; RoleOptionCuidador.BackgroundColor = activeBg;
                RoleIconCuidador.Fill = activeStroke; RoleTextCuidador.TextColor = activeStroke;
            }
        }

        private void UpdateJobOptionsUI()
        {
            JobOption1.Stroke = JobOption3.Stroke = Color.FromArgb("#E5E7EB");
            JobOption1.BackgroundColor = JobOption3.BackgroundColor = Colors.White;
            JobIcon1.Fill = JobIcon3.Fill = JobText1.TextColor = JobText3.TextColor = Color.FromArgb("#374151");

            JobOption2.Stroke = Color.FromArgb("#E5E7EB");
            JobOption2.BackgroundColor = Colors.White;
            JobIcon2.Fill = JobText2.TextColor = Color.FromArgb("#374151");

            var activeStroke = Color.FromArgb("#4024A2");
            var activeBg = Color.FromArgb("#F3E8FF");

            switch (selectedJob)
            {
                case 1:
                    JobOption1.Stroke = activeStroke; JobOption1.BackgroundColor = activeBg;
                    JobIcon1.Fill = activeStroke; JobText1.TextColor = activeStroke;
                    break;
                case 2:
                    JobOption2.Stroke = activeStroke; JobOption2.BackgroundColor = activeBg;
                    JobIcon2.Fill = activeStroke; JobText2.TextColor = activeStroke;
                    break;
                case 3:
                    JobOption3.Stroke = activeStroke; JobOption3.BackgroundColor = activeBg;
                    JobIcon3.Fill = activeStroke; JobText3.TextColor = activeStroke;
                    break;
            }
        }

        private void UpdatePayOptionsUI()
        {
            PayOption1.Stroke = PayOption3.Stroke = Color.FromArgb("#E5E7EB");
            PayOption1.BackgroundColor = PayOption3.BackgroundColor = Colors.White;
            PayIcon1.Fill = PayIcon3.Fill = PayText1.TextColor = PayText3.TextColor = Color.FromArgb("#374151");

            PayOption2.Stroke = Color.FromArgb("#E5E7EB");
            PayOption2.BackgroundColor = Colors.White;
            PayIcon2.Fill = PayText2.TextColor = Color.FromArgb("#374151");

            var activeStroke = Color.FromArgb("#4024A2");
            var activeBg = Color.FromArgb("#F3E8FF");

            switch (selectedPay)
            {
                case 1:
                    PayOption1.Stroke = activeStroke; PayOption1.BackgroundColor = activeBg;
                    PayIcon1.Fill = activeStroke; PayText1.TextColor = activeStroke;
                    break;
                case 2:
                    PayOption2.Stroke = activeStroke; PayOption2.BackgroundColor = activeBg;
                    PayIcon2.Fill = activeStroke; PayText2.TextColor = activeStroke;
                    break;
                case 3:
                    PayOption3.Stroke = activeStroke; PayOption3.BackgroundColor = activeBg;
                    PayIcon3.Fill = activeStroke; PayText3.TextColor = activeStroke;
                    break;
            }
        }

        private async Task UpdateStepUI(bool animate)
        {
            StepIndicator.Text = $"{currentStepIndex + 1} de {currentFlow.Count}";
            TopTitle.IsVisible = currentStepIndex > 0;

            var targetView = currentFlow[currentStepIndex];

            // Ocultar todos los demas suavemente
            foreach (var view in currentFlow)
            {
                if (view != targetView && view.IsVisible)
                {
                    if (animate)
                    {
                        await view.FadeTo(0, 150);
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
                    await targetView.FadeTo(1, 150);
                }
            }
        }
    }
}
