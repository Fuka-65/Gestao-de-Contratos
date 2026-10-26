using Contratos.Core.Entities;

namespace Contratos.Core.Interfaces;

public interface ICryptoService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherTextOrToken);
    string EncryptWithToken(string plainText);
    bool IsEncryptedToken(string? input);
    string HashPassword(string password);
    bool VerifyPassword(string password, string storedHash);
}

public interface ILdapService
{
    Task<(bool Sucesso, string Mensagem, int LatenciaMs)> TestarConexaoAsync(ConfiguracaoSistema config);
    Task<bool> AutenticarUsuarioAdAsync(string login, string senha, ConfiguracaoSistema config);
    Task<IReadOnlyList<ColaboradorAdDto>> SincronizarColaboradoresAsync(ConfiguracaoSistema config);
}

public record ColaboradorAdDto(
    string NomeCompleto,
    string Email,
    string Login,
    string Departamento,
    string Cargo
);

public interface IClmRepository
{
    // Usuários
    Task<IReadOnlyList<Usuario>> ObterUsuariosAsync();
    Task<Usuario?> ObterUsuarioPorIdAsync(int id);
    Task<Usuario?> ObterUsuarioPorLoginAsync(string loginOuEmail);
    Task<Usuario> SalvarUsuarioAsync(Usuario usuario);
    Task ExcluirUsuarioAsync(int id, string usuarioExecutor);

    // Fornecedores
    Task<IReadOnlyList<Fornecedor>> ObterFornecedoresAsync();
    Task<Fornecedor?> ObterFornecedorPorIdAsync(int id);
    Task<Fornecedor> SalvarFornecedorAsync(Fornecedor fornecedor);
    Task ExcluirFornecedorAsync(int id);

    // Contratos
    Task<IReadOnlyList<Contrato>> ObterContratosAsync();
    Task<Contrato?> ObterContratoPorIdAsync(int id);
    Task<Contrato> SalvarContratoAsync(Contrato contrato);
    Task ExcluirContratoAsync(int id);

    // Termos Aditivos
    Task<IReadOnlyList<TermoAditivo>> ObterAditivosPorContratoAsync(int contratoId);
    Task<TermoAditivo> AdicionarTermoAditivoAsync(TermoAditivo aditivo);

    // Faturas e Auditoria
    Task<IReadOnlyList<FaturaGasto>> ObterFaturasAsync();
    Task<FaturaGasto?> ObterFaturaPorIdAsync(int id);
    Task<FaturaGasto> SalvarFaturaAsync(FaturaGasto fatura);
    Task ExcluirFaturaAsync(int id);

    // Rateios por Departamento
    Task<IReadOnlyList<RateioDepartamento>> ObterRateiosAsync();
    Task<IReadOnlyList<RateioDepartamento>> ObterRateiosPorContratoAsync(int contratoId);
    Task<RateioDepartamento> SalvarRateioAsync(RateioDepartamento rateio);
    Task ExcluirRateioAsync(int id);

    // Configurações Globais & Logs
    Task<ConfiguracaoSistema> ObterConfiguracaoAsync();
    Task<ConfiguracaoSistema> SalvarConfiguracaoAsync(ConfiguracaoSistema config);
    Task RegistrarAuditoriaAsync(string usuario, string acao, string entidade, string detalhes, string? ip = null);
    Task<IReadOnlyList<LogAuditoria>> ObterLogsAuditoriaRecentesAsync(int quantidade = 100);
}
