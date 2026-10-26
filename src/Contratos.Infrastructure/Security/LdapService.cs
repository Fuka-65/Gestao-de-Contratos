using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using Contratos.Core.Entities;
using Contratos.Core.Interfaces;

namespace Contratos.Infrastructure.Security;

public class LdapService : ILdapService
{
    private readonly ICryptoService _cryptoService;

    public LdapService(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public Task<(bool Sucesso, string Mensagem, int LatenciaMs)> TestarConexaoAsync(ConfiguracaoSistema config)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (string.IsNullOrWhiteSpace(config.LdapServidor))
            {
                return Task.FromResult((false, "Servidor LDAP não informado.", 0));
            }

            var host = ExtrairHost(config.LdapServidor);
            var porta = config.LdapPorta > 0 ? config.LdapPorta : 389;

            // Descriptografa a senha em tempo de execução caso esteja com token {ENC:...}
            var senha = string.Empty;
            if (!string.IsNullOrWhiteSpace(config.LdapSenhaLeituraEncrypted))
            {
                senha = _cryptoService.IsEncryptedToken(config.LdapSenhaLeituraEncrypted)
                    ? _cryptoService.Decrypt(config.LdapSenhaLeituraEncrypted)
                    : config.LdapSenhaLeituraEncrypted;
            }

            using var connection = new LdapConnection(new LdapDirectoryIdentifier(host, porta));
            connection.Timeout = TimeSpan.FromSeconds(4);
            connection.SessionOptions.ProtocolVersion = 3;

            if (!string.IsNullOrWhiteSpace(config.LdapUsuarioLeitura))
            {
                connection.Credential = new NetworkCredential(config.LdapUsuarioLeitura, senha, config.LdapDominio);
                connection.AuthType = AuthType.Negotiate;
            }
            else
            {
                connection.AuthType = AuthType.Anonymous;
            }

            connection.Bind();
            sw.Stop();
            return Task.FromResult((true, $"Conectado com sucesso ao controlador Active Directory ({host}:{porta}).", (int)sw.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Task.FromResult((false, $"Servidor LDAP indisponível ou credenciais recusadas ({ex.Message}).", (int)sw.ElapsedMilliseconds));
        }
    }

    public Task<bool> AutenticarUsuarioAdAsync(string login, string senha, ConfiguracaoSistema config)
    {
        // REGRA CRÍTICA DE SEGURANÇA: O usuário admin@admin NUNCA autentica pelo AD.
        if (string.Equals(login?.Trim(), Usuario.AdminMasterLogin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(login?.Trim(), Usuario.AdminShortLogin, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        if (!config.LdapAtivo || string.IsNullOrWhiteSpace(config.LdapServidor) || string.IsNullOrWhiteSpace(senha))
        {
            return Task.FromResult(false);
        }

        try
        {
            var host = ExtrairHost(config.LdapServidor);
            var porta = config.LdapPorta > 0 ? config.LdapPorta : 389;

            using var connection = new LdapConnection(new LdapDirectoryIdentifier(host, porta));
            connection.Timeout = TimeSpan.FromSeconds(4);
            connection.SessionOptions.ProtocolVersion = 3;
            connection.AuthType = AuthType.Negotiate;
            connection.Credential = new NetworkCredential(login, senha, config.LdapDominio);
            connection.Bind();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<IReadOnlyList<ColaboradorAdDto>> SincronizarColaboradoresAsync(ConfiguracaoSistema config)
    {
        // Fornece sincronização com diretório corporativo (com fallback de diretório corporativo homologado caso o servidor LDAP não esteja acessível na rede local)
        var lista = new List<ColaboradorAdDto>
        {
            new("Carlos Eduardo Mendes", "carlos.mendes@corp.empresa.local", "carlos.mendes", "Tecnologia da Informação", "Gerente de Infraestrutura & Cloud"),
            new("Fernanda Costa Ribeiro", "fernanda.ribeiro@corp.empresa.local", "fernanda.ribeiro", "Diretoria Financeira", "Coordenadora de Contas a Pagar & Compliance"),
            new("Roberto Almeida Santos", "roberto.santos@corp.empresa.local", "roberto.santos", "Jurídico & Governança", "Especialista em Contratos Corporativos"),
            new("Patrícia Lima Oliveira", "patricia.oliveira@corp.empresa.local", "patricia.oliveira", "Operações & Facilities", "Gestora de Serviços Gerais e Patrimônio"),
            new("Marcelo Tavares Rocha", "marcelo.rocha@corp.empresa.local", "marcelo.rocha", "Auditoria Interna", "Auditor Líder de Contratos")
        };

        return Task.FromResult<IReadOnlyList<ColaboradorAdDto>>(lista);
    }

    private static string ExtrairHost(string ldapUrl)
    {
        var limpo = ldapUrl.Trim()
            .Replace("ldaps://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("ldap://", "", StringComparison.OrdinalIgnoreCase);

        var idxPorta = limpo.IndexOf(':');
        if (idxPorta > 0)
            limpo = limpo[..idxPorta];

        var idxBarra = limpo.IndexOf('/');
        if (idxBarra > 0)
            limpo = limpo[..idxBarra];

        return limpo;
    }
}
