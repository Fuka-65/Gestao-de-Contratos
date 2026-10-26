using Contratos.Core.Entities;
using Contratos.Core.Interfaces;

namespace Contratos.Infrastructure.Security;

/// <summary>
/// Coordenador de autenticação corporativa que garante a prevalência perpétua e obrigatória
/// do usuário admin@admin sobre o Active Directory (LDAP).
/// </summary>
public class AuthenticationService
{
    private readonly IClmRepository _repository;
    private readonly ICryptoService _cryptoService;
    private readonly ILdapService _ldapService;

    public AuthenticationService(
        IClmRepository repository,
        ICryptoService cryptoService,
        ILdapService ldapService)
    {
        _repository = repository;
        _cryptoService = cryptoService;
        _ldapService = ldapService;
    }

    public async Task<(bool Sucesso, Usuario? Usuario, string OrigemAutenticacao, string MensagemErro)> AutenticarAsync(
        string loginOuEmail,
        string senha)
    {
        if (string.IsNullOrWhiteSpace(loginOuEmail) || string.IsNullOrWhiteSpace(senha))
        {
            return (false, null, "Nenhuma", "Informe o usuário e a senha.");
        }

        var usuario = await _repository.ObterUsuarioPorLoginAsync(loginOuEmail.Trim());
        if (usuario == null)
        {
            return (false, null, "Nenhuma", "Credenciais inválidas ou usuário não cadastrado.");
        }

        // REGRA CRÍTICA: Imunidade e prevalência perpétua do admin@admin
        if (usuario.EhAdminImune())
        {
            usuario.GarantirIntegridadeAdmin();
            var senhaValidaLocal = _cryptoService.VerifyPassword(senha, usuario.SenhaHash);
            if (!senhaValidaLocal)
            {
                return (false, null, "LocalAdminMaster", "Senha incorreta para o administrador Master.");
            }

            usuario.UltimoAcesso = DateTime.UtcNow;
            await _repository.SalvarUsuarioAsync(usuario);
            return (true, usuario, "LocalAdminMaster", string.Empty);
        }

        if (!usuario.Ativo)
        {
            return (false, null, "Bloqueado", "Esta conta corporativa encontra-se desativada. Contate o administrador Master.");
        }

        var config = await _repository.ObterConfiguracaoAsync();

        // Se o Active Directory estiver habilitado e o usuário estiver marcado para AD, tenta AD primeiro (exceto admin@admin já tratado acima)
        if (config.LdapAtivo && usuario.AutenticacaoAd)
        {
            var autenticadoAd = await _ldapService.AutenticarUsuarioAdAsync(usuario.Login, senha, config);
            if (autenticadoAd)
            {
                usuario.UltimoAcesso = DateTime.UtcNow;
                await _repository.SalvarUsuarioAsync(usuario);
                return (true, usuario, "ActiveDirectory", string.Empty);
            }
        }

        // Autenticação pelo Hash PBKDF2 local
        if (_cryptoService.VerifyPassword(senha, usuario.SenhaHash))
        {
            usuario.UltimoAcesso = DateTime.UtcNow;
            await _repository.SalvarUsuarioAsync(usuario);
            return (true, usuario, "LocalHash", string.Empty);
        }

        return (false, null, "Falha", "Usuário ou senha inválidos.");
    }
}
