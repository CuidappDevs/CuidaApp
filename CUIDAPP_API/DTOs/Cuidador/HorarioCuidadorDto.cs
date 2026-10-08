namespace CUIDAPP_API.DTOs.Cuidador
{
    /// <summary>
    /// Horario automático de visibilidad. Con <see cref="Activo"/> en true el servidor pone al cuidador
    /// visible u oculto según sus franjas y el interruptor manual queda bloqueado.
    /// </summary>
    public class HorarioCuidadorDto
    {
        public bool Activo { get; set; }
        public List<FranjaHorarioDto> Franjas { get; set; } = new();
    }

    public class FranjaHorarioDto
    {
        /// <summary>0 domingo, 1 lunes … 6 sábado.</summary>
        public int DiaSemana { get; set; }
        /// <summary>"HH:mm"</summary>
        public string HoraInicio { get; set; } = "";
        /// <summary>"HH:mm"</summary>
        public string HoraFin { get; set; } = "";
    }
}
