using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Helpers
{
    /// <summary>
    /// Piezas de interfaz compartidas por las pantallas de contratos fijos (tarjetas, filas, botones,
    /// pastillas de estado y textos de horario). Todos los colores salen de la paleta del tema.
    /// </summary>
    public static class ContratoVista
    {
        // ---------- textos
        public static string Estado(int estado, string? miRol = null) => estado switch
        {
            1 => Localizador.T(miRol == "cuidador" ? "contrato_estado_1_tu" : "contrato_estado_1"),
            2 => Localizador.T(miRol == "cliente" ? "contrato_estado_2_tu" : "contrato_estado_2"),
            >= 3 and <= 9 => Localizador.T($"contrato_estado_{estado}"),
            _ => ""
        };

        public static (string Fondo, string Texto) ColoresEstado(int estado) => estado switch
        {
            1 or 2 or 3 => ("ColorWarningSoft", "ColorWarning"),
            4 => ("ColorSuccessSoft", "ColorSuccess"),
            5 => ("ColorPrimarySoft", "ColorPrimary"),
            _ => ("ColorSubtle", "ColorTextMuted")
        };

        public static string Revision(int estado) => Localizador.T($"contrato_revision_{Math.Clamp(estado, 0, 4)}");

        public static (string Fondo, string Texto) ColoresRevision(int estado) => estado switch
        {
            1 => ("ColorWarningSoft", "ColorWarning"),
            2 => ("ColorSuccessSoft", "ColorSuccess"),
            3 or 4 => ("ColorDangerSoft", "ColorDanger"),
            _ => ("ColorSubtle", "ColorTextMuted")
        };

        public static string Hora(string hhmm) => TimeSpan.TryParse(hhmm, out var t) ? DateTime.Today.Add(t).ToString("h:mm tt", Localizador.Cultura) : hhmm;

        public static string Franja(FranjaContrato f) => $"{Localizador.T($"dia_{f.DiaSemana}")} · {Hora(f.HoraInicio)} – {Hora(f.HoraFin)}";

        /// <summary>Lunes primero.</summary>
        public static IEnumerable<FranjaContrato> Ordenar(IEnumerable<FranjaContrato> franjas)
            => franjas.OrderBy(f => f.DiaSemana == 0 ? 7 : f.DiaSemana).ThenBy(f => f.HoraInicio);

        /// <summary>"1|08:00|14:00;6|09:00|13:00" (formato del servidor) → franjas.</summary>
        public static List<FranjaContrato> Franjas(string? texto) => (texto ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('|')).Where(p => p.Length == 3 && int.TryParse(p[0], out _))
            .Select(p => new FranjaContrato { DiaSemana = int.Parse(p[0]), HoraInicio = p[1], HoraFin = p[2] }).ToList();

        public static string HorarioCorto(string? texto)
        {
            var franjas = Ordenar(Franjas(texto)).ToList();
            return franjas.Count == 0 ? "" : string.Join(", ", franjas.Select(f => Localizador.T($"dia_corto_{f.DiaSemana}") + " " + Hora(f.HoraInicio)));
        }

        public static double HorasSemana(IEnumerable<FranjaContrato> franjas)
            => franjas.Sum(f => TimeSpan.TryParse(f.HoraFin, out var fin) && TimeSpan.TryParse(f.HoraInicio, out var ini) ? (fin - ini).TotalHours : 0);

        public static string Monto(decimal monto) => Localizador.F("rd_monto", monto);

        // ---------- piezas visuales
        public static Border Tarjeta(params View[] hijos)
        {
            var pila = new VerticalStackLayout { Spacing = 12 };
            foreach (var h in hijos) { Desprender(h); pila.Add(h); }
            return new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Tema.C("ColorSurface"),
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(18),
                Shadow = new Shadow { Brush = new SolidColorBrush(Tema.C("ColorShadow")), Offset = new Point(0, 3), Radius = 12, Opacity = 0.08f },
                Content = pila
            };
        }

        public static Label Titulo(string texto, double tam = 16) => new()
        {
            Text = texto, FontSize = tam, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong")
        };

        public static Label Texto(string texto, bool suave = false, double tam = 13.5) => new()
        {
            Text = texto, FontSize = tam, FontFamily = "OpenSansRegular", LineHeight = 1.15,
            TextColor = Tema.C(suave ? "ColorTextMuted" : "ColorTextStrong")
        };

        public static View Fila(string etiqueta, string valor) => new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(118)), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10,
            Children =
            {
                Texto(etiqueta, true, 13),
                new Label { Text = valor, FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong") }.Columna(1)
            }
        };

        public static Border Pastilla(string texto, (string Fondo, string Texto) colores) => new()
        {
            Stroke = Colors.Transparent,
            BackgroundColor = Tema.C(colores.Fondo),
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(10, 4),
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label { Text = texto, FontSize = 12, FontFamily = "OpenSansSemibold", TextColor = Tema.C(colores.Texto) }
        };

        public enum TipoBoton { Principal, Secundario, Peligro }

        public static Border Boton(string texto, TipoBoton tipo, Func<Task> accion)
        {
            var (fondo, letra, borde) = tipo switch
            {
                TipoBoton.Principal => (Tema.C("ColorPrimary"), Colors.White, Colors.Transparent),
                TipoBoton.Peligro => (Tema.C("ColorDangerSoft"), Tema.C("ColorDanger"), Colors.Transparent),
                _ => (Tema.C("ColorSurface"), Tema.C("ColorPrimary"), Tema.C("ColorBorder"))
            };
            var etiqueta = new Label { Text = texto, FontSize = 15, FontFamily = "OpenSansSemibold", TextColor = letra, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            var indicador = new ActivityIndicator { IsVisible = false, IsRunning = false, Color = letra, WidthRequest = 22, HeightRequest = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            var boton = new Border
            {
                Stroke = borde,
                StrokeThickness = tipo == TipoBoton.Secundario ? 1.5 : 0,
                BackgroundColor = fondo,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                HeightRequest = 50,
                Content = new Grid { Children = { etiqueta, indicador } }
            };
            SemanticProperties.SetDescription(boton, texto);
            var ocupado = false;
            boton.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    if (ocupado) return;
                    ocupado = true;
                    _ = boton.ScaleTo(0.97, 80, Easing.CubicOut).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => boton.ScaleTo(1, 140, Easing.CubicOut)));
                    etiqueta.IsVisible = false;
                    indicador.IsVisible = indicador.IsRunning = true;
                    try { await accion(); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                        await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("contrato_err_generico"), Localizador.T("ok"));
                    }
                    finally
                    {
                        etiqueta.IsVisible = true;
                        indicador.IsVisible = indicador.IsRunning = false;
                        ocupado = false;
                    }
                })
            });
            return boton;
        }

        /// <summary>Marco de campo (Entry, Editor, Picker, DatePicker…).</summary>
        public static Border CampoTexto(View entrada, double alto = 50)
        {
            Desprender(entrada);
            entrada.BackgroundColor = Colors.Transparent;
            if (entrada is InputView iv)
            {
                iv.FontSize = 15;
                iv.TextColor = Tema.C("ColorTextStrong");
                iv.PlaceholderColor = Tema.C("ColorPlaceholder");
            }
            if (entrada is Editor ed) ed.AutoSize = EditorAutoSizeOption.TextChanges;
            return new Border
            {
                Stroke = Tema.C("ColorBorder"),
                StrokeThickness = 1.2,
                BackgroundColor = Tema.C("ColorBackground"),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(12, 2),
                MinimumHeightRequest = alto,
                Content = entrada
            };
        }

        /// <summary>Las pantallas se repintan reusando sus campos: Android se cierra si un control entra a un contenedor sin haber salido del anterior.</summary>
        private static void Desprender(View vista)
        {
            if (vista.Parent is Layout contenedor) contenedor.Remove(vista);
            else if (vista.Parent is Border marco && ReferenceEquals(marco.Content, vista)) marco.Content = null;
        }

        public static View HorasCampo(TimePicker inicio, TimePicker fin)
        {
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 12 };
            grid.Add(new VerticalStackLayout { Spacing = 6, Children = { Texto(Localizador.T("hora_inicio"), true, 12), CampoTexto(inicio) } }, 0);
            grid.Add(new VerticalStackLayout { Spacing = 6, Children = { Texto(Localizador.T("hora_fin"), true, 12), CampoTexto(fin) } }, 1);
            return grid;
        }

        public static Border Bloque(params View[] vistas)
        {
            var bloque = Tarjeta(vistas);
            bloque.Shadow = null;
            bloque.BackgroundColor = Tema.C("ColorSubtle");
            bloque.Stroke = Tema.C("ColorBorder");
            bloque.StrokeThickness = 1;
            return bloque;
        }

        public static View Separador() => new ContentView { HeightRequest = 1, BackgroundColor = Tema.C("ColorBorder") };

        private static T Columna<T>(this T vista, int col) where T : View
        {
            Grid.SetColumn(vista, col);
            return vista;
        }
 public static View EditorSemana(List<FranjaContrato> actuales,List<(int Dia,Switch Activo,TimePicker Inicio,TimePicker Fin)> franjas)
 {
  var lista=new VerticalStackLayout();
  var orden=new[]{1,2,3,4,5,6,0};
  for(int i=0;i<orden.Length;i++)
  {
   var dia=orden[i];
   var actual=actuales.FirstOrDefault(h=>h.DiaSemana==dia);
   var activo=new Switch{IsToggled=actual!=null,OnColor=Tema.C("ColorPrimary"),VerticalOptions=LayoutOptions.Center};
   TimePicker Selector(string? hora,int defecto)=>new(){Time=hora!=null&&TimeSpan.TryParse(hora,out var t)?t:TimeSpan.FromHours(defecto),Format="h:mm tt",FontSize=14,TextColor=Tema.C("ColorTextStrong"),HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center};
   var inicio=Selector(actual?.HoraInicio,8);
   var fin=Selector(actual?.HoraFin,14);
   var horas=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto),new ColumnDefinition(GridLength.Star)},ColumnSpacing=10};
   horas.Add(CampoTexto(inicio,44),0);
   horas.Add(new Label{Text="–",FontSize=16,TextColor=Tema.C("ColorTextMuted"),VerticalOptions=LayoutOptions.Center},1);
   horas.Add(CampoTexto(fin,44),2);
   horas.IsVisible=activo.IsToggled;
   var error=Texto("",false,12);error.TextColor=Tema.C("ColorDanger");error.IsVisible=false;
   var resumen=Texto("",true,12.5);
   void Validar()
   {
    error.IsVisible=activo.IsToggled&&fin.Time<=inicio.Time;
    error.Text=error.IsVisible?Localizador.F("horario_err_rango",Localizador.T($"dia_{dia}")):"";
    string H(TimeSpan? t)=>DateTime.Today.Add(t??TimeSpan.Zero).ToString("h:mm tt",Localizador.Cultura);
    resumen.Text=activo.IsToggled?$"{H(inicio.Time)} – {H(fin.Time)}":Localizador.T("dia_libre");
   }
   activo.Toggled+=(_,e)=>{horas.IsVisible=e.Value;Validar();};
   inicio.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(TimePicker.Time))Validar();};
   fin.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(TimePicker.Time))Validar();};
   Validar();
   var encabezado=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)}};
   encabezado.Add(new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center,Children={Titulo(Localizador.T($"dia_{dia}"),15),resumen}},0);
   encabezado.Add(activo,1);
   lista.Add(new VerticalStackLayout{Spacing=10,Padding=new Thickness(14,12),Children={encabezado,horas,error}});
   if(i<orden.Length-1) lista.Add(new ContentView{HeightRequest=1,BackgroundColor=Tema.C("ColorBorder"),Margin=new Thickness(14,0)});
   franjas.Add((dia,activo,inicio,fin));
  }
  var marco=new Border{Stroke=Tema.C("ColorBorder"),StrokeThickness=1,BackgroundColor=Tema.C("ColorBackground"),StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle{CornerRadius=16},Content=lista};
  return marco;
 }
    }
}
