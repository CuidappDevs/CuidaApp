using Plugin.Maui.Audio;

namespace CUIDAPP.Services
{
    /// <summary>
    /// Caja negra TRANSPARENTE: mientras un servicio está en curso, el teléfono del Care Partner graba
    /// audio por tramos y los sube al servidor. NO es oculta: en Android corre un servicio en primer plano
    /// con una notificación visible ("Grabación de seguridad activa") y el punto del micrófono del sistema.
    /// El cliente y el Care Partner la aceptan en los términos y ven el aviso antes de empezar.
    /// Solo se usa si la administración la tiene encendida (GET /api/grabacion/activa).
    /// </summary>
    public static class GrabacionSeguridad
    {
        // Tramos cortos: si se corta la conexión o la app, no se pierde más que el último tramo, y cada
        // archivo se queda muy por debajo del límite de subida (10 MB).
        private static readonly TimeSpan DuracionTramo = TimeSpan.FromMinutes(3);

        private static readonly IAudioManager _audio = AudioManager.Current;
        private static readonly object _candado = new();

        private static int _trabajoId;
        private static int _cuidadorId;
        private static CancellationTokenSource? _cts;
        private static Task? _bucle;

        public static bool Activa => _trabajoId != 0;

        /// <summary>¿La administración tiene encendida la grabación de seguridad?</summary>
        public static async Task<bool> EncendidaEnServidorAsync()
        {
            try { return await new ApiService().GrabacionSeguridadActivaAsync(); }
            catch { return false; }
        }

        /// <summary>
        /// Arranca la grabación del servicio. Pide permiso de micrófono; si no lo dan, no graba (devuelve false).
        /// Debe llamarse justo después de iniciar el servicio con el PIN.
        /// </summary>
        public static async Task<bool> IniciarAsync(int trabajoId, int cuidadorId)
        {
            if (Activa)
                return true;

            var permiso = await Permissions.RequestAsync<Permissions.Microphone>();
            if (permiso != PermissionStatus.Granted)
                return false;

            lock (_candado)
            {
                if (_trabajoId != 0)
                    return true;
                _trabajoId = trabajoId;
                _cuidadorId = cuidadorId;
                _cts = new CancellationTokenSource();
            }

            // Marca el servicio como grabado (consentido) y enciende la notificación visible.
            _ = new ApiService().MarcarGrabacionServicioAsync(trabajoId, cuidadorId);
            IniciarServicioPrimerPlano();

            _bucle = Task.Run(() => GrabarPorTramosAsync(_cts!.Token));
            return true;
        }

        /// <summary>Detiene la grabación y sube el último tramo. Se llama al finalizar el servicio.</summary>
        public static async Task DetenerAsync()
        {
            CancellationTokenSource? cts;
            Task? bucle;
            lock (_candado)
            {
                if (_trabajoId == 0)
                    return;
                cts = _cts;
                bucle = _bucle;
            }

            cts?.Cancel();
            try { if (bucle != null) await bucle; } catch { /* cancelado */ }

            lock (_candado)
            {
                _trabajoId = 0;
                _cuidadorId = 0;
                _cts?.Dispose();
                _cts = null;
                _bucle = null;
            }
            DetenerServicioPrimerPlano();
        }

        private static async Task GrabarPorTramosAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var trabajoId = _trabajoId;
                var cuidadorId = _cuidadorId;
                if (trabajoId == 0)
                    break;

                var ruta = Path.Combine(FileSystem.CacheDirectory, $"grab_{trabajoId}_{Guid.NewGuid():N}.m4a");
                IAudioRecorder grabador;
                try
                {
                    // AAC mono a 16 kHz: voz clara y archivos pequeños. Si el dispositivo no lo soporta, usa su formato por defecto.
                    grabador = _audio.CreateRecorder();
                    var opciones = new AudioRecorderOptions
                    {
                        Encoding = Encoding.Aac,
                        SampleRate = 16000,
                        Channels = ChannelType.Mono,
                        ThrowIfNotSupported = false
                    };
                    await grabador.StartAsync(ruta, opciones);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Grabacion] No se pudo iniciar un tramo: {ex.Message}");
                    try { await Task.Delay(TimeSpan.FromSeconds(10), token); } catch { }
                    continue;
                }

                var reloj = System.Diagnostics.Stopwatch.StartNew();
                try { await Task.Delay(DuracionTramo, token); }
                catch (TaskCanceledException) { /* se termina el servicio: cerramos este tramo igual */ }

                try
                {
                    await grabador.StopAsync();
                    reloj.Stop();
                    var duracion = Math.Max(1, (int)Math.Round(reloj.Elapsed.TotalSeconds));
                    // La subida no debe cancelarse con el token: aunque el servicio terminó, este tramo debe guardarse.
                    _ = SubirTramoAsync(trabajoId, cuidadorId, ruta, duracion);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Grabacion] No se pudo cerrar un tramo: {ex.Message}");
                }
            }
        }

        private static async Task SubirTramoAsync(int trabajoId, int cuidadorId, string ruta, int duracion)
        {
            try
            {
                if (!File.Exists(ruta))
                    return;
                var api = new ApiService();
                var url = await api.UploadFileAsync(ruta, $"grabacion/{trabajoId}");
                if (!string.IsNullOrWhiteSpace(url))
                    await api.RegistrarSegmentoGrabacionAsync(trabajoId, cuidadorId, url!, duracion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Grabacion] No se pudo subir un tramo: {ex.Message}");
            }
            finally
            {
                try { if (File.Exists(ruta)) File.Delete(ruta); } catch { }
            }
        }

        private static void IniciarServicioPrimerPlano()
        {
#if ANDROID
            try
            {
                var contexto = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(contexto, typeof(Platforms.Android.GrabacionForegroundService));
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                    contexto.StartForegroundService(intent);
                else
                    contexto.StartService(intent);
            }
            catch (Exception ex) { Console.WriteLine($"[Grabacion] No se pudo iniciar el servicio en primer plano: {ex.Message}"); }
#endif
        }

        private static void DetenerServicioPrimerPlano()
        {
#if ANDROID
            try
            {
                var contexto = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(contexto, typeof(Platforms.Android.GrabacionForegroundService));
                contexto.StopService(intent);
            }
            catch (Exception ex) { Console.WriteLine($"[Grabacion] No se pudo detener el servicio en primer plano: {ex.Message}"); }
#endif
        }
    }
}
