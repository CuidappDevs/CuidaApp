namespace CUIDAPP.Models.Contrato
{
    /// <summary>
    /// Estados del contrato fijo (Contratos.Estado): 1 espera al Care Partner, 2 espera al cliente,
    /// 3 por firmar, 4 activo, 5 en pausa, 6 terminado, 7 rechazado, 8 vencido, 9 retirado.
    /// Permiso / domicilio: 0 sin solicitar, 1 en revisión, 2 aprobado, 3 rechazado, 4 revocado.
    /// </summary>
    public class PermisoContrato
    {
        public int Estado { get; set; }
        public string? Motivo { get; set; }
        public string? Referencia { get; set; }
        public DateTime? FechaSolicitud { get; set; }
        public DateTime? FechaRevision { get; set; }
        public string? ComentarioAdmin { get; set; }
        public DateTime? PuedeVolverASolicitar { get; set; }
    }

    public class DomicilioPermiso
    {
        public int UbicacionId { get; set; }
        public string Nombre { get; set; } = "";
        public string Direccion { get; set; } = "";
        public int? DomicilioId { get; set; }
        public int Estado { get; set; }
        public string? QuienVive { get; set; }
        public string? ComentarioAdmin { get; set; }
    }

    public class PermisoYDomicilios
    {
        public PermisoContrato? Permiso { get; set; }
        public List<DomicilioPermiso> Domicilios { get; set; } = new();
    }

    public class EstadoProponer
    {
        public int PermisoEstado { get; set; }
        public bool AceptaContratos { get; set; }
        public decimal TarifaHora { get; set; }
        public string? Especialidad { get; set; }
        public string? CuidadorNombre { get; set; }
    }

    public class DomicilioAprobado
    {
        public int UbicacionId { get; set; }
        public string Nombre { get; set; } = "";
        public string Direccion { get; set; } = "";
    }

    public class PuedeProponer
    {
        public EstadoProponer? Estado { get; set; }
        public List<DomicilioAprobado> Domicilios { get; set; } = new();
    }

    public class ConfigCuidadorContratos
    {
        public bool AceptaContratos { get; set; }
        public decimal? TarifaHora { get; set; }
    }

    public class FranjaContrato
    {
        public int DiaSemana { get; set; }
        public string HoraInicio { get; set; } = "";
        public string HoraFin { get; set; } = "";
    }

    public class ProponerContratoRequest
    {
        public int ClienteId { get; set; }
        public int CuidadorId { get; set; }
        public int UbicacionId { get; set; }
        public string TipoServicio { get; set; } = "";
        public decimal TarifaHora { get; set; }
        public string MetodoPago { get; set; } = "";
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public List<FranjaContrato> Horario { get; set; } = new();
        public string? Nota { get; set; }
    }

    public class ResponderContratoRequest
    {
        public int UsuarioId { get; set; }
        public string Accion { get; set; } = "";
        public decimal? TarifaHora { get; set; }
        public DateTime? FechaInicio { get; set; }
        public List<FranjaContrato>? Horario { get; set; }
        public string? Nota { get; set; }
    }

    public class ContratoResumen
    {
        public int Id { get; set; }
        public int Estado { get; set; }
        public string TipoServicio { get; set; } = "";
        public decimal TarifaHora { get; set; }
        public string MetodoPago { get; set; } = "";
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string MiRol { get; set; } = "";
        public int OtroId { get; set; }
        public string OtroNombre { get; set; } = "";
        public string? OtroFoto { get; set; }
        public string DomicilioNombre { get; set; } = "";
        public string Direccion { get; set; } = "";
        public string? Horario { get; set; }
        public bool RequiereAccion { get; set; }
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
        public DateTime? FechaTerminacion { get; set; }
        public string? MotivoTerminacion { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string? ClienteFoto { get; set; }
        public string? ClienteTelefono { get; set; }
        public int CuidadorId { get; set; }
        public string CuidadorNombre { get; set; } = "";
        public string? CuidadorFoto { get; set; }
        public string? CuidadorTelefono { get; set; }
        public string DomicilioNombre { get; set; } = "";
        public string Direccion { get; set; } = "";
        public string? QuienVive { get; set; }
        public string? MiRol { get; set; }
    }

    public class PropuestaContrato
    {
        public int Version { get; set; }
        public int PropuestoPor { get; set; }
        public string PropuestoPorNombre { get; set; } = "";
        public decimal TarifaHora { get; set; }
        public DateTime FechaInicio { get; set; }
        public string? Nota { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class AusenciaContrato
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int MarcadoPor { get; set; }
        public string MarcadoPorNombre { get; set; } = "";
        public string? Motivo { get; set; }
    }

    public class FirmaContrato
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public DateTime Fecha { get; set; }
    }

    public class DocumentoContrato
    {
        public int Id { get; set; }
        public int Version { get; set; }
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

    public class ContratoDetalle
    {
        public ContratoCabecera? Contrato { get; set; }
        public List<FranjaContrato> Horario { get; set; } = new();
        public List<PropuestaContrato> Propuestas { get; set; } = new();
        public List<AusenciaContrato> Ausencias { get; set; } = new();
        public List<FirmaContrato> Firmas { get; set; } = new();
        public List<DocumentoContrato> Documentos { get; set; } = new();
        public List<JornadaContrato> Trabajos { get; set; } = new();
    }

    public class FirmarContratoRequest
    {
        public int UsuarioId { get; set; }
        public string Codigo { get; set; } = "";
        public string Trazos { get; set; } = "";
        public double Ancho { get; set; }
        public double Alto { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
    }
}
