namespace CUIDAPP_ADMINISTRATIVO.Models.Panel
{
    public class Pagina<T>
    {
        public int Total { get; set; }
        public List<T> Filas { get; set; } = new();
    }

    /// <summary>Estados de un servicio (Trabajos.Estado) con su texto y colores de pastilla.</summary>
    public static class EstadoServicio
    {
        public static readonly (int Id, string Texto)[] Todos =
        {
            (1, "Por aceptar"), (2, "Aceptado"), (3, "En curso"), (7, "Por confirmar"),
            (4, "Completado"), (5, "Cancelado"), (6, "Rechazado")
        };

        public static string Texto(int estado) => Todos.FirstOrDefault(e => e.Id == estado).Texto ?? "Desconocido";

        /// <summary>Clase CSS de la pastilla (ver admin.css: .tone-*).</summary>
        public static string Tono(int estado) => estado switch
        {
            1 => "tone-warn",
            2 => "tone-info",
            3 => "tone-live",
            7 => "tone-violet",
            4 => "tone-ok",
            5 => "tone-danger",
            6 => "tone-muted",
            _ => "tone-muted"
        };
    }

    // ---------- Dashboard
    public class DashboardPanel
    {
        public Indicadores Indicadores { get; set; } = new();
        public List<PuntoSerie> Serie { get; set; } = new();
        public List<ActividadItem> Actividad { get; set; } = new();
        public List<VerificacionResumen> Verificacion { get; set; } = new();
    }

    public class Indicadores
    {
        public int ServiciosHoy { get; set; }
        public int ServiciosEnCurso { get; set; }
        public int ServiciosPorAceptar { get; set; }
        public int CuidadoresVisibles { get; set; }
        public int CuidadoresPorVerificar { get; set; }
        public int CuidadoresTotal { get; set; }
        public int ClientesTotal { get; set; }
        public int RegistrosSemana { get; set; }
        public int SosPendientes { get; set; }
        public int TicketsAbiertos { get; set; }
        public int PagosPorAutorizar { get; set; }
        public int PagosPorEnviar { get; set; }
        public decimal IngresosSemana { get; set; }
        public decimal IngresosSemanaAnterior { get; set; }
        public decimal CalificacionPromedio { get; set; }
    }

    public class PuntoSerie
    {
        public DateTime Dia { get; set; }
        public int Servicios { get; set; }
        public int Completados { get; set; }
        public decimal Ingresos { get; set; }
    }

    public class ActividadItem
    {
        public string Tipo { get; set; } = "";
        public string Texto { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string? Enlace { get; set; }
    }

    public class VerificacionResumen
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? FotoUrl { get; set; }
        public DateTime? FechaCreacion { get; set; }
        public int DocsPendientes { get; set; }
        public int DocsRechazados { get; set; }
    }

    // ---------- Servicios
    public class ServicioFila
    {
        public int Id { get; set; }
        public string TipoServicio { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string HoraInicio { get; set; } = "";
        public string HoraFin { get; set; } = "";
        public string? Direccion { get; set; }
        public int Estado { get; set; }
        public decimal Tarifa { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public int CuidadorId { get; set; }
        public string CuidadorNombre { get; set; } = "";
        public bool PagoDisputado { get; set; }
        public bool RechazadoPorCliente { get; set; }
        public bool TieneSos { get; set; }
    }

    public class DetalleServicio
    {
        public ServicioInfo Servicio { get; set; } = new();
        public List<TareaInfo> Tareas { get; set; } = new();
        public List<ActividadInfo> Actividades { get; set; } = new();
        public List<PagoInfo> Pagos { get; set; } = new();
        public List<CalificacionInfo> Calificaciones { get; set; } = new();
        public List<MensajeInfo> Mensajes { get; set; } = new();
        public List<SosInfo> Sos { get; set; } = new();
        public List<TicketInfo> Tickets { get; set; } = new();
    }

    public class ServicioInfo : ServicioFila
    {
        public string? Notas { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaInicioReal { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? JustificacionFinalizacion { get; set; }
        public string? MotivoCancelacion { get; set; }
        public string? ClienteEmail { get; set; }
        public string? ClienteTelefono { get; set; }
        public string? ClienteFoto { get; set; }
        public string? CuidadorEmail { get; set; }
        public string? CuidadorTelefono { get; set; }
        public string? CuidadorFoto { get; set; }
    }

    public class TareaInfo
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = "";
        public bool Completada { get; set; }
        public DateTime? FechaCompletada { get; set; }
    }

    public class ActividadInfo
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = "";
        public DateTime FechaHora { get; set; }
    }

    public class PagoInfo
    {
        public int Id { get; set; }
        public decimal Monto { get; set; }
        public decimal Propina { get; set; }
        public int Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaAutorizacion { get; set; }
        public DateTime? FechaPago { get; set; }

        public string EstadoTexto => Estado switch { 1 => "Por autorizar", 3 => "Autorizado", 2 => "Pagado", _ => "—" };
        public string Tono => Estado switch { 1 => "tone-warn", 3 => "tone-info", 2 => "tone-ok", _ => "tone-muted" };
    }

    public class CalificacionInfo
    {
        public int CalificadorId { get; set; }
        public string CalificadorNombre { get; set; } = "";
        public int Puntuacion { get; set; }
        public string? Comentario { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class MensajeInfo
    {
        public long Id { get; set; }
        public int RemitenteId { get; set; }
        public string RemitenteNombre { get; set; } = "";
        public string Contenido { get; set; } = "";
        public string Tipo { get; set; } = "texto";
        public string? UrlArchivo { get; set; }
        public int? DuracionSegundos { get; set; }
        public DateTime FechaEnvio { get; set; }
        public bool Eliminado { get; set; }
    }

    public class SosInfo
    {
        public int Id { get; set; }
        public string TipoUsuario { get; set; } = "";
        public string? Motivo { get; set; }
        public string Estado { get; set; } = "";
        public string Origen { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
        public string? AtendidoPor { get; set; }
    }

    public class TicketInfo
    {
        public int Id { get; set; }
        public string Asunto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public int Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    // ---------- Verificación
    public class VerificacionFila
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string Email { get; set; } = "";
        public string? FotoUrl { get; set; }
        public string? Telefono { get; set; }
        public DateTime? FechaCreacion { get; set; }
        public int EstadoAprobacion { get; set; }
        public string? Especialidad { get; set; }
        public int DocsTotal { get; set; }
        public int DocsPendientes { get; set; }
        public int DocsAprobados { get; set; }
        public int DocsRechazados { get; set; }
        public DateTime? UltimaSubida { get; set; }

        /// <summary>Todos sus documentos están aprobados pero la cuenta sigue sin aprobar: solo falta el clic final.</summary>
        public bool ListoParaAprobar => EstadoAprobacion == 1 && DocsTotal > 0 && DocsAprobados == DocsTotal;
    }

    // ---------- Mapa
    public class MapaPanel
    {
        public List<MapaCuidador> Cuidadores { get; set; } = new();
        public List<MapaServicio> Servicios { get; set; } = new();
        public List<MapaSos> Sos { get; set; } = new();
    }

    public class MapaCuidador
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? FotoUrl { get; set; }
        public string? Especialidad { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public bool EnServicio { get; set; }
        public DateTime? UltimaUbicacion { get; set; }
        public int? Bateria { get; set; }
    }

    public class MapaServicio
    {
        public int Id { get; set; }
        public string TipoServicio { get; set; } = "";
        public int Estado { get; set; }
        public string? Direccion { get; set; }
        public string HoraInicio { get; set; } = "";
        public string HoraFin { get; set; } = "";
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string CuidadorNombre { get; set; } = "";
        public int CuidadorId { get; set; }
        public double? CuidadorLatitud { get; set; }
        public double? CuidadorLongitud { get; set; }
    }

    // ---------- Centro de mando
    public class FichaMapa
    {
        public PersonaMapa Persona { get; set; } = new();
        public ServicioMapa? Servicio { get; set; }
        public List<AvisoDirectoFila> Avisos { get; set; } = new();
    }

    public class PersonaMapa
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? FotoUrl { get; set; }
        public int RolId { get; set; }
        public string? Especialidad { get; set; }
        public bool? Disponible { get; set; }
        public bool? HorarioAutomatico { get; set; }
        public int? EstadoAprobacion { get; set; }
        public DateTime? UltimaUbicacion { get; set; }
        public int? Bateria { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
        public decimal? Calificacion { get; set; }
        public int Completados { get; set; }
        public int ServiciosHoy { get; set; }
    }

    public class ServicioMapa
    {
        public int Id { get; set; }
        public string TipoServicio { get; set; } = "";
        public int Estado { get; set; }
        public DateTime Fecha { get; set; }
        public string HoraInicio { get; set; } = "";
        public string HoraFin { get; set; } = "";
        public string? Direccion { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string? ClienteTelefono { get; set; }
        public DateTime? FechaInicioReal { get; set; }
        public int Tareas { get; set; }
        public int TareasHechas { get; set; }
    }

    public class AvisoDirectoFila
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = "";
        public string Titulo { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string? Respuesta { get; set; }
        public DateTime? FechaRespuesta { get; set; }
        public string? AdminNombre { get; set; }
    }

    public class PuntoRecorrido
    {
        public int CuidadorId { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public DateTime Fecha { get; set; }
        public int? Bateria { get; set; }
    }

    public class PuntoDemanda
    {
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public int Peso { get; set; }
    }

    public class AlertaMapa
    {
        public string Tipo { get; set; } = "";   // sin_iniciar, sin_senal
        public int TrabajoId { get; set; }
        public int CuidadorId { get; set; }
        public string Nombre { get; set; } = "";
        public string TipoServicio { get; set; } = "";
        public DateTime? Desde { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
    }

    public class MapaSos
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public string Nombre { get; set; } = "";
        public string TipoUsuario { get; set; } = "";
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Origen { get; set; } = "";
    }

    // ---------- Calificaciones
    public class CalificacionesPanel
    {
        public List<CalificacionFila> Calificaciones { get; set; } = new();
        public List<ResumenCuidador> Resumen { get; set; } = new();
    }

    public class CalificacionFila
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public int Puntuacion { get; set; }
        public string? Comentario { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int CalificadorId { get; set; }
        public string CalificadorNombre { get; set; } = "";
        public int CalificadorRol { get; set; }
        public int CalificadoId { get; set; }
        public string CalificadoNombre { get; set; } = "";
        public int CalificadoRol { get; set; }
    }

    public class ResumenCuidador
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? FotoUrl { get; set; }
        public decimal Promedio { get; set; }
        public int Total { get; set; }
        public int Bajas { get; set; }
    }

    // ---------- Finanzas
    public class FinanzasPanel
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public TotalesFinanzas Totales { get; set; } = new();
        public List<FinanzasCuidador> PorCuidador { get; set; } = new();
        public List<FinanzasDia> Serie { get; set; } = new();
    }

    public class TotalesFinanzas
    {
        public int ServiciosCompletados { get; set; }
        public int ServiciosCancelados { get; set; }
        public decimal Facturado { get; set; }
        public decimal Propinas { get; set; }
        public decimal Pagado { get; set; }
        public decimal PorPagar { get; set; }
        public int Disputados { get; set; }
    }

    public class FinanzasCuidador
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? MetodoCobro { get; set; }
        public int Servicios { get; set; }
        public decimal Facturado { get; set; }
        public decimal Propinas { get; set; }
        public decimal Pagado { get; set; }
        public decimal PorPagar { get; set; }
    }

    public class FinanzasDia
    {
        public DateTime Dia { get; set; }
        public int Servicios { get; set; }
        public decimal Facturado { get; set; }
    }

    // ---------- Auditoría y avisos
    public class AuditoriaFila
    {
        public int Id { get; set; }
        public int AdminId { get; set; }
        public string? AdminNombre { get; set; }
        public string Accion { get; set; } = "";
        public string? Entidad { get; set; }
        public string? EntidadId { get; set; }
        public string? Detalle { get; set; }
        public bool Exito { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class AvisoFila
    {
        public int Id { get; set; }
        public int Destino { get; set; }
        public string Titulo { get; set; } = "";
        public string Mensaje { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string? AdminNombre { get; set; }

        public string? Idioma { get; set; }

        public string DestinoTexto => Destino switch { 2 => "Clientes", 3 => "Care Partners", _ => "Todos" };
        public string? IdiomaTexto => Idiomas.Nombre(Idioma);
    }

    /// <summary>Idiomas de la app móvil (Localizador.Idiomas).</summary>
    public static class Idiomas
    {
        public static readonly (string Codigo, string Nombre)[] Todos = { ("es", "Español"), ("en", "Inglés"), ("ht", "Creol") };
        public static string? Nombre(string? codigo) => Todos.FirstOrDefault(i => i.Codigo == codigo).Nombre;
    }

    // ---------- Catálogos
    public class TipoServicioCat
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public string? Icono { get; set; }
        public bool Activo { get; set; } = true;
    }

    public class MotivoCat
    {
        public int? Id { get; set; }
        public string Descripcion { get; set; } = "";
        public bool Activo { get; set; } = true;
        public int OrdenVisual { get; set; }
    }

    public class NacionalidadCat
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Pais { get; set; } = "";
        public string CodigoIso { get; set; } = "";
        public bool Activo { get; set; } = true;
        public int Usuarios { get; set; }
    }

    public class EstatusPanel
    {
        public List<EstatusCat> Estatus { get; set; } = new();
        public List<RequisitoCat> Requisitos { get; set; } = new();
    }

    public class EstatusCat
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = "";
        public bool Activo { get; set; } = true;
    }

    public class RequisitoCat
    {
        public int Id { get; set; }
        public int EstatusMigratorioId { get; set; }
        public string TipoDocumento { get; set; } = "";
    }
}
