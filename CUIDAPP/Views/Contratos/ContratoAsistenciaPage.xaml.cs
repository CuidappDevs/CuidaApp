using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;
namespace CUIDAPP.Views.Contratos;
public partial class ContratoAsistenciaPage : ContentPage, IQueryAttributable
{
 private readonly ApiService _api = new();
 private int usuarioId, contratoId;
 private bool cargando;
 public ContratoAsistenciaPage() { InitializeComponent(); ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0); LblTitulo.Text=Localizador.T("contrato_resumen"); LblSubtitulo.Text=""; }
 public void ApplyQueryAttributes(IDictionary<string, object> query) { if(query.TryGetValue("ContratoId",out var v)) int.TryParse(v?.ToString(),out contratoId); }
 protected override async void OnAppearing() { base.OnAppearing(); usuarioId=Preferences.Default.Get("UserId",0); await Task.Yield(); if(cargando)return; cargando=true; try { Contenido.Clear(); await AgregarResumenAsync(); } catch(Exception ex) { System.Diagnostics.Debug.WriteLine(ex); await Alerta.MostrarAsync(Localizador.T("error"),Localizador.T("contrato_resumen_error"),Localizador.T("ok")); } finally { Contenido.Add(new ContentView{HeightRequest=24+BarraEstado.AltoInferior()}); Cargando.IsVisible=Cargando.IsRunning=false; Desplazamiento.IsVisible=true; cargando=false; } }
 private async void OnBackTapped(object sender,TappedEventArgs e)=>await Shell.Current.GoToAsync("..");
         private DateTime mesResumen = new(ServerClock.Today.Year, ServerClock.Today.Month, 1);
        private async Task AgregarResumenAsync()
        {
            var cuerpo = new VerticalStackLayout { Spacing = 10 };
            var mesTexto = new Label { FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            async Task Actualizar()
            {
                var nombre = mesResumen.ToString("MMMM yyyy", Localizador.Cultura);
                mesTexto.Text = char.ToUpper(nombre[0], Localizador.Cultura) + nombre[1..];
                cuerpo.Clear();
                var r = await _api.ResumenMensualContratoAsync(contratoId, usuarioId, mesResumen);
                if (r == null) { cuerpo.Add(Texto(Localizador.T("contrato_resumen_error"), true)); return; }

                // Lo principal grande (cumplido vs. horas), lo que requiere atención en tonos de aviso,
                // y el desglose de cancelaciones como lista, solo si hubo alguna.
                cuerpo.Add(DosColumnas(
                    Indicador(r.Completadas.ToString(), "contrato_resumen_completadas", "ColorSuccessSoft", "ColorSuccess", 28),
                    Indicador(r.HorasTrabajadas.ToString("0.#", Localizador.Cultura), "contrato_resumen_horas", "ColorPrimarySoft", "ColorPrimary", 28)));
                cuerpo.Add(DosColumnas(
                    Indicador(r.Ausencias.ToString(), "contrato_resumen_ausencias", "ColorWarningSoft", "ColorWarning", 22),
                    Indicador(r.Canceladas.ToString(), "contrato_resumen_canceladas", "ColorDangerSoft", "ColorDanger", 22)));

                var detalle = new (string Clave, int Valor)[] {
                    ("no_iniciadas", r.NoIniciadas), ("canceladas_cliente", r.CanceladasCliente),
                    ("canceladas_cuidador", r.CanceladasCuidador), ("sin_atribuir", r.CanceladasSinAtribuir) }.Where(x => x.Valor > 0).ToList();
                if (detalle.Count > 0)
                {
                    var lista = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(4, 4, 4, 0) };
                    foreach (var (clave, valor) in detalle)
                    {
                        var fila = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(0, 8) };
                        fila.Add(Texto(Localizador.T("contrato_resumen_" + clave), true, 13.5), 0);
                        var num = Titulo(valor.ToString(), 15); Grid.SetColumn(num, 1); fila.Add(num);
                        lista.Add(fila);
                    }
                    cuerpo.Add(lista);
                }
            }
            View Flecha(string simbolo, string clave, int meses)
            {
                var b = new Border
                {
                    WidthRequest = 44, HeightRequest = 44, Stroke = Tema.C("ColorBorder"), StrokeThickness = 1,
                    BackgroundColor = Tema.C("ColorSurface"), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
                    Content = new Label { Text = simbolo, FontSize = 22, TextColor = Tema.C("ColorPrimary"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, -3, 0, 0) }
                };
                SemanticProperties.SetDescription(b, Localizador.T(clave));
                b.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => { mesResumen = mesResumen.AddMonths(meses); await Actualizar(); }) });
                return b;
            }
            var selector = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
            selector.Add(Flecha("‹", "contrato_mes_anterior", -1), 0);
            selector.Add(mesTexto, 1);
            selector.Add(Flecha("›", "contrato_mes_siguiente", 1), 2);
            Contenido.Add(Tarjeta(Titulo(Localizador.T("contrato_resumen")), selector, cuerpo));
            await Actualizar();
        }

        private static View DosColumnas(View a, View b)
        {
            var g = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 };
            g.Add(a, 0); g.Add(b, 1);
            return g;
        }

        /// <summary>Recuadro de cifra: número grande en el color del estado y etiqueta completa debajo (sin cortes).</summary>
        private static View Indicador(string valor, string clave, string fondo, string color, double tam) => new Border
        {
            Stroke = Colors.Transparent,
            BackgroundColor = Tema.C(fondo),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14, 12),
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = valor, FontSize = tam, FontFamily = "OpenSansSemibold", TextColor = Tema.C(color) },
                    new Label { Text = Localizador.T(clave), FontSize = 12.5, FontFamily = "OpenSansRegular", TextColor = Tema.C("ColorTextStrong"), LineBreakMode = LineBreakMode.WordWrap }
                }
            }
        };


}