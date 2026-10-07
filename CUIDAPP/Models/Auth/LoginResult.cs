namespace CUIDAPP.Models.Auth
{
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        AccountSuspended,
        AccountInactive,
        Validation,
        Network,
        ServerError
    }

    public class LoginResult
    {
        public LoginStatus Status { get; init; }
        public AuthResponse? Auth { get; init; }
        public SuspensionInfo? Suspension { get; init; }

        public static LoginResult Of(LoginStatus status, AuthResponse? auth = null, SuspensionInfo? suspension = null)
            => new() { Status = status, Auth = auth, Suspension = suspension };
    }
}
