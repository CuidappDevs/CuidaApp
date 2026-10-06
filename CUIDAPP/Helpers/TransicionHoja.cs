namespace CUIDAPP.Helpers
{
    /// <summary>
    /// Transición compartida de las pantallas de autenticación (encabezado azul + hoja blanca).
    /// Entre ellas se navega sin la animación del sistema: la hoja sale hacia abajo, se cambia
    /// de página (el encabezado azul es idéntico, así que no se nota el corte) y la nueva hoja sube.
    /// </summary>
    public static class TransicionHoja
    {
        private static double AltoPantalla =>
            DeviceDisplay.Current.MainDisplayInfo.Height / DeviceDisplay.Current.MainDisplayInfo.Density;

        // Aproximación de cubic-bezier(0.23, 1, 0.32, 1): ease-out marcado
        private static readonly Easing EaseOutFuerte = new(t => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 4));

        /// <summary>
        /// La hoja toma su alto natural y el encabezado ocupa lo que sobra de la pantalla
        /// (con un mínimo). Así la hoja queda pegada abajo y nunca se corta: si el contenido
        /// es más alto, la hoja sube; si no cabe, se hace scroll. Usa alturas ya dibujadas.
        /// </summary>
        public static void AjustarAlto(View encabezado, Border hoja, double altoPagina, double minEncabezado)
        {
            if (altoPagina <= 0 || hoja.Height <= 0) return;

            var p = hoja.Padding;
            var inferior = 24 + BarraEstado.AltoInferior();
            if (Math.Abs(p.Bottom - inferior) > 0.5)
                hoja.Padding = new Thickness(p.Left, p.Top, p.Right, inferior);

            // La hoja se monta sobre el encabezado (Margin.Top negativo).
            var visibleHoja = hoja.Height + hoja.Margin.Top;
            var deseado = Math.Max(minEncabezado, altoPagina - visibleHoja);
            if (Math.Abs(encabezado.HeightRequest - deseado) > 0.5)
                encabezado.HeightRequest = deseado;
        }

        /// <summary>Estado inicial (antes del primer frame): hoja fuera de pantalla, contenido oculto.</summary>
        public static void Preparar(View hoja, View insignia, Layout contenido)
        {
            hoja.TranslationY = AltoPantalla;
            insignia.Opacity = 0;
            insignia.Scale = 0.92;
            foreach (var hijo in Hijos(contenido))
            {
                hijo.Opacity = 0;
                hijo.TranslationY = 12;
            }
        }

        public static async Task EntrarAsync(View hoja, View insignia, Layout contenido)
        {
            _ = insignia.FadeTo(1, 350, Easing.CubicOut);
            _ = insignia.ScaleTo(1, 450, Easing.CubicOut);

            await Task.Delay(80);
            _ = hoja.TranslateTo(0, 0, 520, EaseOutFuerte);

            // Contenido escalonado; no bloquea la interacción.
            await Task.Delay(200);
            foreach (var hijo in Hijos(contenido))
            {
                _ = hijo.FadeTo(1, 300, Easing.CubicOut);
                _ = hijo.TranslateTo(0, 0, 350, Easing.CubicOut);
                await Task.Delay(45);
            }
        }

        public static Task SalirAsync(View hoja, View insignia, Layout contenido)
        {
            foreach (var hijo in Hijos(contenido))
                _ = hijo.FadeTo(0, 140, Easing.CubicOut);

            return Task.WhenAll(
                insignia.FadeTo(0, 200, Easing.CubicOut),
                insignia.ScaleTo(0.94, 200, Easing.CubicOut),
                hoja.TranslateTo(0, AltoPantalla, 320, Easing.CubicInOut));
        }

        private static IEnumerable<VisualElement> Hijos(Layout contenido)
        {
            // Si el contenido tiene pasos (Step1/Step2), se animan los hijos del paso visible.
            foreach (var hijo in contenido.Children.OfType<VisualElement>())
            {
                if (!hijo.IsVisible) continue;
                if (hijo is Layout paso && contenido is Grid)
                    foreach (var h in paso.Children.OfType<VisualElement>()) yield return h;
                else
                    yield return hijo;
            }
        }
    }
}
