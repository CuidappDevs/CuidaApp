using CUIDAPP.Localization;

namespace CUIDAPP.Helpers
{
    /// <summary>Chip del método de pago (efectivo / transferencia) debajo del monto del servicio.</summary>
    public static class MetodoPagoVista
    {
        private const string IconoEfectivo = "M19 14V6C19 4.9 18.1 4 17 4H3C1.9 4 1 4.9 1 6V14C1 15.1 1.9 16 3 16H17C18.1 16 19 15.1 19 14ZM10 13C8.34 13 7 11.66 7 10C7 8.34 8.34 7 10 7C11.66 7 13 8.34 13 10C13 11.66 11.66 13 10 13ZM23 7V18C23 19.1 22.1 20 21 20H4V18H21V7H23Z";
        private const string IconoTransferencia = "M4 10V17H7V10H4ZM10 10V17H13V10H10ZM2 22H21V19H2V22ZM16 10V17H19V10H16ZM11.5 1L2 6V8H21V6L11.5 1Z";

        public static void Mostrar(Border chip, Microsoft.Maui.Controls.Shapes.Path icono, Label texto, string? metodo, bool paraCuidador)
        {
            chip.IsVisible = metodo is "efectivo" or "transferencia";
            if (!chip.IsVisible)
                return;

            var efectivo = metodo == "efectivo";
            icono.Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
                .ConvertFromInvariantString(efectivo ? IconoEfectivo : IconoTransferencia)!;
            texto.Text = paraCuidador
                ? Localizador.T(efectivo ? "chip_efectivo_cuidador" : "chip_transferencia_cuidador")
                : Localizador.T(efectivo ? "pago_efectivo" : "pago_transferencia");
        }
    }
}
