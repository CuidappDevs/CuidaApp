using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Views.Contratos
{
    /// <summary>
    /// Lista de contratos fijos del usuario (cliente o Care Partner). Arriba: el cliente ve su permiso
    /// para contratar (y entra a gestionarlo); el Care Partner, el interruptor "Acepto contratos fijos".
    /// </summary>
    public partial class MisContratosPage : ContentPage
    {
        private readonly ApiService _api = new();
        private int usuarioId;
        private bool esCuidador;

        public MisContratosPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            usuarioId = Preferences.Default.Get("UserId", 0);
            esCuidador = Preferences.Default.Get("RolId", 0) == 3;
            RealtimeService.ContratoActualizado += AlCambiarContrato;
            await CargarAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RealtimeService.ContratoActualizado -= AlCambiarContrato;
        }

        private void AlCambiarContrato(int contratoId, string? estado) => _ = CargarAsync();

        private async Task CargarAsync()
        {
            var contratos = _api.ObtenerContratosAsync(usuarioId);
            View cabecera = esCuidador ? await TarjetaCuidadorAsync() : await TarjetaClienteAsync();
            var lista = await contratos;

            Contenido.Clear();
            Contenido.Add(cabecera);

            var vivos = lista.Where(c => c.Estado is >= 1 and <= 5).ToList();
            var cerrados = lista.Where(c => c.Estado > 5).ToList();

            if (lista.Count == 0)
                Contenido.Add(Tarjeta(Titulo(Localizador.T("contratos_vacio_titulo")),
                    Texto(Localizador.T(esCuidador ? "contratos_vacio_cuidador" : "contratos_vacio_cliente"), true)));

            if (vivos.Count > 0)
            {
                Contenido.Add(Texto(Localizador.T("contratos_en_curso"), true, 13).ConMargen(4, 4));
                foreach (var c in vivos) Contenido.Add(TarjetaContrato(c));
            }
            if (cerrados.Count > 0)
            {
                Contenido.Add(Texto(Localizador.T("contratos_anteriores"), true, 13).ConMargen(4, 4));
                foreach (var c in cerrados) Contenido.Add(TarjetaContrato(c));
            }
            Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });

            Cargando.IsVisible = Cargando.IsRunning = false;
            Desplazamiento.IsVisible = true;
        }

        private async Task<View> TarjetaClienteAsync()
        {
            var datos = await _api.ObtenerPermisoContratoAsync(usuarioId);
            var estado = datos?.Permiso?.Estado ?? 0;
            var validados = datos?.Domicilios.Count(d => d.Estado == 2) ?? 0;

            var texto = estado switch
            {
                2 => validados == 0 ? Localizador.T("permiso_aprobado_sin_domicilio") : Localizador.F("permiso_aprobado_con_domicilios", validados),
                1 => Localizador.T("permiso_en_revision_texto"),
                3 or 4 => string.IsNullOrWhiteSpace(datos?.Permiso?.ComentarioAdmin) ? Localizador.T("permiso_rechazado_texto") : datos!.Permiso!.ComentarioAdmin!,
                _ => Localizador.T("permiso_sin_solicitar_texto")
            };
            return Tarjeta(
                new HorizontalStackLayout { Spacing = 10, Children = { Titulo(Localizador.T("permiso_para_contratar")), Pastilla(Revision(estado), ColoresRevision(estado)) } },
                Texto(texto, true),
                Boton(Localizador.T(estado == 0 ? "permiso_solicitar" : "permiso_gestionar"), estado == 0 ? TipoBoton.Principal : TipoBoton.Secundario,
                    () => Shell.Current.GoToAsync("PermisoContratarPage")));
        }

        private async Task<View> TarjetaCuidadorAsync()
        {
            var config = await _api.ObtenerConfigContratosAsync(usuarioId);
            var interruptor = new Switch { IsToggled = config?.AceptaContratos ?? false, OnColor = Tema.C("ColorPrimary"), VerticalOptions = LayoutOptions.Center };
            interruptor.Toggled += async (_, e) =>
            {
                var (ok, error) = await _api.GuardarConfigContratosAsync(usuarioId, e.Value);
                if (!ok)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok"));
                    interruptor.IsToggled = !e.Value;
                }
            };
            var fila = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 12 };
            fila.Add(Titulo(Localizador.T("acepto_contratos_fijos")), 0);
            fila.Add(interruptor, 1);
            return Tarjeta(fila, Texto(Localizador.T("acepto_contratos_fijos_desc"), true));
        }

        private View TarjetaContrato(ContratoResumen c)
        {
            var foto = new Border
            {
                Stroke = Colors.Transparent, BackgroundColor = Tema.C("ColorSubtle"), WidthRequest = 46, HeightRequest = 46,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                Content = string.IsNullOrWhiteSpace(c.OtroFoto) ? null : new Image { Source = $"{ApiService.ServerOrigin}{c.OtroFoto}", Aspect = Aspect.AspectFill }
            };
            var encabezado = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 12 };
            encabezado.Add(foto, 0);
            var nombres = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            nombres.Add(Titulo(c.OtroNombre, 15));
            nombres.Add(Texto($"{Localizador.D(c.TipoServicio)} · {c.DomicilioNombre}", true, 12.5));
            encabezado.Add(nombres, 1);

            var tarjeta = Tarjeta(
                encabezado,
                Pastilla(Estado(c.Estado, c.MiRol), ColoresEstado(c.Estado)),
                Texto(HorarioCorto(c.Horario)),
                Texto(Localizador.F("contrato_tarifa_hora", Monto(c.TarifaHora)), true, 13));
            if (c.RequiereAccion)
            {
                tarjeta.Stroke = Tema.C("ColorPrimary");
                tarjeta.StrokeThickness = 1.5;
                ((VerticalStackLayout)tarjeta.Content!).Add(Texto(Localizador.T(c.Estado == 3 ? "contrato_falta_tu_firma" : "contrato_te_toca_responder"), false, 13).ConColor("ColorPrimary"));
            }
            tarjeta.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await Shell.Current.GoToAsync("ContratoDetallePage", new Dictionary<string, object> { { "ContratoId", c.Id } }))
            });
            return tarjeta;
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }

    internal static class ExtensionesVista
    {
        public static T ConMargen<T>(this T v, double horizontal, double vertical) where T : View { v.Margin = new Thickness(horizontal, vertical); return v; }
        public static Label ConColor(this Label l, string clave) { l.TextColor = Tema.C(clave); return l; }
        public static T Centrado<T>(this T v) where T : View { v.VerticalOptions = LayoutOptions.Center; return v; }
    }
}
