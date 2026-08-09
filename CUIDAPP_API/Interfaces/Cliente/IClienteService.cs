using CUIDAPP_API.DTOs.Cliente;

namespace CUIDAPP_API.Interfaces.Cliente
{
    public interface IClienteService
    {
        Task<PerfilClienteDto?> ObtenerPerfilAsync(int clienteId);
    }
}
