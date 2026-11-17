namespace CUIDAPP.Services
{
    /// <summary>
    /// "Continuar con Google": abre el selector de cuentas de Google del teléfono (Credential Manager)
    /// y devuelve el ID token firmado. El servidor lo valida con el ID de cliente Web
    /// (api/Auth/google); la app nunca ve la contraseña de Google.
    /// </summary>
    public static class GoogleLogin
    {
        /// <summary>ID de cliente "Aplicación web" de Google Cloud (proyecto Cuidapp). No es secreto.</summary>
        public const string ClientIdWeb = "898865790800-05q4u9h3hn78atr83ekpjm3d9qfjhmcn.apps.googleusercontent.com";

        public enum Resultado { Ok, Cancelado, SinCuentas, NoDisponible, Error }

        public static bool Disponible =>
#if ANDROID
            true;
#else
            false;
#endif

        public static async Task<(Resultado Resultado, string? IdToken)> ObtenerIdTokenAsync()
        {
#if ANDROID
            var actividad = Platform.CurrentActivity;
            if (actividad == null) return (Resultado.NoDisponible, null);

            try
            {
                var opcion = new Google.Android.Libraries.Identity.GoogleId.GetSignInWithGoogleOption.Builder(ClientIdWeb).Build();
                var solicitud = new AndroidX.Credentials.GetCredentialRequest.Builder().AddCredentialOption(opcion).Build();
                var gestor = AndroidX.Credentials.CredentialManager.Companion.Create(actividad);

                var respuesta = new TaskCompletionSource<Java.Lang.Object>();
                gestor.GetCredentialAsync(actividad, solicitud, new Android.OS.CancellationSignal(),
                    Java.Util.Concurrent.Executors.NewSingleThreadExecutor()!, new Callback(respuesta));

                var resultado = await respuesta.Task;
                var credencial = (resultado as AndroidX.Credentials.GetCredentialResponse)?.Credential;
                if (credencial is AndroidX.Credentials.CustomCredential personalizada
                    && personalizada.Type == Google.Android.Libraries.Identity.GoogleId.GoogleIdTokenCredential.TypeGoogleIdTokenCredential)
                {
                    var google = Google.Android.Libraries.Identity.GoogleId.GoogleIdTokenCredential.CreateFrom(personalizada.Data);
                    return string.IsNullOrWhiteSpace(google.IdToken) ? (Resultado.Error, null) : (Resultado.Ok, google.IdToken);
                }
                return (Resultado.Error, null);
            }
            catch (ErrorGoogle ex) when (ex.Tipo is "cancelado")
            {
                return (Resultado.Cancelado, null);
            }
            catch (ErrorGoogle ex) when (ex.Tipo is "sin_cuentas")
            {
                return (Resultado.SinCuentas, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GoogleLogin] {ex}");
                return (Resultado.Error, null);
            }
#else
            await Task.CompletedTask;
            return (Resultado.NoDisponible, null);
#endif
        }

#if ANDROID
        private sealed class ErrorGoogle(string tipo, string mensaje) : Exception(mensaje)
        {
            public string Tipo { get; } = tipo;
        }

        private sealed class Callback(TaskCompletionSource<Java.Lang.Object> tcs)
            : Java.Lang.Object, AndroidX.Credentials.ICredentialManagerCallback
        {
            public void OnResult(Java.Lang.Object? result)
            {
                if (result != null) tcs.TrySetResult(result);
                else tcs.TrySetException(new ErrorGoogle("error", "Sin respuesta"));
            }

            public void OnError(Java.Lang.Object e)
            {
                // Las excepciones llegan como objetos Java: se distinguen por el nombre de su clase.
                var tipo = e.Class.SimpleName switch
                {
                    "GetCredentialCancellationException" => "cancelado",
                    "NoCredentialException" => "sin_cuentas",
                    _ => "error"
                };
                tcs.TrySetException(new ErrorGoogle(tipo, e.ToString() ?? tipo));
            }
        }
#endif
    }
}
