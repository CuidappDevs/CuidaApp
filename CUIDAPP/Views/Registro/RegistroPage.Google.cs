namespace CUIDAPP.Views.Registro
{
    /// <summary>
    /// Registro desde "Continuar con Google": el login pasa el ID token, el correo y el nombre.
    /// El correo queda fijo (debe coincidir con el de Google) y no se pide contraseña; el resto
    /// del registro (rol, datos, documentos, test) es el mismo.
    /// </summary>
    public partial class RegistroPage : IQueryAttributable
    {
        private string? googleIdToken;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (!query.TryGetValue("GoogleIdToken", out var token) || token is not string t || string.IsNullOrWhiteSpace(t))
                return;

            googleIdToken = t;
            if (query.TryGetValue("GoogleEmail", out var e) && e is string email && email.Length > 0)
            {
                EntryEmail.Text = email;
                EntryEmail.IsReadOnly = true;
                EntryEmail.ClearButtonVisibility = ClearButtonVisibility.Never;
            }
            if (query.TryGetValue("GoogleNombre", out var n) && n is string nombre && nombre.Length > 0 && string.IsNullOrWhiteSpace(EntryNombre.Text))
                EntryNombre.Text = nombre;

            AvisoGoogle.IsVisible = true;
            BloqueClave.IsVisible = BloqueReglasClave.IsVisible = BloqueConfirmarClave.IsVisible = false;
        }
    }
}
