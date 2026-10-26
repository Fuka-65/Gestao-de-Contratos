using System.Security.Cryptography;
using Contratos.Infrastructure.Security;
using Xunit;

namespace Contratos.Tests;

public class CryptoTests
{
    [Fact]
    public void Aes256_DeveEncriptarEDecriptarComSuporteATokenEnc()
    {
        var crypto = new AesCryptoService();
        const string segredoOriginal = "Server=sql.corp.local;Database=ContratosProd;User Id=sa;Password=Sup3rS3cr3t#2026!;Encrypt=True;";

        var token = crypto.EncryptWithToken(segredoOriginal);

        Assert.True(crypto.IsEncryptedToken(token));
        Assert.StartsWith("{ENC:", token);
        Assert.EndsWith("}", token);
        Assert.NotEqual(segredoOriginal, token);

        var decriptado = crypto.Decrypt(token);
        Assert.Equal(segredoOriginal, decriptado);
    }

    [Fact]
    public void Aes256_IvDinamico_DeveGerarCifrasDiferentesParaMesmoTextoPlano()
    {
        var crypto = new AesCryptoService();
        const string texto = "MesmaSenhaSecreta123!";

        var token1 = crypto.EncryptWithToken(texto);
        var token2 = crypto.EncryptWithToken(texto);

        Assert.NotEqual(token1, token2);
        Assert.Equal(texto, crypto.Decrypt(token1));
        Assert.Equal(texto, crypto.Decrypt(token2));
    }

    [Fact]
    public void Aes256_HmacSha256_DeveDetectarAdulteracaoDoToken()
    {
        var crypto = new AesCryptoService();
        var cifrado = crypto.Encrypt("DadosCriticosDeBanco");
        var bytes = Convert.FromBase64String(cifrado);

        // Adultera 1 byte no meio do ciphertext
        bytes[20] ^= 0xFF;
        var tokenAdulterado = $"{{ENC:{Convert.ToBase64String(bytes)}}}";

        Assert.Throws<CryptographicException>(() => crypto.Decrypt(tokenAdulterado));
    }

    [Fact]
    public void ResolveConnectionStringTokens_DeveDecriptarSenhaEmbutidaNaConnectionString()
    {
        var crypto = new AesCryptoService();
        var senhaToken = crypto.EncryptWithToken("SqlP@ssw0rd2026!");
        var connStr = $"Server=10.0.0.5;Database=ContratosDB;User Id=clm_app;Password={senhaToken};TrustServerCertificate=True;";

        var resolvida = crypto.ResolveConnectionStringTokens(connStr);

        Assert.Equal("Server=10.0.0.5;Database=ContratosDB;User Id=clm_app;Password=SqlP@ssw0rd2026!;TrustServerCertificate=True;", resolvida);
    }
}
