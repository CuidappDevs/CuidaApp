namespace CUIDAPP.Models.Auth
{
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        AccountSuspended,
        AccountInactive,
        /// <summary>La cuenta pidió eliminarse y está en el plazo de 30 días: se puede recuperar.</summary>
        PendingDeletion,
        /// <summary>Google: el correo no tiene cuenta en CuidaApp; hay que registrarse (datos en Google).</summary>
        GoogleRegistroRequerido,
        /// <summary>Google: el token no es válido o el correo no está verificado.</summary>
        GoogleInvalido,
        Validation,
        Network,
        ServerError
    }

    public class LoginResult
    {
        public LoginStatus Status { get; init; }
        public AuthResponse? Auth { get; init; }
        public SuspensionInfo? Suspension { get; init; }
        public DateTime? FechaEliminacion { get; init; }
        /// <summary>Datos de la cuenta de Google para prellenar el registro (GoogleRegistroRequerido).</summary>
        public string? GoogleEmail { get; init; }
        public string? GoogleNombre { get; init; }

        public static LoginResult Of(LoginStatus status, AuthResponse? auth = null, SuspensionInfo? suspension = null)
            => new() { Status = status, Auth = auth, Suspension = suspension };
    }
}
