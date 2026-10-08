namespace CUIDAPP_ADMINISTRATIVO.Services
{
    /// <summary>
    /// Diálogo de confirmación del panel (lo pinta Components/Shared/DialogoConfirmacion en el layout).
    /// Toda acción destructiva o que afecta a un usuario pasa por aquí antes de llamar a la API.
    /// </summary>
    public class ConfirmacionService
    {
        public record Solicitud(string Titulo, string Mensaje, string Confirmar, bool Peligro, string? EtiquetaTexto, int MinimoTexto);

        private TaskCompletionSource<(bool Ok, string? Texto)>? _tcs;

        public Solicitud? Actual { get; private set; }
        public event Action? Cambio;

        /// <summary>Pregunta sí/no.</summary>
        public async Task<bool> PreguntarAsync(string titulo, string mensaje, string confirmar = "Confirmar", bool peligro = false)
            => (await Abrir(new Solicitud(titulo, mensaje, confirmar, peligro, null, 0))).Ok;

        /// <summary>Pide un texto obligatorio (motivo). Devuelve null si cancela.</summary>
        public async Task<string?> PedirMotivoAsync(string titulo, string mensaje, string confirmar, string etiqueta = "Motivo", bool peligro = true, int minimo = 5)
        {
            var (ok, texto) = await Abrir(new Solicitud(titulo, mensaje, confirmar, peligro, etiqueta, minimo));
            return ok ? texto?.Trim() : null;
        }

        private Task<(bool Ok, string? Texto)> Abrir(Solicitud s)
        {
            _tcs?.TrySetResult((false, null));
            _tcs = new TaskCompletionSource<(bool Ok, string? Texto)>();
            Actual = s;
            Cambio?.Invoke();
            return _tcs.Task;
        }

        public void Responder(bool ok, string? texto = null)
        {
            Actual = null;
            Cambio?.Invoke();
            _tcs?.TrySetResult((ok, texto));
        }
    }

    /// <summary>Mensajes breves de resultado (abajo a la derecha). Se ocultan solos.</summary>
    public class AvisoToastService
    {
        public record Toast(Guid Id, string Texto, bool Ok);

        private readonly List<Toast> _lista = new();
        public IReadOnlyList<Toast> Lista => _lista;
        public event Action? Cambio;

        public void Mostrar(string texto, bool ok = true)
        {
            var t = new Toast(Guid.NewGuid(), texto, ok);
            lock (_lista) _lista.Add(t);
            Cambio?.Invoke();
            _ = Task.Delay(ok ? 3200 : 5500).ContinueWith(_ => Quitar(t.Id));
        }

        public void Mostrar(ResultadoAccion r) => Mostrar(r.Mensaje, r.Ok);

        public void Quitar(Guid id)
        {
            lock (_lista) _lista.RemoveAll(x => x.Id == id);
            Cambio?.Invoke();
        }
    }
}
