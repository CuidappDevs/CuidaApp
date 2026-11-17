using System.Security.Cryptography;
using CUIDAPP_API.Services.Cuenta;

namespace CUIDAPP_API.Tests;

public class BovedaCifradoTests
{
    [Fact]
    public void Cifra_y_descifra_con_nonce_distinto_por_archivo()
    {
        var clave=RandomNumberGenerator.GetBytes(32);
        var pdf="%PDF-1.7 contrato firmado con datos de prueba"u8.ToArray();
        var a=BovedaCifrado.Cifrar(pdf,clave);
        var b=BovedaCifrado.Cifrar(pdf,clave);
        Assert.Equal(pdf,BovedaCifrado.Descifrar(a,clave));
        Assert.Equal(pdf,BovedaCifrado.Descifrar(b,clave));
        Assert.False(a.SequenceEqual(b));
        BovedaCifrado.VerificarHash(pdf,Convert.ToHexString(SHA256.HashData(pdf)));
    }
    [Fact]
    public void Tag_alterado_falla()
    {
        var clave=RandomNumberGenerator.GetBytes(32);
        var cifrado=BovedaCifrado.Cifrar("%PDF prueba"u8.ToArray(),clave);
        cifrado[12]^=1;
        Assert.ThrowsAny<CryptographicException>(()=>BovedaCifrado.Descifrar(cifrado,clave));
    }
    [Fact]
    public void Clave_incorrecta_falla()
    {
        var cifrado=BovedaCifrado.Cifrar("%PDF prueba"u8.ToArray(),RandomNumberGenerator.GetBytes(32));
        Assert.ThrowsAny<CryptographicException>(()=>BovedaCifrado.Descifrar(cifrado,RandomNumberGenerator.GetBytes(32)));
    }
    [Fact]
    public void Hash_incorrecto_falla() => Assert.Throws<CryptographicException>(()=>
        BovedaCifrado.VerificarHash("%PDF prueba"u8.ToArray(),new string('0',64)));
}
