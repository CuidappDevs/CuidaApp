using CUIDAPP_API.DTOs.Cuidador;

namespace CUIDAPP_API.Interfaces.Cuidador
{
    public interface ICuidadorService
    {
        Task<int> SubirDocumentoAsync(SubirDocumentoDto dto);
    }
}
