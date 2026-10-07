using CUIDAPP.Localization;
using CUIDAPP.Views.Comun;

namespace CUIDAPP.Helpers
{
    public enum TipoAlerta { Auto, Info, Exito, Error, Advertencia, Pregunta }

    /// <summary>
    /// Alerta única de la app, con diseño propio en lugar del diálogo nativo.
    /// Misma firma que DisplayAlert: un botón (Task) o dos botones (Task&lt;bool&gt;: true = aceptar).
    /// Las alertas se muestran de una en una (si llegan varias, esperan su turno).
    /// </summary>
    public static class Alerta
    {
        private static readonly SemaphoreSlim _turno = new(1, 1);

        public static Task MostrarAsync(string titulo, string mensaje, string boton, TipoAlerta tipo = TipoAlerta.Auto)
            => MostrarInternoAsync(titulo, mensaje, boton, null, tipo);

        public static Task<bool> MostrarAsync(string titulo, string mensaje, string aceptar, string cancelar, TipoAlerta tipo = TipoAlerta.Auto)
            => MostrarInternoAsync(titulo, mensaje, aceptar, cancelar, tipo);

        private static async Task<bool> MostrarInternoAsync(string titulo, string mensaje, string aceptar, string? cancelar, TipoAlerta tipo)
        {
            if (tipo == TipoAlerta.Auto)
                tipo = Deducir(titulo, cancelar != null);

            await _turno.WaitAsync();
            try
            {
                return await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var vista = new AlertaVista(titulo, mensaje, aceptar, cancelar, tipo);
                    if (!await PresentarAsync(vista))
                        return false;
                    return await vista.Resultado;
                });
            }
            finally
            {
                _turno.Release();
            }
        }

        /// <summary>
        /// Muestra la vista encima de todo SIN navegar: en Android, un diálogo nativo transparente.
        /// Así la página de abajo no recibe OnDisappearing/OnAppearing (no se cortan timers,
        /// SignalR ni animaciones). En otras plataformas se usa una página modal.
        /// </summary>
        private static async Task<bool> PresentarAsync(AlertaVista vista)
        {
            var pagina = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (pagina == null)
                return false;

#if ANDROID
            var contexto = pagina.Handler?.MauiContext;
            var actividad = Platform.CurrentActivity;
            if (contexto == null || actividad == null)
                return false;

            var nativa = Microsoft.Maui.Platform.ElementExtensions.ToPlatform(vista, contexto);
            var dialogo = new Android.App.Dialog(actividad, Android.Resource.Style.ThemeTranslucentNoTitleBar);
            dialogo.SetCancelable(false);
            dialogo.SetContentView(nativa, new Android.Views.ViewGroup.LayoutParams(
                Android.Views.ViewGroup.LayoutParams.MatchParent, Android.Views.ViewGroup.LayoutParams.MatchParent));
            dialogo.Window?.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(Android.Graphics.Color.Transparent));
            dialogo.Window?.SetLayout(Android.Views.ViewGroup.LayoutParams.MatchParent, Android.Views.ViewGroup.LayoutParams.MatchParent);

            // Atrás del sistema = cancelar, con animación de salida.
            dialogo.KeyPress += (_, e) =>
            {
                e.Handled = e.KeyCode == Android.Views.Keycode.Back;
                if (e.Handled && e.Event?.Action == Android.Views.KeyEventActions.Up)
                    _ = vista.CancelarAsync();
            };
            vista.Cerrada += (_, _) =>
            {
                dialogo.Dismiss();
                vista.Handler?.DisconnectHandler();
            };

            dialogo.Show();
            // Segunda medición ya con el tamaño real de la ventana (por si el texto ocupa más líneas).
            nativa.Post(() => nativa.RequestLayout());
            _ = vista.AnimarEntradaAsync();
            return true;
#else
            var modal = new ContentPage { BackgroundColor = Colors.Transparent, Content = vista };
            Shell.SetNavBarIsVisible(modal, false);
            vista.Cerrada += async (_, _) => await modal.Navigation.PopModalAsync(false);
            await pagina.Navigation.PushModalAsync(modal, false);
            _ = vista.AnimarEntradaAsync();
            return true;
#endif
        }

        // Tipo a partir del título (en el idioma actual) cuando no se indica explícitamente.
        private static TipoAlerta Deducir(string titulo, bool dosBotones)
        {
            var t = (titulo ?? "").Trim().ToLowerInvariant();
            bool Es(params string[] claves) => claves.Any(c => t == Localizador.T(c).Trim().ToLowerInvariant());

            if (t.Contains("error") || Es("error")) return TipoAlerta.Error;
            if (t.Contains("éxito") || t.Contains("exito") || t.Contains("success") || Es("exito_2", "listo", "codigo_enviado")) return TipoAlerta.Exito;
            if (Es("atencion", "aviso", "advertencia", "limite_alcanzado")) return TipoAlerta.Advertencia;
            return dosBotones ? TipoAlerta.Pregunta : TipoAlerta.Info;
        }
    }
}
