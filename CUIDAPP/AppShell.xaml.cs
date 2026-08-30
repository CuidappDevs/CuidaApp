namespace CUIDAPP
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("RegistroPage", typeof(Views.Registro.RegistroPage));
            Routing.RegisterRoute("VerificacionPendientePage", typeof(Views.Verificacion.VerificacionPendientePage));
            Routing.RegisterRoute("CuidadorDashboardPage", typeof(Views.Dashboard.CuidadorDashboardPage));
            Routing.RegisterRoute("CuidadorPerfilPage", typeof(Views.Perfil.CuidadorPerfilPage));
            Routing.RegisterRoute("TrabajosPage", typeof(Views.Trabajos.TrabajosPage));
            Routing.RegisterRoute("DetalleTrabajoPage", typeof(Views.Trabajos.DetalleTrabajoPage));
            Routing.RegisterRoute("IniciarTrabajoPage", typeof(Views.Trabajos.IniciarTrabajoPage));
            Routing.RegisterRoute("FinalizarTrabajoPage", typeof(Views.Trabajos.FinalizarTrabajoPage));
            Routing.RegisterRoute("CancelarServicioPage", typeof(Views.Trabajos.CancelarServicioPage));
            Routing.RegisterRoute("MapaCompletoPage", typeof(Views.Trabajos.MapaCompletoPage));
            Routing.RegisterRoute("CalificarPage", typeof(Views.Calificacion.CalificarPage));
            Routing.RegisterRoute("MisCalificacionesPage", typeof(Views.Calificacion.MisCalificacionesPage));
            Routing.RegisterRoute("DineroPage", typeof(Views.Dinero.DineroPage));
            Routing.RegisterRoute("ClienteDashboardPage", typeof(Views.Cliente.ClienteDashboardPage));
            Routing.RegisterRoute("ClientePerfilPage", typeof(Views.Cliente.ClientePerfilPage));
            Routing.RegisterRoute("CuidadoresPorServicioPage", typeof(Views.Cliente.CuidadoresPorServicioPage));
            Routing.RegisterRoute("CuidadorDetallePage", typeof(Views.Cliente.CuidadorDetallePage));
            Routing.RegisterRoute("SolicitarServicioPage", typeof(Views.Cliente.SolicitarServicioPage));
            Routing.RegisterRoute("MiServicioPage", typeof(Views.Cliente.MiServicioPage));
            Routing.RegisterRoute("DetalleServicioClientePage", typeof(Views.Cliente.DetalleServicioClientePage));
            Routing.RegisterRoute("ChatPage", typeof(Views.Chat.ChatPage));
            Routing.RegisterRoute("MisUbicacionesPage", typeof(Views.Cliente.MisUbicacionesPage));
            Routing.RegisterRoute("SeleccionarPuntoMapaPage", typeof(Views.Cliente.SeleccionarPuntoMapaPage));
            Routing.RegisterRoute("ForgotPasswordPage", typeof(Views.Auth.ForgotPasswordPage));
            Routing.RegisterRoute("ResetPasswordPage", typeof(Views.Auth.ResetPasswordPage));
            Routing.RegisterRoute("MisReportesPage", typeof(Views.Soporte.MisReportesPage));
            Routing.RegisterRoute("NuevoReportePage", typeof(Views.Soporte.NuevoReportePage));
            Routing.RegisterRoute("DetalleReportePage", typeof(Views.Soporte.DetalleReportePage));
        }
    }
}
