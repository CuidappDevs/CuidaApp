using CUIDAPP_API.Interfaces.Chat;
using Microsoft.AspNetCore.SignalR;

namespace CUIDAPP_API.Hubs
{
    // Hub de tiempo real: cada cliente (cliente o cuidador) se une a un grupo con su propio
    // UsuarioId al conectar, y el backend envía eventos a ese grupo cuando algo le corresponde
    // (nueva solicitud, trabajo aceptado, cambio de estado, etc.).
    public class TrabajoHub : Hub
    {
        public static string GrupoUsuario(int usuarioId) => $"user-{usuarioId}";

        public async Task Unirse(int usuarioId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GrupoUsuario(usuarioId));
        }

        private readonly IChatService _chatService;

        public TrabajoHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        // Indicador de "escribiendo" en el chat: el cliente lo invoca mientras el usuario
        // escribe (con límite de frecuencia) y al dejar de escribir; se reenvía al otro
        // participante como el evento "UsuarioEscribiendo".
        public async Task Escribiendo(int conversacionId, int usuarioId, bool escribiendo)
        {
            await _chatService.NotificarEscribiendoAsync(conversacionId, usuarioId, escribiendo);
        }
    }
}
