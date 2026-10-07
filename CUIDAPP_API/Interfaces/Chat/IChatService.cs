using CUIDAPP_API.DTOs.Chat;

namespace CUIDAPP_API.Interfaces.Chat
{
    public interface IChatService
    {
        Task<ConversacionDto> ObtenerOCrearConversacionAsync(int trabajoId);
        Task<IEnumerable<MensajeDto>> ObtenerMensajesAsync(int conversacionId);
        Task<MensajeDto> EnviarMensajeAsync(EnviarMensajeDto dto);
        Task MarcarLeidosAsync(MarcarLeidosDto dto);
        Task<IEnumerable<NoLeidosDto>> ContarNoLeidosAsync(int usuarioId);

        /// <summary>Avisa al otro participante de la conversación que el usuario está (o dejó de estar) escribiendo.</summary>
        Task NotificarEscribiendoAsync(int conversacionId, int usuarioId, bool escribiendo);
    }
}
