namespace CUIDAPP.Services
{
    /// <summary>
    /// Detecta "impacto fuerte + inmovilidad prolongada" a partir de muestras del acelerómetro
    /// (en g, con la gravedad incluida, como las entrega MAUI) y del giroscopio (rad/s).
    /// Clase pura (sin MAUI) para poder probarla con datos simulados.
    ///
    /// Funcionamiento:
    ///   1. Esperando: una muestra con magnitud >= UmbralImpactoG se considera un impacto.
    ///   2. Vigilando: tras el impacto se mide cuánto tiempo seguido el teléfono está quieto
    ///      (magnitud ≈ 1 g y giro bajo). Cualquier movimiento reinicia la cuenta.
    ///   3. Si la quietud continua llega a QuietudRequeridaMs, se pide confirmación al usuario.
    ///   4. Si pasa VentanaVigilanciaMs desde el impacto sin llegar a eso, se descarta.
    /// </summary>
    public sealed class DetectorCaidas
    {
        // Umbrales: valores iniciales razonables; conviene calibrarlos con pruebas en campo.
        public double UmbralImpactoG { get; init; } = 3.0;
        public double ToleranciaQuietudG { get; init; } = 0.10;       // |magnitud - 1 g|
        public double ToleranciaGiroRadS { get; init; } = 0.35;
        public long QuietudRequeridaMs { get; init; } = 60_000;
        public long VentanaVigilanciaMs { get; init; } = 600_000;

        private bool _vigilando;
        private long _tImpacto;
        private long? _inicioQuietud;
        private double _giro;

        public bool Vigilando => _vigilando;
        public double ImpactoG { get; private set; }
        public long SegundosInmovil { get; private set; }

        public void ProcesarGiroscopio(double x, double y, double z) =>
            _giro = Math.Sqrt(x * x + y * y + z * z);

        /// <returns>true cuando hay que pedir confirmación (impacto + inmovilidad prolongada).</returns>
        public bool ProcesarAcelerometro(double x, double y, double z, long tMs)
        {
            var magnitud = Math.Sqrt(x * x + y * y + z * z);

            if (magnitud >= UmbralImpactoG)
            {
                // Impacto (o uno nuevo, más fuerte, durante la vigilancia): se reinicia la medición.
                if (!_vigilando || magnitud > ImpactoG)
                    ImpactoG = magnitud;
                _vigilando = true;
                _tImpacto = tMs;
                _inicioQuietud = null;
                return false;
            }

            if (!_vigilando)
                return false;

            if (tMs - _tImpacto > VentanaVigilanciaMs)
            {
                Reiniciar();
                return false;
            }

            var quieto = Math.Abs(magnitud - 1.0) < ToleranciaQuietudG && _giro < ToleranciaGiroRadS;
            if (!quieto)
            {
                _inicioQuietud = null;
                return false;
            }

            _inicioQuietud ??= tMs;
            if (tMs - _inicioQuietud.Value < QuietudRequeridaMs)
                return false;

            SegundosInmovil = (tMs - _inicioQuietud.Value) / 1000;
            _vigilando = false;
            _inicioQuietud = null;
            return true;
        }

        public void Reiniciar()
        {
            _vigilando = false;
            _inicioQuietud = null;
            ImpactoG = 0;
            SegundosInmovil = 0;
        }
    }
}
