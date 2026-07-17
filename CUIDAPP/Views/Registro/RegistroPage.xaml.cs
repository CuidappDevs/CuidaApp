using Microsoft.Maui.Controls;

namespace CUIDAPP.Views.Registro
{
    public partial class RegistroPage : ContentPage
    {
        private int currentStepIndex = 0;
        private List<View> currentFlow = new List<View>();
        
        private string selectedRole = "Cuidador"; // Default
        private int selectedJob = 2; // Default Niñera
        private int selectedPay = 2; // Default Billetera

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

        private async Task FinishRegistration()
        {
            OverlayExito.IsVisible = true;
            await OverlayExito.FadeTo(1, 300);
            
            // Simular petición a BD
            await Task.Delay(2000);
            
            await OverlayExito.FadeTo(0, 200);
            OverlayExito.IsVisible = false;

            if (selectedRole == "Cuidador")
            {
                await Shell.Current.GoToAsync("CuidadorDashboardPage");
            }
            else
            {
                await Shell.Current.GoToAsync("..");
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
