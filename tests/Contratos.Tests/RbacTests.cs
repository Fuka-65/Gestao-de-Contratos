using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Contratos.Infrastructure.Persistence;
using Contratos.Infrastructure.Security;
using Xunit;

namespace Contratos.Tests;

public class RbacTests
{
    [Fact]
    public void RegraAcumuloDePapeis_DevePrevalecerPapelMaisPermissivo()
    {
        // Arrange: Usuário com Financeiro (2) + GestorContratos (3) + Auditor (4)
        var usuario = new Usuario
        {
            Id = 10,
            NomeCompleto = "Analista Sênior",
            Login = "analista.senior",
            Email = "analista@empresa.com.br"
        };

        usuario.DefinirPapeis(new[] { UserRole.Auditor, UserRole.GestorContratos, UserRole.Financeiro });

        // Act
        var papelMaisPermissivo = usuario.ObterPapelMaisPermissivo();

        // Assert
        Assert.Equal(UserRole.Financeiro, papelMaisPermissivo);
        Assert.True(usuario.PodeGerenciarFinanceiro());
        Assert.True(usuario.PodeGerenciarContratos());
        Assert.False(usuario.PodeAcessarAdministracaoSistema());
    }

    [Fact]
    public void RegraAcumuloDePapeis_MasterPrevaleceSobreQualquerCombinacao()
    {
        var usuario = new Usuario
        {
            Id = 11,
            NomeCompleto = "Diretor",
            Login = "diretor.corporativo",
            Email = "diretor@empresa.com.br"
        };

        usuario.DefinirPapeis(new[] { UserRole.Auditor, UserRole.Master });

        Assert.Equal(UserRole.Master, usuario.ObterPapelMaisPermissivo());
        Assert.True(usuario.PodeAcessarAdministracaoSistema());
        Assert.True(usuario.PodeGerenciarFinanceiro());
        Assert.True(usuario.PodeGerenciarContratos());
    }

    [Fact]
    public async Task ImunidadeAdminMaster_DeveImpedirExclusaoInativacaoERevogacaoDePapel()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"clm_rbac_test_{Guid.NewGuid():N}.json");
        try
        {
            var crypto = new AesCryptoService();
            var repo = new JsonClmRepository(tempFile, crypto);

            var admin = await repo.ObterUsuarioPorLoginAsync("admin@admin");
            Assert.NotNull(admin);
            Assert.True(admin!.EhAdminImune());
            Assert.Equal(UserRole.Master, admin.ObterPapelMaisPermissivo());

            // 1. Proibido excluir admin@admin
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await repo.ExcluirUsuarioAsync(admin.Id, "admin@admin");
            });

            // 2. Proibido desativar admin@admin
            var tentativaInativar = new Usuario
            {
                Id = admin.Id,
                Login = "admin@admin",
                Email = "admin@admin",
                NomeCompleto = admin.NomeCompleto,
                SenhaHash = admin.SenhaHash,
                PapeisCsv = "1",
                Ativo = false
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await repo.SalvarUsuarioAsync(tentativaInativar);
            });

            // 3. Proibido revogar papel Master do admin@admin
            Assert.Throws<InvalidOperationException>(() =>
            {
                admin.DefinirPapeis(new[] { UserRole.GestorContratos });
            });

            // 4. Permitida redefinição de senha do admin@admin
            var novoHash = crypto.HashPassword("NovaSenhaAdmin@2026!");
            admin.SenhaHash = novoHash;
            var salvo = await repo.SalvarUsuarioAsync(admin);
            Assert.True(crypto.VerifyPassword("NovaSenhaAdmin@2026!", salvo.SenhaHash));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PrevalenciaAdminMaster_DeveAutenticarPeloHashLocalMesmoComActiveDirectoryAtivo()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"clm_ad_test_{Guid.NewGuid():N}.json");
        try
        {
            var crypto = new AesCryptoService();
            var repo = new JsonClmRepository(tempFile, crypto);

            // Ativa o LDAP com um servidor fictício inacessível
            var config = await repo.ObterConfiguracaoAsync();
            config.LdapAtivo = true;
            config.LdapServidor = "ldap://servidor-ad-inexistente.local";
            await repo.SalvarConfiguracaoAsync(config);

            var ldap = new LdapService(crypto);
            var authService = new AuthenticationService(repo, crypto, ldap);

            // Act: admin@admin deve autenticar via hash local ignorando o AD
            var resultado = await authService.AutenticarAsync("admin@admin", SeedDataGenerator.DefaultAdminPassword);

            // Assert
            Assert.True(resultado.Sucesso);
            Assert.Equal("LocalAdminMaster", resultado.OrigemAutenticacao);
            Assert.NotNull(resultado.Usuario);
            Assert.True(resultado.Usuario!.EhAdminImune());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
