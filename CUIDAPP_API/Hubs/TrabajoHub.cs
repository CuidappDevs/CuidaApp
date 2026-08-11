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
    }
}
