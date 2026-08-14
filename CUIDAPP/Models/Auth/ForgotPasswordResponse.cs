namespace CUIDAPP.Models.Auth
{
    public class ForgotPasswordResponse
    {
        public string Message { get; set; } = "";
        public Guid ResetToken { get; set; }
        public string Code { get; set; } = "";
    }
}
