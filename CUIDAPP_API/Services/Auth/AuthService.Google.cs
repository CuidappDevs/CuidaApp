using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.Auth;
namespace CUIDAPP_API.Services.Auth;

public partial class AuthService
{
    private async Task<GoogleIdentidad> ValidarGoogleAsync(string token)
    {
        var identidad = await _google.ValidarAsync(token);
        if (!identidad.EmailVerified)
            throw new GoogleAuthException("GOOGLE_CORREO_NO_VERIFICADO", 401, "Google no ha verificado este correo.");
        return identidad;
    }

    private async Task<GoogleIdentidad?> ValidarRegistroGoogleAsync(string? token, string email)
    {
        if (token == null) return null;
        var identidad = await ValidarGoogleAsync(token);
        if (!string.Equals(email, identidad.Email, StringComparison.OrdinalIgnoreCase))
            throw new GoogleAuthException("GOOGLE_CORREO_DISTINTO", 400, "El correo debe coincidir con el de Google.");
        return identidad;
    }

    public async Task<LoginResult> LoginGoogleAsync(string idToken)
    {
        var identidad = await ValidarGoogleAsync(idToken);
        var email = await ResolverGoogleAsync(identidad);
        if (email == null)
            throw new GoogleAuthException("REGISTRO_REQUERIDO", 404, "Completa el registro.", identidad);
        return await LoginCoreAsync(new LoginRequestDto { Email = email, Password = "" }, true);
    }

    protected virtual async Task<string?> ResolverGoogleAsync(GoogleIdentidad identidad)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("sp_GoogleResolver", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@GoogleSub", SqlDbType.NVarChar, 64).Value = identidad.Sub;
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = identidad.Email;
        await connection.OpenAsync();
        try { return await command.ExecuteScalarAsync() as string; }
        catch (SqlException ex) when (ex.Number is 51070 or 2601 or 2627) { throw GoogleEnlazado(); }
    }

    private static async Task ComprobarRegistroGoogleAsync(SqlConnection connection, SqlTransaction transaction, GoogleIdentidad identidad)
    {
        using var command = new SqlCommand("sp_GoogleRegistroValidar", connection, transaction) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@GoogleSub", SqlDbType.NVarChar, 64).Value = identidad.Sub;
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = identidad.Email;
        try { await command.ExecuteNonQueryAsync(); }
        catch (SqlException ex) when (ex.Number is 51070 or 2601 or 2627) { throw GoogleEnlazado(); }
    }

    private static GoogleAuthException GoogleEnlazado() =>
        new("GOOGLE_YA_ENLAZADO", 409, "La identidad de Google ya está enlazada a otra cuenta.");

    private static async Task EnlazarGoogleAsync(SqlConnection connection, SqlTransaction transaction, int id, string sub)
    {
        using var command = new SqlCommand("sp_GoogleEnlazar", connection, transaction) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@UsuarioId", id);
        command.Parameters.Add("@GoogleSub", SqlDbType.NVarChar, 64).Value = sub;
        try { await command.ExecuteNonQueryAsync(); }
        catch (SqlException ex) when (ex.Number is 51070 or 2601 or 2627) { throw GoogleEnlazado(); }
    }
}
