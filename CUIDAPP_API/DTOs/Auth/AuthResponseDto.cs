namespace CUIDAPP_API.DTOs.Auth
{
    public class AuthResponseDto
    {
        public required string Token { get; set; }
        public required string Email { get; set; }
        public int RolId { get; set; }
    }
}
