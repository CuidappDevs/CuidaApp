namespace CUIDAPP_ADMINISTRATIVO.Models.Ticket
{
    public class TicketAdmin
    {
        public int Id { get; set; }
        public string Asunto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public int? TrabajoId { get; set; }
        public int Estado { get; set; } // 1=Abierto, 2=En proceso, 3=Resuelto
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = "";
        public string UsuarioEmail { get; set; } = "";
        public int UsuarioRolId { get; set; }
        public int? AsignadoAdminId { get; set; }
        public string? AsignadoAdminNombre { get; set; }
        public int TotalMensajes { get; set; }

        public string EstadoTexto => Estado switch
        {
            1 => "Abierto",
            2 => "En proceso",
            3 => "Resuelto",
            _ => "Desconocido"
        };

        public string RolTexto => UsuarioRolId switch
        {
            2 => "Cliente",
            3 => "Cuidador",
            _ => "Usuario"
        };
    }
}
