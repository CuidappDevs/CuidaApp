namespace CUIDAPP.Services
{
    /// <summary>
    /// Decide si mostrar la bienvenida animada: una vez por CUENTA (columna Usuarios.BienvenidaVista en el
    /// servidor), así que no vuelve a salir aunque la persona entre desde otro teléfono.
    /// Se llama desde los paneles de cliente y cuidador (el cuidador solo llega a su panel ya aprobado).
    /// </summary>
    public static class BienvenidaApp
    {
        private static bool _enCurso;

        // Caché local para no consultar al servidor cada vez que se abre el panel.
        private static string ClaveLocal(int usuarioId) => $"BienvenidaVista_{usuarioId}";

        /// <summary>Vuelve a mostrar la bienvenida a pedido (desde Ayuda), sin tocar lo guardado en el servidor.</summary>
        public static async Task RepetirAsync()
        {
            if (_enCurso || Shell.Current?.Navigation is not { } navegacion)
                return;
            _enCurso = true;
            try
            {
                var pagina = new Views.Comun.BienvenidaPage(
                    esCuidador: Preferences.Default.Get("RolId", 0) == 3,
                    nombre: Preferences.Default.Get("UserNombre", ""));
                await navegacion.PushModalAsync(pagina, false);
                await pagina.Terminada;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bienvenida] {ex.Message}");
            }
            finally
            {
                _enCurso = false;
            }
        }

        public static async Task MostrarSiCorrespondeAsync()
        {
            var usuarioId = Preferences.Default.Get("UserId", 0);
            if (usuarioId == 0 || _enCurso || Preferences.Default.Get(ClaveLocal(usuarioId), false))
                return;

            _enCurso = true;
            try
            {
                var api = new ApiService();
                var vista = await api.ObtenerBienvenidaVistaAsync(usuarioId);
                if (vista == null)
                    return; // sin conexión o API vieja: se intenta la próxima vez
                if (vista.Value)
                {
                    Preferences.Default.Set(ClaveLocal(usuarioId), true);
                    return;
                }

                var pagina = new Views.Comun.BienvenidaPage(
                    esCuidador: Preferences.Default.Get("RolId", 0) == 3,
                    nombre: Preferences.Default.Get("UserNombre", ""));
                var navegacion = Shell.Current?.Navigation;
                if (navegacion == null)
                    return;

                await navegacion.PushModalAsync(pagina, false);
                await pagina.Terminada;

                // Vista (o saltada): se registra en el servidor para todas las sesiones de esta cuenta.
                if (await api.MarcarBienvenidaVistaAsync(usuarioId))
                    Preferences.Default.Set(ClaveLocal(usuarioId), true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bienvenida] {ex.Message}");
            }
            finally
            {
                _enCurso = false;
            }
        }
    }
}
