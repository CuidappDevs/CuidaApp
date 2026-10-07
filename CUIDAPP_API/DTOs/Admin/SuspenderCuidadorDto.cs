namespace CUIDAPP_API.DTOs.Admin
{
    public class SuspenderCuidadorDto
    {
        public int AdminId { get; set; }
        public string Motivo { get; set; } = "";

        // Null = suspensión indefinida. Debe incluir offset; se convierte a UTC.
        public DateTimeOffset? FechaFinUtc { get; set; }

        public const int MotivoMin = 10;
        public const int MotivoMax = 500;
        public static readonly TimeSpan MargenMinimoFechaFin = TimeSpan.FromMinutes(5);

        // Devuelve errores por campo; vacío si el DTO es válido. Un motivo corto/largo es
        // validación de forma (400 VALIDATION_ERROR); la base repite la regla por INVALID_REASON.
        public Dictionary<string, string[]> Validar(int usuarioId, DateTimeOffset ahoraUtc)
        {
            var errores = new Dictionary<string, string[]>();
            if (usuarioId <= 0) errores["usuarioId"] = new[] { "El id de usuario debe ser positivo." };
            if (AdminId <= 0) errores["adminId"] = new[] { "El id de administrador debe ser positivo." };

            var largo = (Motivo ?? "").Trim().Length;
            if (largo < MotivoMin || largo > MotivoMax)
                errores["motivo"] = new[] { $"El motivo debe tener entre {MotivoMin} y {MotivoMax} caracteres." };

            if (FechaFinUtc.HasValue && FechaFinUtc.Value.ToUniversalTime() < ahoraUtc.Add(MargenMinimoFechaFin))
                errores["fechaFinUtc"] = new[] { "La fecha de fin debe ser al menos 5 minutos posterior a la hora actual." };

            return errores;
        }
    }
}
