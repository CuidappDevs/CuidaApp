namespace CUIDAPP_API.Interfaces.Usuario
{
    public interface IUsuarioService
    {
        /// <summary>Si el usuario ya vio la bienvenida animada de la app. Null si no existe.</summary>
        Task<bool?> ObtenerBienvenidaVistaAsync(int usuarioId);

        /// <summary>Marca la bienvenida como vista (una vez por cuenta, en cualquier teléfono).</summary>
        Task<bool> MarcarBienvenidaVistaAsync(int usuarioId);
    }
}
