namespace CUIDAPP
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("RegistroPage", typeof(Views.Registro.RegistroPage));
            Routing.RegisterRoute("CuidadorDashboardPage", typeof(Views.Dashboard.CuidadorDashboardPage));
            Routing.RegisterRoute("CuidadorPerfilPage", typeof(Views.Perfil.CuidadorPerfilPage));
            Routing.RegisterRoute("TrabajosPage", typeof(Views.Trabajos.TrabajosPage));
            Routing.RegisterRoute("DetalleTrabajoPage", typeof(Views.Trabajos.DetalleTrabajoPage));
        }
    }
}
