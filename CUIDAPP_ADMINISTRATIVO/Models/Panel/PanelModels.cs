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
        public DateTime? FechaLlegada { get; set; }
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
        public string? MetodoPago { get; set; }
        public int? DisputaId { get; set; }
        public bool GrabacionActiva { get; set; }
    }

    public class GrabacionSegmento
    {
        public int Id { get; set; }
        public string UrlArchivo { get; set; } = "";
        public int Orden { get; set; }
        public int? DuracionSeg { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class GrabacionServicio
    {
        public List<GrabacionSegmento> Segmentos { get; set; } = new();
        public GrabacionInfo? Info { get; set; }
    }

    public class GrabacionInfo
    {
        public bool GrabacionActiva { get; set; }
        public int? DuracionTotal { get; set; }
        public int Estado { get; set; }
        public bool HayBorrados { get; set; }
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
        public decimal ComisionPorcentaje { get; set; }
        public decimal DescuentoSaldo { get; set; }
        public decimal Comision => Math.Round(Monto * ComisionPorcentaje / 100, 2);
        public decimal Neto => Monto - Comision + Propina - DescuentoSaldo;

        public string EstadoTexto => Estado switch { 1 => "Por autorizar", 3 => "Autorizado", 2 => "Pagado", 4 => "Cobrado en efectivo", _ => "—" };
        public string Tono => Estado switch { 1 => "tone-warn", 3 => "tone-info", 2 => "tone-ok", 4 => "tone-violet", _ => "tone-muted" };
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
        public List<FinanzasMetodo> PorMetodo { get; set; } = new();
    }

    public class FinanzasMetodo
    {
        public string Metodo { get; set; } = "";
        public int Servicios { get; set; }
        public decimal Facturado { get; set; }
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
        public decimal Comision { get; set; }
        public int SinMetodo { get; set; }
        public decimal ComisionGeneral { get; set; }
        public decimal CobradoEfectivo { get; set; }
        public decimal ComisionPorCobrar { get; set; }
        public decimal TopeDeudaEfectivo { get; set; }
    }

    public class FinanzasCuidador
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? MetodoCobro { get; set; }
        public int Servicios { get; set; }
        public decimal Facturado { get; set; }
        public decimal Propinas { get; set; }
        public decimal Comision { get; set; }
        public decimal Pagado { get; set; }
        public decimal PorPagar { get; set; }
        public decimal Efectivo { get; set; }
        public decimal Saldo { get; set; }
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

    // ---------- Método de pago del cliente
    public static class MetodosPago
    {
        public static readonly (string Id, string Texto)[] Todos = { ("efectivo", "Efectivo"), ("tarjeta", "Tarjeta"), ("transferencia", "Transferencia") };
        public static string Texto(string? id) => Todos.FirstOrDefault(m => m.Id == id).Texto ?? "Sin registrar";
    }

    // ---------- Calidad de Care Partners
    public class CalidadCuidador
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? FotoUrl { get; set; }
        public string? Telefono { get; set; }
        public string? Especialidad { get; set; }
        public bool? Disponible { get; set; }
        public int Solicitudes { get; set; }
        public int Completados { get; set; }
        public int Cancelados { get; set; }
        public int Rechazados { get; set; }
        public int NoSePresento { get; set; }
        public int Tarde { get; set; }
        public int Disputas { get; set; }
        public int Sos { get; set; }
        public int Resenas { get; set; }
        public decimal? Nota { get; set; }
        public int Malas { get; set; }
        public decimal? NotaGeneral { get; set; }
        public int DocsVencidos { get; set; }
        public int Puntaje { get; set; }
        public string Semaforo { get; set; } = "verde";
        public decimal? TasaAceptacion { get; set; }

        public string Tono => Semaforo switch { "rojo" => "tone-danger", "amarillo" => "tone-warn", _ => "tone-ok" };
        public string SemaforoTexto => Semaforo switch { "rojo" => "Revisar ya", "amarillo" => "Atención", _ => "Bien" };
        public string Color => Semaforo switch { "rojo" => "var(--danger)", "amarillo" => "var(--warn)", _ => "var(--ok)" };
    }

    public class IncidenciaCalidad
    {
        public int? TrabajoId { get; set; }
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = "";
        public string? Detalle { get; set; }
        public string? ClienteNombre { get; set; }

        public string TipoTexto => Tipo switch
        {
            "cancelo" => "Canceló el servicio",
            "rechazo" => "Rechazó la solicitud",
            "no_se_presento" => "No se presentó",
            "disputa" => "El cliente rechazó el final",
            "tarde" => "Llegó tarde",
            "resena_baja" => "Reseña baja",
            _ => Tipo
        };
        public string Tono => Tipo switch { "no_se_presento" or "cancelo" or "resena_baja" => "tone-danger", "disputa" or "tarde" => "tone-warn", _ => "tone-muted" };
    }

    // ---------- Documentos que vencen
    public class DocumentoVencimiento
    {
        public int Id { get; set; }
        public int CuidadorId { get; set; }
        public string Nombre { get; set; } = "";
        public string? FotoUrl { get; set; }
        public string? Telefono { get; set; }
        public string TipoDocumento { get; set; } = "";
        public string? UrlArchivo { get; set; }
        public int Estado { get; set; }
        public string? ObservacionesAdmin { get; set; }
        public DateTime? FechaSubida { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public int? DiasRestantes { get; set; }
        public int AvisoVencimiento { get; set; }
        public bool? Disponible { get; set; }

        /// <summary>revisar · vencidos · pronto (30 días) · sinfecha · vigentes</summary>
        public string Grupo => Estado == 1 ? "revisar"
            : DiasRestantes < 0 ? "vencidos"
            : DiasRestantes <= 30 ? "pronto"
            : FechaVencimiento == null ? "sinfecha" : "vigentes";
    }

    // ---------- Comisión
    public class ComisionesPanel
    {
        public ComisionGeneral? General { get; set; }
        public List<TipoComision> Tipos { get; set; } = new();
    }

    public class ComisionGeneral
    {
        public decimal? Porcentaje { get; set; }
        public DateTime? FechaActualizacion { get; set; }
        public string? AdminNombre { get; set; }
    }

    public class TipoComision
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public bool Activo { get; set; }
        public decimal? ComisionPorcentaje { get; set; }
    }

    // ---------- Disputas
    public class DisputaFila
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public string Origen { get; set; } = "";
        public int Estado { get; set; }
        public string? Motivo { get; set; }
        public string? Resolucion { get; set; }
        public decimal? MontoFinal { get; set; }
        public string? Nota { get; set; }
        public DateTime FechaApertura { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public string? ResueltaPorNombre { get; set; }
        public string? AbiertaPorNombre { get; set; }
        public string TipoServicio { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string HoraInicio { get; set; } = "";
        public string HoraFin { get; set; } = "";
        public int EstadoServicio { get; set; }
        public decimal Tarifa { get; set; }
        public string? JustificacionFinalizacion { get; set; }
        public string? MetodoPago { get; set; }
        public DateTime? FechaInicioReal { get; set; }
        public DateTime? FechaFin { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string? ClienteFoto { get; set; }
        public string? ClienteTelefono { get; set; }
        public int CuidadorId { get; set; }
        public string CuidadorNombre { get; set; } = "";
        public string? CuidadorFoto { get; set; }
        public string? CuidadorTelefono { get; set; }
        public decimal? PagoMonto { get; set; }
        public decimal? PagoPropina { get; set; }
        public int? PagoEstado { get; set; }
        public int DiasAbierta { get; set; }
        public int PuntosGps { get; set; }
        public int Tareas { get; set; }
        public int TareasHechas { get; set; }

        public bool Abierta => Estado == 1;
        public string OrigenTexto => Origen switch
        {
            "rechazo" => "El cliente rechazó el final",
            "sin_pago" => "Cerrado sin pago",
            _ => "Abierta por soporte"
        };
        public string ResolucionTexto => Resolucion switch
        {
            "completo" => "Pago completo",
            "parcial" => "Pago parcial",
            "sin_pago" => "Sin pago",
            "acuerdo" => "Resuelta entre las partes",
            "cancelado" => "Servicio cancelado",
            _ => "—"
        };
    }

    // ---------- Saldos de comisiones de efectivo
    public class SaldoCuidadorFila
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = "";
        public string? FotoUrl { get; set; }
        public string? Telefono { get; set; }
        public decimal Saldo { get; set; }
        public int ServiciosEfectivo { get; set; }
        public DateTime? UltimoMovimiento { get; set; }
        public bool PuedeAceptarEfectivo { get; set; } = true;
    }

    public class MovimientoSaldo
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = "";
        public decimal Monto { get; set; }
        public string? Nota { get; set; }
        public DateTime Fecha { get; set; }
        public int? TrabajoId { get; set; }
        public string? TipoServicio { get; set; }
        public string? AdminNombre { get; set; }

        public string TipoTexto => Tipo switch
        {
            "comision_efectivo" => "Comisión de servicio en efectivo",
            "descuento_pago" => "Descontado de un pago",
            "abono" => "Abono",
            _ => Tipo
        };
    }

    public class ConfigPagos
    {
        public decimal TopeDeudaEfectivo { get; set; }
        public string? InstruccionesTransferencia { get; set; }
    }

    // ---------- Test psicológico del registro

    /// <summary>Resultado de un intento (TestIntentos.Resultado) con su texto y color de pastilla.</summary>
    public static class ResultadoTest
    {
        public const int Aprobado = 1, Revision = 2, Bloqueado = 3, Descartado = 4;

        public static string Texto(int r) => r switch
        {
            Aprobado => "Aprobado", Revision => "Revisión manual", Bloqueado => "Bloqueado", Descartado => "Descartado", _ => "—"
        };

        public static string Tono(int r) => r switch
        {
            Aprobado => "tone-ok", Revision => "tone-warn", Bloqueado or Descartado => "tone-danger", _ => "tone-muted"
        };
    }

    public class TestsPanel
    {
        public List<TestFila> Tests { get; set; } = new();
        public List<TestTipoVinculo> Tipos { get; set; } = new();
    }

    public class TestFila
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public int MinutosLimite { get; set; }
        public decimal PorcentajeAprobacion { get; set; }
        public decimal PorcentajeRevision { get; set; }
        public int DiasBloqueo { get; set; }
        public bool Activo { get; set; }
        public int Preguntas { get; set; }
        public int Criticas { get; set; }
        public int Intentos { get; set; }
    }

    public class TestTipoVinculo
    {
        public int TestId { get; set; }
        public int TipoTrabajoId { get; set; }
        public string Nombre { get; set; } = "";
    }

    /// <summary>Lo que se edita y se envía al guardar (POST admin/tests).</summary>
    public class TestEdicion
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public int MinutosLimite { get; set; } = 12;
        public decimal PorcentajeAprobacion { get; set; } = 83;
        public decimal PorcentajeRevision { get; set; } = 66;
        public int DiasBloqueo { get; set; } = 90;
        public bool Activo { get; set; } = true;
        public List<int> Tipos { get; set; } = new();
        public List<PreguntaEdicion> Preguntas { get; set; } = new();
    }

    public class PreguntaEdicion
    {
        public string Texto { get; set; } = "";
        public bool EsCritica { get; set; }
        public List<OpcionEdicion> Opciones { get; set; } = new();
    }

    public class OpcionEdicion
    {
        public string Texto { get; set; } = "";
        public bool EsCorrecta { get; set; }
        public bool EsRedFlag { get; set; }
    }

    public class TestDetalleRespuesta
    {
        public TestEdicion Test { get; set; } = new();
        public List<int> Tipos { get; set; } = new();
        public List<PreguntaEdicion> Preguntas { get; set; } = new();
    }

    public class TestIntentoFila
    {
        public int Id { get; set; }
        public int TestId { get; set; }
        public string Test { get; set; } = "";
        public string? TipoTrabajo { get; set; }
        public int? UsuarioId { get; set; }
        public string? Nombre { get; set; }
        public string? Email { get; set; }
        public string? Documento { get; set; }
        public string? Telefono { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int? SegundosUsados { get; set; }
        public bool TiempoAgotado { get; set; }
        public int? TotalPreguntas { get; set; }
        public int? Correctas { get; set; }
        public decimal? Porcentaje { get; set; }
        public bool FalloCritica { get; set; }
        public bool EligioRedFlag { get; set; }
        public int Resultado { get; set; }
        public DateTime? BloqueadoHasta { get; set; }
        public bool Anulado { get; set; }
        public DateTime? FechaAnulado { get; set; }
        public string? AnuladoPorNombre { get; set; }
    }

    public class TestRespuestaFila
    {
        public int Orden { get; set; }
        public string PreguntaTexto { get; set; } = "";
        public bool EsCritica { get; set; }
        public string? OpcionTexto { get; set; }
        public string? CorrectaTexto { get; set; }
        public bool EsCorrecta { get; set; }
        public bool EsRedFlag { get; set; }
    }

    public class TestIntentoDetalle
    {
        public TestIntentoFila Intento { get; set; } = new();
        public List<TestRespuestaFila> Respuestas { get; set; } = new();
    }

    public class TestResultadoUsuario
    {
        public int UsuarioId { get; set; }
        public int IntentoId { get; set; }
        public int Resultado { get; set; }
        public decimal? Porcentaje { get; set; }
    }
}
