using System.Text.Json;
using CUIDAPP.Models;

namespace CUIDAPP.Services
{
    // Historial local de notificaciones (por dispositivo, no sincronizado con el servidor).
    // Guardado como JSON en Preferences: simple y suficiente para una lista corta que solo
    // le importa a este usuario en este teléfono.
    public static class NotificacionHistorial
    {
        private const string Clave = "notificaciones_historial";
        private const int MaximoGuardadas = 50;

        public static List<Notificacion> Obtener()
        {
            var json = Preferences.Default.Get(Clave, "");
            if (string.IsNullOrWhiteSpace(json))
                return new List<Notificacion>();

            try
            {
                return JsonSerializer.Deserialize<List<Notificacion>>(json) ?? new List<Notificacion>();
            }
            catch
            {
                return new List<Notificacion>();
            }
        }

        public static void Agregar(string titulo, string mensaje, string tipo, int? trabajoId = null)
        {
            var lista = Obtener();
            lista.Insert(0, new Notificacion
            {
                Titulo = titulo,
                Mensaje = mensaje,
                Fecha = DateTime.Now,
                Tipo = tipo,
                TrabajoId = trabajoId,
                Leida = false
            });

            if (lista.Count > MaximoGuardadas)
                lista = lista.Take(MaximoGuardadas).ToList();

            Preferences.Default.Set(Clave, JsonSerializer.Serialize(lista));
        }

        public static void MarcarTodasLeidas()
        {
            var lista = Obtener();
            foreach (var n in lista)
                n.Leida = true;
            Preferences.Default.Set(Clave, JsonSerializer.Serialize(lista));
        }

        public static int ContarNoLeidas() => Obtener().Count(n => !n.Leida);

        public static void Limpiar() => Preferences.Default.Remove(Clave);
    }
}
