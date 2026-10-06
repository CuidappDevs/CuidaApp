using System.Diagnostics;
using CUIDAPP.Localization;
using CUIDAPP.Views.Seguridad;

namespace CUIDAPP.Services
{
    public enum ResultadoDeadMan
    {
        Confirmada,     // el cuidador dijo "Estoy bien"
        AlertaEnviada,  // se agotaron los 30 s y la alerta llegó al servidor
        ErrorEnvio      // se agotaron los 30 s pero no se pudo enviar la alerta
    }

    /// <summary>
    /// "Hombre muerto": mientras el cuidador tiene un servicio En Progreso escucha el acelerómetro
    /// y el giroscopio. Si detecta un impacto fuerte seguido de inmovilidad prolongada, muestra una
    /// cuenta regresiva de 30 s; si no confirma "Estoy bien", dispara POST /sos/dead-man-triggered
    /// (alerta al panel admin y aviso al familiar).
    ///
    /// Los sensores solo funcionan con el proceso vivo: en Android lo mantiene el servicio en primer
    /// plano (ConexionForegroundService), que se inicia al iniciar sesión.
    /// </summary>
    public static class DeadManService
    {
        public const int SegundosConfirmacion = 30;
        private const int IntentosEnvio = 3;

        private static readonly object _candado = new();
        private static readonly DetectorCaidas _detector = new();
        private static readonly Stopwatch _reloj = Stopwatch.StartNew();

        private static int _trabajoId;
        private static bool _escuchandoSensores;
        private static Timer? _temporizador;
        private static int _restantes;
        private static double _impactoG;
        private static long _segundosInmovil;

        public static bool Activo => _trabajoId != 0;
        public static bool CuentaRegresivaActiva { get; private set; }
        public static int SegundosRestantes => _restantes;
        public static bool PaginaVisible { get; set; }

        /// <summary>Cada segundo de la cuenta regresiva (segundos restantes). Se dispara en el hilo principal.</summary>
        public static event Action<int>? Tick;
        public static event Action<ResultadoDeadMan>? Finalizada;

        // ---------------------------------------------------------------- Ciclo de vida

        public static void Iniciar(int trabajoId)
        {
            lock (_candado)
            {
                if (_trabajoId == trabajoId && _escuchandoSensores)
                    return;
                _trabajoId = trabajoId;
                _detector.Reiniciar();
            }

            IniciarSensores();
        }

        public static void Detener()
        {
            lock (_candado)
            {
                _trabajoId = 0;
                _detector.Reiniciar();
                CancelarCuentaRegresivaInterno();
            }

            DetenerSensores();
        }

        /// <summary>Alinea el monitoreo con el servidor: activo solo si el cuidador tiene un servicio En Progreso.</summary>
        public static async Task SincronizarAsync(int cuidadorId)
        {
            if (cuidadorId == 0)
            {
                Detener();
                return;
            }

            try
            {
                var trabajos = await new ApiService().ObtenerTrabajosAsync(cuidadorId);
                var enProgreso = trabajos.FirstOrDefault(t => t.Estado == 3);
                if (enProgreso != null)
                    Iniciar(enProgreso.Id);
                else
                    Detener();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeadMan] No se pudo sincronizar: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------- Sensores

        private static void IniciarSensores()
        {
            try
            {
                if (_escuchandoSensores)
                    return;

                if (Accelerometer.Default.IsSupported && !Accelerometer.Default.IsMonitoring)
                {
                    Accelerometer.Default.ReadingChanged += OnAcelerometro;
                    Accelerometer.Default.Start(SensorSpeed.Game);
                }

                if (Gyroscope.Default.IsSupported && !Gyroscope.Default.IsMonitoring)
                {
                    Gyroscope.Default.ReadingChanged += OnGiroscopio;
                    Gyroscope.Default.Start(SensorSpeed.Game);
                }

                _escuchandoSensores = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeadMan] No se pudieron iniciar los sensores: {ex.Message}");
            }
        }

        private static void DetenerSensores()
        {
            try
            {
                if (Accelerometer.Default.IsMonitoring)
                {
                    Accelerometer.Default.ReadingChanged -= OnAcelerometro;
                    Accelerometer.Default.Stop();
                }

                if (Gyroscope.Default.IsMonitoring)
                {
                    Gyroscope.Default.ReadingChanged -= OnGiroscopio;
                    Gyroscope.Default.Stop();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeadMan] Error deteniendo sensores: {ex.Message}");
            }
            finally
            {
                _escuchandoSensores = false;
            }
        }

        private static void OnGiroscopio(object? sender, GyroscopeChangedEventArgs e)
        {
            var v = e.Reading.AngularVelocity;
            lock (_candado)
                _detector.ProcesarGiroscopio(v.X, v.Y, v.Z);
        }

        private static void OnAcelerometro(object? sender, AccelerometerChangedEventArgs e)
        {
            var v = e.Reading.Acceleration;
            bool pedirConfirmacion;

            lock (_candado)
            {
                if (_trabajoId == 0 || CuentaRegresivaActiva)
                    return;

                pedirConfirmacion = _detector.ProcesarAcelerometro(v.X, v.Y, v.Z, _reloj.ElapsedMilliseconds);
                if (pedirConfirmacion)
                {
                    _impactoG = _detector.ImpactoG;
                    _segundosInmovil = _detector.SegundosInmovil;
                }
            }

            if (pedirConfirmacion)
                IniciarCuentaRegresiva();
        }

        // ---------------------------------------------------------------- Cuenta regresiva

        private static void IniciarCuentaRegresiva()
        {
            lock (_candado)
            {
                if (CuentaRegresivaActiva || _trabajoId == 0)
                    return;
                CuentaRegresivaActiva = true;
                _restantes = SegundosConfirmacion;
                _temporizador = new Timer(_ => TickSegundo(), null, 1000, 1000);
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Vibrar(800);
                MostrarPagina();
                Tick?.Invoke(SegundosConfirmacion);
            });

            // Con la app en segundo plano no se puede abrir una pantalla: se avisa con una notificación.
            if (!App.EstaEnPrimerPlano)
                NativeNotifier.Mostrar(Localizador.T("dm_notif_titulo"), Localizador.T("dm_notif_texto"));
        }

        private static void TickSegundo()
        {
            int restantes;
            lock (_candado)
            {
                if (!CuentaRegresivaActiva)
                    return;
                restantes = --_restantes;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Vibrar(300);
                Tick?.Invoke(Math.Max(restantes, 0));
            });

            if (restantes <= 0)
                _ = DispararAlertaAsync();
        }

        /// <summary>El cuidador pulsó "Estoy bien": se cancela todo y no se envía nada.</summary>
        public static void Confirmar()
        {
            lock (_candado)
            {
                if (!CuentaRegresivaActiva)
                    return;
                CancelarCuentaRegresivaInterno();
                _detector.Reiniciar();
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CancelarVibracion();
                Finalizada?.Invoke(ResultadoDeadMan.Confirmada);
            });
        }

        private static void CancelarCuentaRegresivaInterno()
        {
            _temporizador?.Dispose();
            _temporizador = null;
            CuentaRegresivaActiva = false;
            _restantes = 0;
        }

        // ---------------------------------------------------------------- Alerta

        private static async Task DispararAlertaAsync()
        {
            int trabajoId;
            lock (_candado)
            {
                if (!CuentaRegresivaActiva)
                    return;
                CancelarCuentaRegresivaInterno();
                trabajoId = _trabajoId;
            }

            var usuarioId = Preferences.Default.Get("UserId", 0);
            var (lat, lng) = await ObtenerUbicacionAsync();
            var api = new ApiService();

            var enviada = false;
            for (var intento = 1; intento <= IntentosEnvio && !enviada; intento++)
            {
                enviada = await api.EnviarDeadManAsync(trabajoId, usuarioId, lat, lng, Math.Round(_impactoG, 1), (int)_segundosInmovil);
                if (!enviada && intento < IntentosEnvio)
                    await Task.Delay(TimeSpan.FromSeconds(3));
            }

            lock (_candado)
                _detector.Reiniciar();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CancelarVibracion();
                Finalizada?.Invoke(enviada ? ResultadoDeadMan.AlertaEnviada : ResultadoDeadMan.ErrorEnvio);
            });
        }

        // Sin pedir permisos (en segundo plano no se puede mostrar el diálogo): ubicación actual,
        // si no la última conocida, y como último recurso la ubicación por defecto.
        private static async Task<(double Lat, double Lng)> ObtenerUbicacionAsync()
        {
            try
            {
                var actual = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(8)));
                if (actual != null)
                    return (actual.Latitude, actual.Longitude);
            }
            catch (Exception ex) { Console.WriteLine($"[DeadMan] Sin ubicación actual: {ex.Message}"); }

            try
            {
                var ultima = await Geolocation.Default.GetLastKnownLocationAsync();
                if (ultima != null)
                    return (ultima.Latitude, ultima.Longitude);
            }
            catch (Exception ex) { Console.WriteLine($"[DeadMan] Sin última ubicación: {ex.Message}"); }

            return (LocationService.LatitudPorDefecto, LocationService.LongitudPorDefecto);
        }

        // ---------------------------------------------------------------- UI

        /// <summary>Abre la pantalla de confirmación si hay una cuenta regresiva en curso y no está ya visible.</summary>
        public static void MostrarSiPendiente()
        {
            if (CuentaRegresivaActiva)
                MainThread.BeginInvokeOnMainThread(MostrarPagina);
        }

        private static void MostrarPagina()
        {
            if (PaginaVisible)
                return;

            var navegacion = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
            if (navegacion == null)
                return;

            PaginaVisible = true;
            _ = navegacion.PushModalAsync(new ConfirmarBienestarPage());
        }

        private static void Vibrar(int milisegundos)
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(milisegundos)); }
            catch (Exception) { /* dispositivo sin vibrador o sin permiso */ }
        }

        private static void CancelarVibracion()
        {
            try { Vibration.Default.Cancel(); }
            catch (Exception) { }
        }

        /// <summary>
        /// Aviso de llevar siempre el celular encima. Se muestra al aceptar y al iniciar un servicio.
        /// Si el cuidador no tiene contacto de emergencia, ofrece agregarlo.
        /// </summary>
        /// <returns>true si el cuidador eligió ir a su perfil a agregar el contacto.</returns>
        public static async Task<bool> MostrarAvisoCelularAsync(Page pagina)
        {
            await Alerta.MostrarAsync(Localizador.T("dm_aviso_titulo"), Localizador.T("dm_aviso_texto"), Localizador.T("entendido"));

            try
            {
                var cuidadorId = Preferences.Default.Get("UserId", 0);
                var contacto = await new ApiService().ObtenerContactoEmergenciaCuidadorAsync(cuidadorId);
                var tiene = contacto != null && !string.IsNullOrWhiteSpace(contacto.Nombre)
                            && (!string.IsNullOrWhiteSpace(contacto.Telefono) || !string.IsNullOrWhiteSpace(contacto.Email));
                if (tiene)
                    return false;

                return await Alerta.MostrarAsync(Localizador.T("dm_sin_contacto_titulo"), Localizador.T("dm_sin_contacto_texto"),
                    Localizador.T("dm_agregar_ahora"), Localizador.T("despues"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeadMan] No se pudo verificar el contacto: {ex.Message}");
                return false;
            }
        }
    }
}
