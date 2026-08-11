using CUIDAPP.Models.Cliente;

namespace CUIDAPP.Services
{
    // Puente simple para devolver un valor a la página anterior en navegación Shell,
    // usado cuando SolicitarServicioPage abre MisUbicacionesPage en "modo selección".
    public static class SeleccionUbicacionBroker
    {
        public static TaskCompletionSource<UbicacionCliente?>? Pendiente;

        // Usado cuando SeleccionarPuntoMapaPage corre en "solo elegir" (ej. registro, donde
        // aún no hay ClienteId con sesión ni se debe guardar nada en BD todavía).
        public static TaskCompletionSource<UbicacionCliente?>? PendientePunto;
    }
}
