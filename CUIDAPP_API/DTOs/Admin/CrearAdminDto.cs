namespace CUIDAPP_API.DTOs.Admin
{
    public class CrearAdminDto
    {
        [System.ComponentModel.DataAnnotations.Range(1, 6)]
        public int NivelAdmin { get; set; } = 2;
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string NombreCompleto { get; set; } = "";
    }
}
