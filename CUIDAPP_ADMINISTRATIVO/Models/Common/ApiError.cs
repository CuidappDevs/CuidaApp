namespace CUIDAPP_ADMINISTRATIVO.Models.Common
{
    public class ApiError
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
