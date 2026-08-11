using Microsoft.AspNetCore.SignalR;
using CUIDAPP_API.Hubs;

namespace CUIDAPP_API.Services.Realtime
{
    public class TrabajoNotifier : ITrabajoNotifier
    {
        private readonly IHubContext<TrabajoHub> _hubContext;

        public TrabajoNotifier(IHubContext<TrabajoHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotificarAsync(int usuarioId, string evento, object payload)
        {
            return _hubContext.Clients.Group(TrabajoHub.GrupoUsuario(usuarioId)).SendAsync(evento, payload);
        }

        public Task NotificarGlobalAsync(string evento, object payload)
        {
            return _hubContext.Clients.All.SendAsync(evento, payload);
        }
    }
}
