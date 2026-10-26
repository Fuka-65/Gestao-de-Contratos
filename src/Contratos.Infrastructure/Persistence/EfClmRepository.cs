using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Contratos.Infrastructure.Persistence;

public class EfClmRepository : IClmRepository
{
    private readonly ClmDbContext _db;
    private readonly ICryptoService _cryptoService;

    public EfClmRepository(ClmDbContext db, ICryptoService cryptoService)
    {
        _db = db;
        _cryptoService = cryptoService;
    }

    public async Task<IReadOnlyList<Usuario>> ObterUsuariosAsync()
    {
        return await _db.Usuarios.AsNoTracking().OrderBy(u => u.NomeCompleto).ToListAsync();
    }

    public async Task<Usuario?> ObterUsuarioPorIdAsync(int id)
    {
        return await _db.Usuarios.FindAsync(id);
    }

    public async Task<Usuario?> ObterUsuarioPorLoginAsync(string loginOuEmail)
    {
        if (string.IsNullOrWhiteSpace(loginOuEmail))
            return null;

        var chave = loginOuEmail.Trim().ToLowerInvariant();
        if (chave == Usuario.AdminMasterLogin || chave == Usuario.AdminShortLogin)
        {
            var admin = await _db.Usuarios.FirstOrDefaultAsync(u =>
                u.Login.ToLower() == Usuario.AdminMasterLogin || u.Login.ToLower() == Usuario.AdminShortLogin);
            admin?.GarantirIntegridadeAdmin();
            return admin;
        }

        return await _db.Usuarios.FirstOrDefaultAsync(u =>
            u.Login.ToLower() == chave || u.Email.ToLower() == chave);
    }

    public async Task<Usuario> SalvarUsuarioAsync(Usuario usuario)
    {
        var existente = usuario.Id > 0
            ? await _db.Usuarios.FindAsync(usuario.Id)
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.Login == usuario.Login);

        if (existente != null && existente.EhAdminImune())
        {
            if (!usuario.Ativo)
                throw new InvalidOperationException("Operação bloqueada: É terminantemente proibido desativar o usuário admin@admin.");
            if (!usuario.PossuiPapel(UserRole.Master))
                throw new InvalidOperationException("Operação bloqueada: É terminantemente proibido revogar o papel Master do usuário admin@admin.");

            existente.SenhaHash = usuario.SenhaHash;
            existente.UltimoAcesso = usuario.UltimoAcesso ?? existente.UltimoAcesso;
            existente.GarantirIntegridadeAdmin();
            await _db.SaveChangesAsync();
            return existente;
        }

        if (existente == null)
        {
            _db.Usuarios.Add(usuario);
        }
        else
        {
            existente.NomeCompleto = usuario.NomeCompleto;
            existente.Email = usuario.Email;
            existente.Login = usuario.Login;
            if (!string.IsNullOrWhiteSpace(usuario.SenhaHash))
                existente.SenhaHash = usuario.SenhaHash;
            existente.Departamento = usuario.Departamento;
            existente.Cargo = usuario.Cargo;
            existente.PapeisCsv = usuario.PapeisCsv;
            existente.Ativo = usuario.Ativo;
            existente.AutenticacaoAd = usuario.AutenticacaoAd;
            existente.UltimoAcesso = usuario.UltimoAcesso;
        }

        await _db.SaveChangesAsync();
        return existente ?? usuario;
    }

    public async Task ExcluirUsuarioAsync(int id, string usuarioExecutor)
    {
        var alvo = await _db.Usuarios.FindAsync(id);
        if (alvo == null) return;

        if (alvo.EhAdminImune())
            throw new InvalidOperationException("Operação bloqueada: O usuário admin@admin possui imunidade perpétua e não pode ser excluído.");

        _db.Usuarios.Remove(alvo);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Fornecedor>> ObterFornecedoresAsync()
    {
        return await _db.Fornecedores
            .Include(f => f.Contratos)
            .OrderBy(f => f.NomeFantasia)
            .ToListAsync();
    }

    public async Task<Fornecedor?> ObterFornecedorPorIdAsync(int id)
    {
        return await _db.Fornecedores
            .Include(f => f.Contratos)
            .Include(f => f.Faturas)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Fornecedor> SalvarFornecedorAsync(Fornecedor fornecedor)
    {
        if (fornecedor.Id == 0)
            _db.Fornecedores.Add(fornecedor);
        else
            _db.Fornecedores.Update(fornecedor);

        await _db.SaveChangesAsync();
        return fornecedor;
    }

    public async Task ExcluirFornecedorAsync(int id)
    {
        if (await _db.Contratos.AnyAsync(c => c.FornecedorId == id))
            throw new InvalidOperationException("Não é possível excluir um fornecedor que possui contratos vinculados.");

        var forn = await _db.Fornecedores.FindAsync(id);
        if (forn != null)
        {
            _db.Fornecedores.Remove(forn);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<Contrato>> ObterContratosAsync()
    {
        return await _db.Contratos
            .Include(c => c.Fornecedor)
            .Include(c => c.TermosAditivos)
            .Include(c => c.Rateios)
            .OrderBy(c => c.DataTerminoVigencia)
            .ToListAsync();
    }

    public async Task<Contrato?> ObterContratoPorIdAsync(int id)
    {
        return await _db.Contratos
            .Include(c => c.Fornecedor)
            .Include(c => c.TermosAditivos)
            .Include(c => c.Faturas)
            .Include(c => c.Rateios)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Contrato> SalvarContratoAsync(Contrato contrato)
    {
        if (contrato.Id == 0)
            _db.Contratos.Add(contrato);
        else
            _db.Contratos.Update(contrato);

        await _db.SaveChangesAsync();
        return contrato;
    }

    public async Task ExcluirContratoAsync(int id)
    {
        var c = await _db.Contratos.FindAsync(id);
        if (c != null)
        {
            _db.Contratos.Remove(c);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<TermoAditivo>> ObterAditivosPorContratoAsync(int contratoId)
    {
        return await _db.TermosAditivos
            .Where(a => a.ContratoId == contratoId)
            .OrderByDescending(a => a.DataAssinatura)
            .ToListAsync();
    }

    public async Task<TermoAditivo> AdicionarTermoAditivoAsync(TermoAditivo aditivo)
    {
        _db.TermosAditivos.Add(aditivo);
        var contrato = await _db.Contratos.FindAsync(aditivo.ContratoId);
        if (contrato != null)
        {
            if (aditivo.NovaDataTermino.HasValue)
            {
                contrato.DataTerminoVigencia = aditivo.NovaDataTermino.Value;
                contrato.Status = StatusContrato.Vigente;
            }
            if (aditivo.NovoValorMensal.HasValue && aditivo.NovoValorMensal.Value > 0)
            {
                contrato.ValorMensalContratado = aditivo.NovoValorMensal.Value;
            }
        }
        await _db.SaveChangesAsync();
        return aditivo;
    }

    public async Task<IReadOnlyList<FaturaGasto>> ObterFaturasAsync()
    {
        return await _db.FaturasGastos
            .Include(f => f.Contrato)
            .Include(f => f.Fornecedor)
            .OrderByDescending(f => f.AnoReferencia)
            .ThenByDescending(f => f.MesReferencia)
            .ToListAsync();
    }

    public async Task<FaturaGasto?> ObterFaturaPorIdAsync(int id)
    {
        return await _db.FaturasGastos
            .Include(f => f.Contrato)
            .Include(f => f.Fornecedor)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<FaturaGasto> SalvarFaturaAsync(FaturaGasto fatura)
    {
        if (fatura.Id == 0)
        {
            if (fatura.PossuiDesvio && fatura.Status == StatusFatura.Pendente)
                fatura.Status = StatusFatura.EmDisputaContestada;

            _db.FaturasGastos.Add(fatura);
        }
        else
        {
            _db.FaturasGastos.Update(fatura);
        }

        await _db.SaveChangesAsync();
        return fatura;
    }

    public async Task ExcluirFaturaAsync(int id)
    {
        var fat = await _db.FaturasGastos.FindAsync(id);
        if (fat != null)
        {
            _db.FaturasGastos.Remove(fat);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<RateioDepartamento>> ObterRateiosAsync()
    {
        return await _db.RateiosDepartamentos
            .Include(r => r.Contrato)
            .OrderBy(r => r.ContratoId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<RateioDepartamento>> ObterRateiosPorContratoAsync(int contratoId)
    {
        return await _db.RateiosDepartamentos
            .Where(r => r.ContratoId == contratoId)
            .OrderByDescending(r => r.Percentual)
            .ToListAsync();
    }

    public async Task<RateioDepartamento> SalvarRateioAsync(RateioDepartamento rateio)
    {
        _db.RateiosDepartamentos.Add(rateio);
        await _db.SaveChangesAsync();
        return rateio;
    }

    public async Task ExcluirRateioAsync(int id)
    {
        var r = await _db.RateiosDepartamentos.FindAsync(id);
        if (r != null)
        {
            _db.RateiosDepartamentos.Remove(r);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<ConfiguracaoSistema> ObterConfiguracaoAsync()
    {
        var cfg = await _db.ConfiguracoesSistema.FirstOrDefaultAsync();
        if (cfg == null)
        {
            cfg = new ConfiguracaoSistema();
            _db.ConfiguracoesSistema.Add(cfg);
            await _db.SaveChangesAsync();
        }
        return cfg;
    }

    public async Task<ConfiguracaoSistema> SalvarConfiguracaoAsync(ConfiguracaoSistema config)
    {
        if (!string.IsNullOrWhiteSpace(config.LdapSenhaLeituraEncrypted) &&
            !_cryptoService.IsEncryptedToken(config.LdapSenhaLeituraEncrypted))
        {
            config.LdapSenhaLeituraEncrypted = _cryptoService.EncryptWithToken(config.LdapSenhaLeituraEncrypted);
        }

        config.UltimaAtualizacao = DateTime.UtcNow;
        _db.ConfiguracoesSistema.Update(config);
        await _db.SaveChangesAsync();
        return config;
    }

    public async Task RegistrarAuditoriaAsync(string usuario, string acao, string entidade, string detalhes, string? ip = null)
    {
        _db.LogsAuditoria.Add(new LogAuditoria
        {
            DataHora = DateTime.UtcNow,
            UsuarioLogin = usuario,
            Acao = acao,
            Entidade = entidade,
            Detalhes = detalhes,
            EnderecoIp = ip
        });
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<LogAuditoria>> ObterLogsAuditoriaRecentesAsync(int quantidade = 100)
    {
        return await _db.LogsAuditoria
            .OrderByDescending(l => l.DataHora)
            .Take(quantidade)
            .ToListAsync();
    }
}
