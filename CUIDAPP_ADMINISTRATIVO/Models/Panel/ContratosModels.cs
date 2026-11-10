namespace CUIDAPP_ADMINISTRATIVO.Models.Panel
{
    /// <summary>Estados de un contrato fijo (Contratos.Estado).</summary>
    public static class EstadoContrato
    {
        public static string Texto(int e) => e switch
        {
            1 => "Espera al Care Partner", 2 => "Espera al cliente", 3 => "Por firmar", 4 => "Activo", 5 => "En pausa",
            6 => "Terminado", 7 => "Rechazado", 8 => "Vencido", 9 => "Retirado", _ => "Desconocido"
        };

        public static string Tono(int e) => e switch
        {
            1 or 2 or 3 => "tone-warn", 4 => "tone-ok", 5 => "tone-info", _ => "tone-muted"
        };

        /// <summary>Permiso para contratar y domicilios: 1 en revisión, 2 aprobado, 3 rechazado, 4 revocado.</summary>
        public static string TextoRevision(int e) => e switch { 1 => "En revisión", 2 => "Aprobado", 3 => "Rechazado", 4 => "Revocado", _ => "—" };
        public static string TonoRevision(int e) => e switch { 1 => "tone-warn", 2 => "tone-ok", 3 or 4 => "tone-danger", _ => "tone-muted" };

        private static readonly System.Globalization.CultureInfo Es = new("es-DO");
        private static string Dia(int d) => d switch { 0 => "Dom", 1 => "Lun", 2 => "Mar", 3 => "Mié", 4 => "Jue", 5 => "Vie", 6 => "Sáb", _ => "?" };
        private static string Hora(string h) => TimeSpan.TryParse(h, out var t) ? DateTime.Today.Add(t).ToString("h:mm tt", Es) : h;

        /// <summary>"1|08:00|14:00;6|09:00|13:00" → "Lun 8:00 a. m.–2:00 p. m. · Sáb …".</summary>
        public static string Horario(string? texto) => string.IsNullOrWhiteSpace(texto) ? "—" : string.Join(" · ",
            texto.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(f => f.Split('|')).Where(p => p.Length == 3)
                 .Select(p => $"{Dia(int.Parse(p[0]))} {Hora(p[1])}–{Hora(p[2])}"));

        public static string Franja(FranjaContrato f) => $"{Dia(f.DiaSemana)} {Hora(f.HoraInicio)}–{Hora(f.HoraFin)}";
    }

    public class SolicitudContrato
    {
        public int ClienteId { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? DocumentoIdentidad { get; set; }
        public string? FotoUrl { get; set; }
        public DateTime? ClienteDesde { get; set; }
        public int Estado { get; set; }
        public string Motivo { get; set; } = "";
        public string? CedulaUrl { get; set; }
        public string? Referencia { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public DateTime? FechaRevision { get; set; }
        public string? ComentarioAdmin { get; set; }
        public string? RevisadoPor { get; set; }
        public int ServiciosCompletados { get; set; }
        public int ServiciosCancelados { get; set; }
        public decimal? CalificacionPromedio { get; set; }
        public int Sanciones { get; set; }
        public int DomiciliosAprobados { get; set; }
        public int ContratosActivos { get; set; }
    }

    public class DomicilioContrato
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public int PermisoEstado { get; set; }
        public string DomicilioNombre { get; set; } = "";
        public string Direccion { get; set; } = "";
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public string ComprobanteUrl { get; set; } = "";
        public string QuienVive { get; set; } = "";
        public int Estado { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public DateTime? FechaRevision { get; set; }
        public string? ComentarioAdmin { get; set; }
        public string? RevisadoPor { get; set; }
    }

    public class ContratoFila
    {
        public int Id { get; set; }
        public int Estado { get; set; }
        public string TipoServicio { get; set; } = "";
        public decimal TarifaHora { get; set; }
        public string MetodoPago { get; set; } = "";
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaActivacion { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public int CuidadorId { get; set; }
        public string CuidadorNombre { get; set; } = "";
        public string DomicilioNombre { get; set; } = "";
        public string Direccion { get; set; } = "";
        public string? Horario { get; set; }
        public int JornadasCompletadas { get; set; }
        public int Documentos { get; set; }
    }

    public class DetalleContrato
    {
        public ContratoCabecera? Contrato { get; set; }
        public List<FranjaContrato> Horario { get; set; } = new();
        public List<PropuestaContrato> Propuestas { get; set; } = new();
        public List<AusenciaContrato> Ausencias { get; set; } = new();
        public List<FirmaContrato> Firmas { get; set; } = new();
        public List<DocumentoContrato> Documentos { get; set; } = new();
        public List<JornadaContrato> Trabajos { get; set; } = new();
    }

    public class ContratoCabecera
    {
        public int Id { get; set; }
        public int Estado { get; set; }
        public string TipoServicio { get; set; } = "";
        public decimal TarifaHora { get; set; }
        public string MetodoPago { get; set; } = "";
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int DiasPreaviso { get; set; }
        public string? Nota { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaUltimaAccion { get; set; }
        public DateTime? FechaActivacion { get; set; }
        public DateTime? FechaTerminacion { get; set; }
        public int? TerminadoPor { get; set; }
        public string? MotivoTerminacion { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string? ClienteEmail { get; set; }
        public string? ClienteTelefono { get; set; }
        public string? ClienteDocumento { get; set; }
        public string? ClienteFoto { get; set; }
        public int CuidadorId { get; set; }
        public string CuidadorNombre { get; set; } = "";
        public string? CuidadorEmail { get; set; }
        public string? CuidadorTelefono { get; set; }
        public string? CuidadorDocumento { get; set; }
        public string? CuidadorFoto { get; set; }
        public string DomicilioNombre { get; set; } = "";
        public string Direccion { get; set; } = "";
        public string? QuienVive { get; set; }
    }

    public class FranjaContrato
    {
        public int DiaSemana { get; set; }
        public string HoraInicio { get; set; } = "";
        public string HoraFin { get; set; } = "";
    }

    public class PropuestaContrato
    {
        public int Version { get; set; }
        public int PropuestoPor { get; set; }
        public string PropuestoPorNombre { get; set; } = "";
        public decimal TarifaHora { get; set; }
        public DateTime FechaInicio { get; set; }
        public string Horario { get; set; } = "";
        public string? Nota { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class AusenciaContrato
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string MarcadoPorNombre { get; set; } = "";
        public string? Motivo { get; set; }
    }

    public class FirmaContrato
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string FirmaSvg { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string? Ip { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public string? Correo { get; set; }
    }

    public class DocumentoContrato
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public string Tipo { get; set; } = "";
        public string Hash { get; set; } = "";
        public DateTime Fecha { get; set; }
    }

    public class JornadaContrato
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string HoraInicio { get; set; } = "";
        public string HoraFin { get; set; } = "";
        public int Estado { get; set; }
        public decimal Tarifa { get; set; }
    }

    public class VerificacionDocumento
    {
        public bool Existe { get; set; }
        public bool Integro { get; set; }
        public string Hash { get; set; } = "";
    }
}
