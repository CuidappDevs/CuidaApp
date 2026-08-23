using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".pdf", ".m4a", ".mp3", ".wav", ".aac" };
        private const long TamanoMaximoBytes = 10 * 1024 * 1024; // 10 MB
        // Permite subcarpetas de un solo nivel (ej. "chat/42") para organizar por usuario,
        // sin abrir la puerta a "..", rutas absolutas, ni traversal fuera de uploads/usuarios.
        private static readonly Regex CarpetaValida = new(@"^[a-zA-Z0-9\-_]{1,40}(\/[a-zA-Z0-9\-_]{1,40})?$", RegexOptions.Compiled);

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // Sube un archivo a la carpeta del usuario/registro indicada (una carpeta por usuario en wwwroot/uploads/usuarios/{carpeta}).
        // Si no se especifica carpeta, se usa una carpeta temporal por fecha.
        [HttpPost]
        public async Task<IActionResult> SubirArchivo(IFormFile file, [FromForm] string? carpeta)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No se recibió ningún archivo.");

            if (file.Length > TamanoMaximoBytes)
                return BadRequest("El archivo excede el tamaño máximo permitido (10 MB).");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!ExtensionesPermitidas.Contains(extension))
                return BadRequest("Tipo de archivo no permitido. Usa JPG, PNG, PDF o un formato de audio soportado (M4A, MP3, WAV, AAC).");

            var nombreCarpeta = !string.IsNullOrWhiteSpace(carpeta) && CarpetaValida.IsMatch(carpeta)
                ? carpeta
                : DateTime.UtcNow.ToString("yyyyMM");

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var directorioDestino = Path.Combine(webRoot, "uploads", "usuarios", nombreCarpeta);
            Directory.CreateDirectory(directorioDestino);

            var nombreArchivo = $"{Guid.NewGuid()}{extension}";
            var rutaCompleta = Path.Combine(directorioDestino, nombreArchivo);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var urlRelativa = $"/uploads/usuarios/{nombreCarpeta}/{nombreArchivo}";

            return Ok(new { url = urlRelativa });
        }
    }
}
