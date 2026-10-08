namespace CUIDAPP.Models.Cuidador
{
    /// <summary>
    /// Horario automático de visibilidad. Con <see cref="Activo"/> el servidor pone visible / oculto al
    /// cuidador según sus franjas y el interruptor manual queda bloqueado.
    /// </summary>
    public class HorarioCuidador
    {
        public bool Activo { get; set; }
        public List<FranjaHorario> Franjas { get; set; } = new();

        /// <summary>
        /// Franjas reales de los próximos días (las contiguas del mismo día se unen: 8–12 y 12–17 es 8–17).
        /// </summary>
        public List<(DateTime Inicio, DateTime Fin)> Proximas(DateTime desde, int dias = 8)
        {
            var lista = new List<(DateTime Inicio, DateTime Fin)>();
            for (int d = 0; d < dias; d++)
            {
                var fecha = desde.Date.AddDays(d);
                foreach (var f in Franjas.Where(f => f.DiaSemana == (int)fecha.DayOfWeek && f.Valida).OrderBy(f => f.Inicio))
                {
                    var (ini, fin) = (fecha + f.Inicio, fecha + f.Fin);
                    if (lista.Count > 0 && lista[^1].Fin >= ini)
                        lista[^1] = (lista[^1].Inicio, fin > lista[^1].Fin ? fin : lista[^1].Fin);
                    else
                        lista.Add((ini, fin));
                }
            }
            return lista;
        }

        /// <summary>Si ahora le toca estar visible y hasta cuándo, o cuándo vuelve a estarlo.</summary>
        public (bool VisibleAhora, DateTime? Cambio) Estado(DateTime ahora)
        {
            foreach (var (ini, fin) in Proximas(ahora))
            {
                if (ahora >= ini && ahora < fin) return (true, fin);
                if (ini > ahora) return (false, ini);
            }
            return (false, null);
        }
    }

    public class FranjaHorario
    {
        /// <summary>0 domingo, 1 lunes … 6 sábado (DayOfWeek).</summary>
        public int DiaSemana { get; set; }
        /// <summary>"HH:mm"</summary>
        public string HoraInicio { get; set; } = "08:00";
        /// <summary>"HH:mm"</summary>
        public string HoraFin { get; set; } = "17:00";

        public TimeSpan Inicio => TimeSpan.TryParse(HoraInicio, out var t) ? t : TimeSpan.Zero;
        public TimeSpan Fin => TimeSpan.TryParse(HoraFin, out var t) ? t : TimeSpan.Zero;
        public bool Valida => Fin > Inicio;
    }
}
