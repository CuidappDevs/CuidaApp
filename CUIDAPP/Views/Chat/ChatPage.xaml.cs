using Plugin.Maui.Audio;
using CUIDAPP.Models.Chat;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Chat
{
    public partial class ChatPage : ContentPage, IQueryAttributable
    {
        // Mientras esta página esté abierta con una conversación cargada, el resto de la
        // app (GlobalNotifier) sabe que no debe mostrar un banner de "nuevo mensaje" para
        // esa misma conversación, porque el usuario ya la está viendo en vivo.
        public static int ConversacionAbiertaId;

        private readonly ApiService _apiService = new ApiService();
        private readonly IAudioManager _audioManager = AudioManager.Current;
        private IAudioRecorder? _grabador;
        private string? _rutaGrabacionActual;
        private System.Diagnostics.Stopwatch? _cronometroGrabacion;
        private int trabajoId;
        private int miUsuarioId;
        private int conversacionId;
        private bool grabando;
        private readonly HashSet<long> idsRenderizados = new();

        // Reproductores retenidos aquí a propósito: si no se guarda una referencia,
        // el recolector de basura puede liberar el IAudioPlayer a mitad de la reproducción
        // y el audio se corta o nunca llega a sonar.
        private readonly Dictionary<long, IAudioPlayer> _reproductores = new();
        private readonly Dictionary<long, Label> _iconosPlayPausa = new();

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("TrabajoId", out var t) && t is int id)
                trabajoId = id;

            if (query.TryGetValue("OtroNombre", out var nombre) && nombre is string n)
                LblOtroNombre.Text = n;

            if (query.TryGetValue("OtroFotoUrl", out var foto) && foto is string f && !string.IsNullOrWhiteSpace(f))
                ImgOtro.Source = $"{ApiService.ServerOrigin}{f}";
        }

        public ChatPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            miUsuarioId = Preferences.Default.Get("UserId", 0);

            RealtimeService.MensajeNuevo += OnMensajeNuevoTiempoReal;

            var conversacion = await _apiService.ObtenerOCrearConversacionAsync(trabajoId);
            if (conversacion == null)
            {
                await DisplayAlert("Error", "No se pudo abrir el chat. Intenta de nuevo.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            conversacionId = conversacion.Id;
            ConversacionAbiertaId = conversacionId;

            MostrarAdvertenciaSiEsPrimeraVez();

            var mensajes = await _apiService.ObtenerMensajesAsync(conversacionId);
            foreach (var mensaje in mensajes)
                AgregarMensajeSiNuevo(mensaje);

            await _apiService.MarcarMensajesLeidosAsync(conversacionId, miUsuarioId);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RealtimeService.MensajeNuevo -= OnMensajeNuevoTiempoReal;

            if (ConversacionAbiertaId == conversacionId)
                ConversacionAbiertaId = 0;

            foreach (var reproductor in _reproductores.Values)
            {
                reproductor.Stop();
                reproductor.Dispose();
            }
            _reproductores.Clear();
        }

        private void MostrarAdvertenciaSiEsPrimeraVez()
        {
            var clave = $"chat_advertencia_{conversacionId}";
            if (Preferences.Default.Get(clave, false))
                return;

            Preferences.Default.Set(clave, true);

            var aviso = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#FEF3C7"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 10),
                HorizontalOptions = LayoutOptions.Fill,
                Content = new Label
                {
                    Text = "🔒 Por tu seguridad, no compartas contraseñas, datos bancarios, cédula ni otra información sensible por este chat. Úsalo solo para coordinar el servicio.",
                    FontSize = 12,
                    FontFamily = "OpenSansRegular",
                    TextColor = Color.FromArgb("#92400E"),
                    HorizontalTextAlignment = TextAlignment.Center
                }
            };

            ListaMensajes.Add(aviso);
        }

        private async void OnMensajeNuevoTiempoReal(Mensaje mensaje)
        {
            if (mensaje.ConversacionId != conversacionId)
                return;

            AgregarMensajeSiNuevo(mensaje);

            if (mensaje.RemitenteId != miUsuarioId)
                await _apiService.MarcarMensajesLeidosAsync(conversacionId, miUsuarioId);
        }

        private void AgregarMensajeSiNuevo(Mensaje mensaje)
        {
            if (!idsRenderizados.Add(mensaje.Id))
                return;

            var esMio = mensaje.RemitenteId == miUsuarioId;

            View contenidoBurbuja = mensaje.Tipo switch
            {
                "imagen" => CrearContenidoImagen(mensaje, esMio),
                "audio" => CrearContenidoAudio(mensaje, esMio),
                _ => CrearContenidoTexto(mensaje, esMio)
            };

            var burbuja = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = esMio ? Color.FromArgb("#5A31F4") : Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = mensaje.Tipo == "imagen" ? new Thickness(6) : new Thickness(14, 10),
                MaximumWidthRequest = 260,
                HorizontalOptions = esMio ? LayoutOptions.End : LayoutOptions.Start,
                Content = contenidoBurbuja
            };

            ListaMensajes.Add(burbuja);
            _ = ScrollMensajes.ScrollToAsync(0, ListaMensajes.Height, true);
        }

        private static View CrearContenidoTexto(Mensaje mensaje, bool esMio)
        {
            return new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    new Label { Text = mensaje.Contenido, FontSize = 14, FontFamily = "OpenSansRegular", TextColor = esMio ? Colors.White : Color.FromArgb("#111827") },
                    CrearLabelHora(mensaje, esMio)
                }
            };
        }

        private View CrearContenidoImagen(Mensaje mensaje, bool esMio)
        {
            var imagen = new Image
            {
                Aspect = Aspect.AspectFill,
                WidthRequest = 200,
                HeightRequest = 200
            };

            if (!string.IsNullOrWhiteSpace(mensaje.UrlArchivo))
                imagen.Source = ImageSource.FromUri(new Uri($"{ApiService.ServerOrigin}{mensaje.UrlArchivo}"));

            var marco = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Content = imagen,
                Padding = 0
            };

            return new VerticalStackLayout
            {
                Spacing = 4,
                Children = { marco, CrearLabelHora(mensaje, esMio, margenExtra: true) }
            };
        }

        private View CrearContenidoAudio(Mensaje mensaje, bool esMio)
        {
            var colorAcento = esMio ? Colors.White : Color.FromArgb("#5A31F4");

            var iconoPlay = new Label
            {
                Text = "▶",
                FontSize = 13,
                TextColor = esMio ? Color.FromArgb("#5A31F4") : Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            _iconosPlayPausa[mensaje.Id] = iconoPlay;

            var botonPlay = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = esMio ? Colors.White : Color.FromArgb("#5A31F4"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
                WidthRequest = 36,
                HeightRequest = 36,
                Content = iconoPlay
            };

            botonPlay.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await AlternarReproduccionAsync(mensaje))
            });

            // "Espectro" decorativo: barras de altura variable (no son datos reales de la
            // onda, es una representación visual estándar de nota de voz tipo WhatsApp).
            var espectro = new HorizontalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
            var alturas = new[] { 8, 14, 10, 18, 12, 20, 9, 15, 11, 17, 8, 13 };
            foreach (var altura in alturas)
            {
                espectro.Children.Add(new BoxView
                {
                    WidthRequest = 3,
                    HeightRequest = altura,
                    CornerRadius = 2,
                    Color = colorAcento,
                    VerticalOptions = LayoutOptions.Center,
                    Opacity = 0.75
                });
            }

            var duracionTexto = mensaje.DuracionSegundos.HasValue
                ? TimeSpan.FromSeconds(mensaje.DuracionSegundos.Value).ToString(@"m\:ss")
                : "--:--";

            var fila = new HorizontalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    botonPlay,
                    espectro,
                    new Label { Text = duracionTexto, FontSize = 12, FontFamily = "OpenSansRegular", TextColor = esMio ? Color.FromArgb("#D6D0FB") : Color.FromArgb("#6B7280"), VerticalOptions = LayoutOptions.Center }
                }
            };

            return new VerticalStackLayout
            {
                Spacing = 3,
                Children = { fila, CrearLabelHora(mensaje, esMio) }
            };
        }

        private static Label CrearLabelHora(Mensaje mensaje, bool esMio, bool margenExtra = false)
        {
            return new Label
            {
                Text = mensaje.FechaEnvio.ToLocalTime().ToString("h:mm tt"),
                FontSize = 10,
                FontFamily = "OpenSansRegular",
                TextColor = esMio ? Color.FromArgb("#D6D0FB") : Color.FromArgb("#9CA3AF"),
                HorizontalOptions = LayoutOptions.End,
                Margin = margenExtra ? new Thickness(0, 0, 6, 4) : new Thickness(0)
            };
        }

        private async Task AlternarReproduccionAsync(Mensaje mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje.UrlArchivo))
                return;

            _iconosPlayPausa.TryGetValue(mensaje.Id, out var icono);

            try
            {
                // Si ya existe un reproductor para este mensaje, alterna play/pausa sin
                // volver a descargar ni recrear nada.
                if (_reproductores.TryGetValue(mensaje.Id, out var existente))
                {
                    if (existente.IsPlaying)
                    {
                        existente.Pause();
                        if (icono != null) icono.Text = "▶";
                    }
                    else
                    {
                        existente.Play();
                        if (icono != null) icono.Text = "⏸";
                    }
                    return;
                }

                var url = $"{ApiService.ServerOrigin}{mensaje.UrlArchivo}";
                var rutaLocal = Path.Combine(FileSystem.CacheDirectory, $"audio_{Math.Abs(url.GetHashCode())}.m4a");

                if (!File.Exists(rutaLocal))
                {
                    using var http = new HttpClient();
                    var bytes = await http.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(rutaLocal, bytes);
                }

                var reproductor = _audioManager.CreatePlayer(rutaLocal);
                reproductor.PlaybackEnded += (s, e) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (_iconosPlayPausa.TryGetValue(mensaje.Id, out var iconoFin))
                            iconoFin.Text = "▶";
                    });
                };

                _reproductores[mensaje.Id] = reproductor;
                reproductor.Play();
                if (icono != null) icono.Text = "⏸";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo reproducir la nota de voz.\n{ex.Message}", "OK");
            }
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            var texto = EntryMensaje.Text?.Trim();
            if (string.IsNullOrWhiteSpace(texto) || conversacionId == 0)
                return;

            EntryMensaje.Text = "";

            var mensaje = await _apiService.EnviarMensajeAsync(conversacionId, miUsuarioId, texto);
            if (mensaje != null)
                AgregarMensajeSiNuevo(mensaje);
        }

        private async void OnAdjuntarImagenTapped(object sender, EventArgs e)
        {
            if (conversacionId == 0)
                return;

            try
            {
                var foto = await MediaPicker.Default.PickPhotoAsync();
                if (foto == null)
                    return;

                var url = await _apiService.UploadFileAsync(foto.FullPath, "chat");
                if (url == null)
                {
                    await DisplayAlert("Error", "No se pudo subir la imagen. Intenta de nuevo.", "OK");
                    return;
                }

                var mensaje = await _apiService.EnviarMensajeAsync(conversacionId, miUsuarioId, "📷 Foto", "imagen", url);
                if (mensaje != null)
                    AgregarMensajeSiNuevo(mensaje);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo adjuntar la imagen.\n{ex.Message}", "OK");
            }
        }

        private async void OnMicrofonoTapped(object sender, EventArgs e)
        {
            if (conversacionId == 0)
                return;

            if (!grabando)
            {
                var permiso = await Permissions.RequestAsync<Permissions.Microphone>();
                if (permiso != PermissionStatus.Granted)
                {
                    await DisplayAlert("Permiso necesario", "Necesitamos acceso al micrófono para grabar notas de voz.", "OK");
                    return;
                }

                _grabador = _audioManager.CreateRecorder();
                _rutaGrabacionActual = Path.Combine(FileSystem.CacheDirectory, $"nota_{Guid.NewGuid():N}.m4a");
                await _grabador.StartAsync(_rutaGrabacionActual);
                _cronometroGrabacion = System.Diagnostics.Stopwatch.StartNew();

                grabando = true;
                ContenedorEntry.IsVisible = false;
                LblGrabando.IsVisible = true;
                IconoMicrofono.Fill = Color.FromArgb("#DC2626");
            }
            else
            {
                grabando = false;
                ContenedorEntry.IsVisible = true;
                LblGrabando.IsVisible = false;
                IconoMicrofono.Fill = Color.FromArgb("#5A31F4");

                if (_grabador == null || _rutaGrabacionActual == null)
                    return;

                await _grabador.StopAsync();

                var duracionSegundos = _cronometroGrabacion != null
                    ? Math.Max(1, (int)Math.Ceiling(_cronometroGrabacion.Elapsed.TotalSeconds))
                    : (int?)null;
                _cronometroGrabacion = null;

                if (duracionSegundos < 1)
                {
                    await DisplayAlert("Nota muy corta", "Mantén presionado un poco más para grabar algo audible.", "OK");
                    return;
                }

                var url = await _apiService.UploadFileAsync(_rutaGrabacionActual, "chat");
                if (url == null)
                {
                    await DisplayAlert("Error", "No se pudo subir la nota de voz. Intenta de nuevo.", "OK");
                    return;
                }

                var mensaje = await _apiService.EnviarMensajeAsync(conversacionId, miUsuarioId, "🎤 Nota de voz", "audio", url, duracionSegundos);
                if (mensaje != null)
                    AgregarMensajeSiNuevo(mensaje);
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
