namespace CUIDAPP_API.Services.Realtime
{
    public interface ITrabajoNotifier
    {
        Task NotificarAsync(int usuarioId, string evento, object payload);
        Task NotificarGlobalAsync(string evento, object payload);
    }
}
