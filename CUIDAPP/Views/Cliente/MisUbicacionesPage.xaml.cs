using System.Collections.ObjectModel;
using CUIDAPP.Models.Cliente;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class MisUbicacionesPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly ObservableCollection<UbicacionCliente> ubicaciones = new();
        private bool modoSeleccion;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ModoSeleccion", out var value) && value is bool b)
                modoSeleccion = b;
        }

        public MisUbicacionesPage()
        {
            InitializeComponent();
            ListaUbicaciones.ItemsSource = ubicaciones;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            LblTitulo.Text = modoSeleccion ? "Elige a dónde vas" : "Mis ubicaciones";
            await CargarUbicaciones();
        }

        private async Task CargarUbicaciones()
        {
            LoaderCarga.IsRunning = true;
            LoaderCarga.IsVisible = true;

            var clienteId = Preferences.Default.Get("UserId", 0);
            var resultado = await _apiService.ObtenerUbicacionesClienteAsync(clienteId);

            ubicaciones.Clear();
            foreach (var u in resultado)
                ubicaciones.Add(u);

            EstadoVacio.IsVisible = ubicaciones.Count == 0;
            LoaderCarga.IsRunning = false;
            LoaderCarga.IsVisible = false;
        }

        private async void OnUbicacionTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not UbicacionCliente ubicacion)
                return;

            if (modoSeleccion)
            {
                SeleccionUbicacionBroker.Pendiente?.TrySetResult(ubicacion);
                await Shell.Current.GoToAsync("..");
                return;
            }

            await Shell.Current.GoToAsync("SeleccionarPuntoMapaPage", new Dictionary<string, object> { { "Ubicacion", ubicacion } });
        }

        private async void OnEliminarTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not UbicacionCliente ubicacion)
                return;

            var confirmar = await DisplayAlert("Eliminar ubicación", $"¿Eliminar \"{ubicacion.Nombre}\"?", "Sí, eliminar", "Cancelar");
            if (!confirmar)
                return;

            var clienteId = Preferences.Default.Get("UserId", 0);
            var success = await _apiService.EliminarUbicacionClienteAsync(ubicacion.Id, clienteId);

            if (success)
                await CargarUbicaciones();
            else
                await DisplayAlert("Error", "No se pudo eliminar la ubicación. Intenta de nuevo.", "OK");
        }

        private async void OnAgregarClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("SeleccionarPuntoMapaPage");
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            if (modoSeleccion)
                SeleccionUbicacionBroker.Pendiente?.TrySetResult(null);

            await Shell.Current.GoToAsync("..");
        }
    }
}
