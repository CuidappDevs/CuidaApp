using System.Security.Cryptography;

namespace CUIDAPP_API.Services.Cuenta;

/// <summary>Formato v1: nonce (12 bytes), tag (16 bytes), PDF cifrado. Clave AES de 32 bytes.</summary>
public static class BovedaCifrado
{
    public static byte[] Cifrar(byte[] pdf, byte[] clave)
    {
        ValidarClave(clave);
        var salida = new byte[28 + pdf.Length];
        RandomNumberGenerator.Fill(salida.AsSpan(0,12));
        using var aes = new AesGcm(clave,16);
        aes.Encrypt(salida.AsSpan(0,12),pdf,salida.AsSpan(28),salida.AsSpan(12,16));
        return salida;
    }
    public static byte[] Descifrar(byte[] archivo, byte[] clave)
    {
        ValidarClave(clave);
        if(archivo.Length<28) throw new CryptographicException("Archivo de bóveda inválido.");
        var pdf = new byte[archivo.Length-28];
        using var aes = new AesGcm(clave,16);
        aes.Decrypt(archivo.AsSpan(0,12),archivo.AsSpan(28),archivo.AsSpan(12,16),pdf);
        return pdf;
    }
    private static void ValidarClave(byte[] clave)
    {
        if(clave.Length!=32) throw new CryptographicException("La clave de la bóveda debe tener 32 bytes.");
    }
    public static void VerificarHash(byte[] pdf,string hash)
    {
        if(!string.Equals(Convert.ToHexString(SHA256.HashData(pdf)),hash,StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("La integridad del documento no coincide.");
    }
}
