namespace CUIDAPP.Models.Auth
{
    public class ApiError
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
        public Dictionary<string, string[]>? Errors { get; set; }
        public SuspensionInfo? Suspension { get; set; }
    }

    public class SuspensionInfo
    {
        public string Motivo { get; set; } = "";
        public string Tipo { get; set; } = "";          // INDEFINIDA | TEMPORAL
        public DateTimeOffset? FechaFinUtc { get; set; }
    }
}
