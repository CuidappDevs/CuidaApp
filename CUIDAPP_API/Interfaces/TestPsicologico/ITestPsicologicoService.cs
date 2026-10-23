using CUIDAPP_API.DTOs.TestPsicologico;

namespace CUIDAPP_API.Interfaces.TestPsicologico
{
    /// <summary>Test psicológico del registro de Care Partners (lado app).</summary>
    public interface ITestPsicologicoService
    {
        Task<EstadoTestDto> EstadoAsync(int tipoTrabajoId, string? documento, string? telefono);

        /// <summary>Abre un intento si el candidato está libre; si no, devuelve null y el estado que lo impide.</summary>
        Task<(TestParaResolverDto? Test, EstadoTestDto Estado)> IniciarAsync(IniciarTestDto dto);

        /// <summary>Califica el intento. Null si el token no existe o ya se había terminado.</summary>
        Task<ResultadoTestDto?> FinalizarAsync(FinalizarTestDto dto);
    }
}
