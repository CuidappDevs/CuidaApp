namespace CUIDAPP_API.Services.Email
{
    public class EmailCredentials
    {
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; }
        public string SmtpUser { get; set; } = "";
        public string SmtpPass { get; set; } = "";
    }
}
