namespace CUIDAPP_API.DTOs.Admin
{
    public class ReactivarCuidadorDto
    {
        public int AdminId { get; set; }

        public Dictionary<string, string[]> Validar(int usuarioId)
        {
            var errores = new Dictionary<string, string[]>();
            if (usuarioId <= 0) errores["usuarioId"] = new[] { "El id de usuario debe ser positivo." };
            if (AdminId <= 0) errores["adminId"] = new[] { "El id de administrador debe ser positivo." };
            return errores;
        }
    }
}
