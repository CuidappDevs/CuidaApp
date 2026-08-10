namespace CUIDAPP.Views.Trabajos
{
    public partial class MapaCompletoPage : ContentPage, IQueryAttributable
    {
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Html", out var value) && value is string html)
                MapaWebView.Source = new HtmlWebViewSource { Html = html };
        }

        public MapaCompletoPage()
        {
            InitializeComponent();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
