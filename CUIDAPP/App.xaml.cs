using Microsoft.Extensions.DependencyInjection;
using CUIDAPP.Models.Chat;
using CUIDAPP.Services;
using CUIDAPP.Views.Chat;

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

            // Suscripción única para toda la vida de la app: avisa de mensajes de chat
            // nuevos sin importar en qué pantalla esté el usuario, salvo que ya tenga
            // abierta esa misma conversación (ChatPage se encarga de eso en ese caso).
            RealtimeService.MensajeNuevo += OnMensajeNuevoGlobal;
        }

        private void OnMensajeNuevoGlobal(Mensaje mensaje)
        {
            var miUsuarioId = Preferences.Default.Get("UserId", 0);
            if (mensaje.RemitenteId == miUsuarioId)
                return;

            if (mensaje.ConversacionId == ChatPage.ConversacionAbiertaId)
                return;

            var texto = mensaje.Tipo switch
            {
                "imagen" => "📷 Te enviaron una foto",
                "audio" => "🎤 Te enviaron una nota de voz",
                _ => mensaje.Contenido
            };

            GlobalNotifier.MostrarBanner("Nuevo mensaje", texto);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}