namespace CUIDAPP.Services
{
    public static class LocationService
    {
        // Ubicación por defecto (Santo Domingo) si el usuario niega el permiso o falla el GPS.
        public const double LatitudPorDefecto = 18.4861;
        public const double LongitudPorDefecto = -69.9312;

        public static async Task<Location?> ObtenerUbicacionActualAsync()
        {
            try
            {
                var estadoPermiso = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (estadoPermiso != PermissionStatus.Granted)
                {
                    estadoPermiso = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (estadoPermiso != PermissionStatus.Granted)
                    return null;

                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                return await Geolocation.Default.GetLocationAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo ubicación: {ex.Message}");
                return null;
            }
        }
    }
}
