using Microsoft.Extensions.DependencyInjection;

namespace CUIDAPP
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Red de seguridad: si algo revienta sin try/catch en un handler async void,
            // lo dejamos registrado en el Output de Visual Studio en vez de que la app crashee
            // silenciosamente sin dejar rastro del motivo real.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.ExceptionObject}");

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {e.Exception}");
                e.SetObserved();
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}