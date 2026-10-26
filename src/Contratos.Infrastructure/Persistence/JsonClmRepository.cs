using System.Text.Encodings.Web;
using System.Text.Json;
using Contratos.Core.Entities;
using Contratos.Core.Interfaces;

namespace Contratos.Infrastructure.Persistence;

/// <summary>
/// Repositório JSON Thread-Safe para desenvolvimento e execução imediata.
/// Utiliza ReaderWriterLockSlim e escrita atômica via arquivo temporário (.tmp) seguido de File.Move(..., overwrite: true).
/// </summary>
public class JsonClmRepository : IClmRepository
{
    private readonly string _jsonFilePath;
    private readonly ICryptoService _cryptoService;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public JsonClmRepository(string jsonFilePath, ICryptoService cryptoService)
    {
        _jsonFilePath = jsonFilePath ?? throw new ArgumentNullException(nameof(jsonFilePath));
        _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));

        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        _lock.EnterWriteLock();
        try
        {
            var dir = Path.GetDirectoryName(_jsonFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(_jsonFilePath))
            {
                var seed = SeedDataGenerator.CreateInitialSeed(_cryptoService);
                SaveSnapshotUnlocked(seed);
            }
            else
            {
                var snapshot = LoadSnapshotUnlocked();
                EnsureAdminMasterExists(snapshot);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void EnsureAdminMasterExists(ClmDatabaseSnapshot snapshot)
    {
        var admin = snapshot.Usuarios.FirstOrDefault(u => u.EhAdminImune());
        if (admin == null)
        {
            var nextId = snapshot.Usuarios.Count > 0 ? snapshot.Usuarios.Max(u => u.Id) + 1 : 1;
            snapshot.Usuarios.Insert(0, new Usuario
            {
                Id = nextId,
                NomeCompleto = "Administrador Master Corporativo",
                Email = Usuario.AdminMasterLogin,
                Login = Usuario.AdminMasterLogin,
                SenhaHash = _cryptoService.HashPassword(SeedDataGenerator.DefaultAdminPassword),
                Departamento = "Governança Corporativa & TI",
                Cargo = "Administrador Master",
                PapeisCsv = "1",
                Ativo = true,
                AutenticacaoAd = false
            });
            SaveSnapshotUnlocked(snapshot);
        }
        else
        {
            admin.GarantirIntegridadeAdmin();
        }
    }

    private ClmDatabaseSnapshot LoadSnapshotUnlocked()
    {
        if (!File.Exists(_jsonFilePath))
        {
            return SeedDataGenerator.CreateInitialSeed(_cryptoService);
        }

        var json = File.ReadAllText(_jsonFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return SeedDataGenerator.CreateInitialSeed(_cryptoService);
        }

        var snapshot = JsonSerializer.Deserialize<ClmDatabaseSnapshot>(json, JsonOptions)
                       ?? SeedDataGenerator.CreateInitialSeed(_cryptoService);

        HydrateRelationships(snapshot);
        return snapshot;
    }

    private void SaveSnapshotUnlocked(ClmDatabaseSnapshot snapshot)
    {
        var dir = Path.GetDirectoryName(_jsonFilePath) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempFile = Path.Combine(dir, $"{Path.GetFileName(_jsonFilePath)}.{Guid.NewGuid():N}.tmp");
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);

        File.WriteAllText(tempFile, json);
        File.Move(tempFile, _jsonFilePath, overwrite: true);
    }

    private static void HydrateRelationships(ClmDatabaseSnapshot snapshot)
    {
        var fornecedoresDict = snapshot.Fornecedores.ToDictionary(f => f.Id);
        var contratosDict = snapshot.Contratos.ToDictionary(c => c.Id);

        foreach (var f in snapshot.Fornecedores)
        {
            f.Contratos = new List<Contrato>();
            f.Faturas = new List<FaturaGasto>();
        }

        foreach (var c in snapshot.Contratos)
        {
            if (!c.ValorOpexMensal.HasValue && !c.ValorCapexMensal.HasValue)
            {
                if (c.Id == 1) { c.ValorOpexMensal = 41500m; c.ValorCapexMensal = 7000m; c.AliquotaImpostosPercentual = 14.25m; }
                else if (c.Id == 2) { c.ValorOpexMensal = 44000m; c.ValorCapexMensal = 18000m; c.AliquotaImpostosPercentual = 14.25m; }
                else if (c.Id == 3) { c.ValorOpexMensal = 26200m; c.ValorCapexMensal = 8000m; c.AliquotaImpostosPercentual = 16.33m; }
                else if (c.Id == 4) { c.ValorOpexMensal = 78000m; c.ValorCapexMensal = 0m; c.AliquotaImpostosPercentual = 18.65m; }
                else if (c.Id == 5) { c.ValorOpexMensal = 22500m; c.ValorCapexMensal = 0m; c.AliquotaImpostosPercentual = 11.15m; }
                else if (c.Id == 6) { c.ValorOpexMensal = 49000m; c.ValorCapexMensal = 5000m; c.AliquotaImpostosPercentual = 9.25m; }
                else if (c.Id == 7) { c.ValorOpexMensal = 12000m; c.ValorCapexMensal = 6000m; c.AliquotaImpostosPercentual = 14.25m; }
            }

            if (fornecedoresDict.TryGetValue(c.FornecedorId, out var forn))
            {
                c.Fornecedor = forn;
                forn.Contratos.Add(c);
            }
            c.TermosAditivos = snapshot.TermosAditivos
                .Where(a => a.ContratoId == c.Id)
                .OrderByDescending(a => a.DataAssinatura)
                .ToList();
            c.Rateios = snapshot.RateiosDepartamentos
                .Where(r => r.ContratoId == c.Id)
                .OrderByDescending(r => r.Percentual)
                .ToList();
            c.Faturas = new List<FaturaGasto>();
        }

        foreach (var fatura in snapshot.FaturasGastos)
        {
            if (contratosDict.TryGetValue(fatura.ContratoId, out var contrato))
            {
                fatura.Contrato = contrato;
                contrato.Faturas.Add(fatura);
            }
            if (fornecedoresDict.TryGetValue(fatura.FornecedorId, out var forn))
            {
                fatura.Fornecedor = forn;
                forn.Faturas.Add(fatura);
            }
        }
    }

    // ==================== USUÁRIOS ====================
    public Task<IReadOnlyList<Usuario>> ObterUsuariosAsync()
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult<IReadOnlyList<Usuario>>(
                snapshot.Usuarios.OrderBy(u => u.EhAdminImune() ? 0 : 1).ThenBy(u => u.NomeCompleto).ToList());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<Usuario?> ObterUsuarioPorIdAsync(int id)
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult(snapshot.Usuarios.FirstOrDefault(u => u.Id == id));
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<Usuario?> ObterUsuarioPorLoginAsync(string loginOuEmail)
    {
        _lock.EnterReadLock();
        try
        {
            if (string.IsNullOrWhiteSpace(loginOuEmail))
                return Task.FromResult<Usuario?>(null);

            var chave = loginOuEmail.Trim();
            var snapshot = LoadSnapshotUnlocked();

            if (string.Equals(chave, Usuario.AdminMasterLogin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(chave, Usuario.AdminShortLogin, StringComparison.OrdinalIgnoreCase))
            {
                var admin = snapshot.Usuarios.FirstOrDefault(u => u.EhAdminImune());
                admin?.GarantirIntegridadeAdmin();
                return Task.FromResult(admin);
            }

            var user = snapshot.Usuarios.FirstOrDefault(u =>
                string.Equals(u.Login, chave, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Email, chave, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(user);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<Usuario> SalvarUsuarioAsync(Usuario usuario)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            var existente = usuario.Id > 0
                ? snapshot.Usuarios.FirstOrDefault(u => u.Id == usuario.Id)
                : snapshot.Usuarios.FirstOrDefault(u =>
                    string.Equals(u.Login, usuario.Login, StringComparison.OrdinalIgnoreCase));

            if (existente != null && existente.EhAdminImune())
            {
                // Regra de Imunidade Perpétua do admin@admin:
                // Não permite inativar, alterar login, habilitar AD ou remover o papel Master. Apenas redefinição de senha é permitida.
                if (!usuario.Ativo)
                {
                    throw new InvalidOperationException("Operação bloqueada: É terminantemente proibido desativar o usuário admin@admin.");
                }

                if (!usuario.PossuiPapel(Contratos.Core.Enums.UserRole.Master))
                {
                    throw new InvalidOperationException("Operação bloqueada: É terminantemente proibido revogar o papel Master do usuário admin@admin.");
                }

                existente.SenhaHash = usuario.SenhaHash;
                existente.UltimoAcesso = usuario.UltimoAcesso ?? existente.UltimoAcesso;
                existente.GarantirIntegridadeAdmin();
                SaveSnapshotUnlocked(snapshot);
                return Task.FromResult(existente);
            }

            if (existente == null)
            {
                usuario.Id = snapshot.Usuarios.Count > 0 ? snapshot.Usuarios.Max(u => u.Id) + 1 : 1;
                usuario.DataCriacao = DateTime.UtcNow;
                if (usuario.EhAdminImune())
                {
                    usuario.GarantirIntegridadeAdmin();
                }
                snapshot.Usuarios.Add(usuario);
            }
            else
            {
                existente.NomeCompleto = usuario.NomeCompleto;
                existente.Email = usuario.Email;
                existente.Login = usuario.Login;
                if (!string.IsNullOrWhiteSpace(usuario.SenhaHash))
                {
                    existente.SenhaHash = usuario.SenhaHash;
                }
                existente.Departamento = usuario.Departamento;
                existente.Cargo = usuario.Cargo;
                existente.PapeisCsv = usuario.PapeisCsv;
                existente.Ativo = usuario.Ativo;
                existente.AutenticacaoAd = usuario.AutenticacaoAd;
                existente.UltimoAcesso = usuario.UltimoAcesso;
            }

            SaveSnapshotUnlocked(snapshot);
            return Task.FromResult(existente ?? usuario);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task ExcluirUsuarioAsync(int id, string usuarioExecutor)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            var alvo = snapshot.Usuarios.FirstOrDefault(u => u.Id == id);
            if (alvo == null)
                return Task.CompletedTask;

            if (alvo.EhAdminImune())
            {
                throw new InvalidOperationException("Operação bloqueada: O usuário admin@admin possui imunidade perpétua e não pode ser excluído.");
            }

            snapshot.Usuarios.Remove(alvo);
            SaveSnapshotUnlocked(snapshot);
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // ==================== FORNECEDORES ====================
    public Task<IReadOnlyList<Fornecedor>> ObterFornecedoresAsync()
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult<IReadOnlyList<Fornecedor>>(
                snapshot.Fornecedores.OrderBy(f => f.NomeFantasia).ToList());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<Fornecedor?> ObterFornecedorPorIdAsync(int id)
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult(snapshot.Fornecedores.FirstOrDefault(f => f.Id == id));
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<Fornecedor> SalvarFornecedorAsync(Fornecedor fornecedor)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            var existente = snapshot.Fornecedores.FirstOrDefault(f => f.Id == fornecedor.Id && fornecedor.Id > 0);
            if (existente == null)
            {
                fornecedor.Id = snapshot.Fornecedores.Count > 0 ? snapshot.Fornecedores.Max(f => f.Id) + 1 : 1;
                fornecedor.DataCadastro = DateTime.UtcNow;
                snapshot.Fornecedores.Add(fornecedor);
            }
            else
            {
                existente.RazaoSocial = fornecedor.RazaoSocial;
                existente.NomeFantasia = fornecedor.NomeFantasia;
                existente.Cnpj = fornecedor.Cnpj;
                existente.InscricaoEstadual = fornecedor.InscricaoEstadual;
                existente.InscricaoMunicipal = fornecedor.InscricaoMunicipal;
                existente.ContatoComercialNome = fornecedor.ContatoComercialNome;
                existente.ContatoComercialTelefone = fornecedor.ContatoComercialTelefone;
                existente.ContatoComercialEmail = fornecedor.ContatoComercialEmail;
                existente.ContatoFinanceiroNome = fornecedor.ContatoFinanceiroNome;
                existente.ContatoFinanceiroTelefone = fornecedor.ContatoFinanceiroTelefone;
                existente.ContatoFinanceiroEmail = fornecedor.ContatoFinanceiroEmail;
                existente.CentralSuporte = fornecedor.CentralSuporte;
                existente.SlaAtendimento = fornecedor.SlaAtendimento;
                existente.Site = fornecedor.Site;
                existente.LogoPath = fornecedor.LogoPath ?? existente.LogoPath;
                existente.Ativo = fornecedor.Ativo;
                existente.Observacoes = fornecedor.Observacoes;
            }

            SaveSnapshotUnlocked(snapshot);
            return Task.FromResult(existente ?? fornecedor);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task ExcluirFornecedorAsync(int id)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            if (snapshot.Contratos.Any(c => c.FornecedorId == id))
            {
                throw new InvalidOperationException("Não é possível excluir um fornecedor que possui contratos vinculados.");
            }

            snapshot.Fornecedores.RemoveAll(f => f.Id == id);
            SaveSnapshotUnlocked(snapshot);
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // ==================== CONTRATOS ====================
    public Task<IReadOnlyList<Contrato>> ObterContratosAsync()
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult<IReadOnlyList<Contrato>>(
                snapshot.Contratos.OrderBy(c => c.DataTerminoVigencia).ToList());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<Contrato?> ObterContratoPorIdAsync(int id)
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult(snapshot.Contratos.FirstOrDefault(c => c.Id == id));
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<Contrato> SalvarContratoAsync(Contrato contrato)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            var existente = snapshot.Contratos.FirstOrDefault(c => c.Id == contrato.Id && contrato.Id > 0);
            if (existente == null)
            {
                contrato.Id = snapshot.Contratos.Count > 0 ? snapshot.Contratos.Max(c => c.Id) + 1 : 1;
                contrato.DataCriacao = DateTime.UtcNow;
                snapshot.Contratos.Add(contrato);
            }
            else
            {
                existente.NumeroContrato = contrato.NumeroContrato;
                existente.TituloObjeto = contrato.TituloObjeto;
                existente.FornecedorId = contrato.FornecedorId;
                existente.CategoriaContrato = contrato.CategoriaContrato;
                existente.DataAssinatura = contrato.DataAssinatura;
                existente.DataInicioVigencia = contrato.DataInicioVigencia;
                existente.DataTerminoVigencia = contrato.DataTerminoVigencia;
                existente.ValorMensalContratado = contrato.ValorMensalContratado;
                existente.ValorGlobalContrato = contrato.ValorGlobalContrato;
                existente.ValorOpexMensal = contrato.ValorOpexMensal;
                existente.ValorCapexMensal = contrato.ValorCapexMensal;
                existente.AliquotaImpostosPercentual = contrato.AliquotaImpostosPercentual;
                existente.Moeda = contrato.Moeda;
                existente.IndiceReajuste = contrato.IndiceReajuste;
                existente.PeriodicidadeReajusteMeses = contrato.PeriodicidadeReajusteMeses;
                existente.RenovaAutomaticamente = contrato.RenovaAutomaticamente;
                existente.DiasAvisoPrevioRescisao = contrato.DiasAvisoPrevioRescisao;
                existente.DiasAlertaVencimento = contrato.DiasAlertaVencimento;
                existente.CentroDeCusto = contrato.CentroDeCusto;
                existente.DepartamentoResponsavel = contrato.DepartamentoResponsavel;
                existente.GestorContratoNome = contrato.GestorContratoNome;
                existente.GestorContratoEmail = contrato.GestorContratoEmail;
                existente.Status = contrato.Status;
                if (!string.IsNullOrWhiteSpace(contrato.ArquivoContratoPath))
                {
                    existente.ArquivoContratoPath = contrato.ArquivoContratoPath;
                }
                existente.Observacoes = contrato.Observacoes;
            }

            SaveSnapshotUnlocked(snapshot);
            return Task.FromResult(existente ?? contrato);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task ExcluirContratoAsync(int id)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            snapshot.TermosAditivos.RemoveAll(a => a.ContratoId == id);
            snapshot.RateiosDepartamentos.RemoveAll(r => r.ContratoId == id);
            snapshot.FaturasGastos.RemoveAll(f => f.ContratoId == id);
            snapshot.Contratos.RemoveAll(c => c.Id == id);
            SaveSnapshotUnlocked(snapshot);
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // ==================== TERMOS ADITIVOS ====================
    public Task<IReadOnlyList<TermoAditivo>> ObterAditivosPorContratoAsync(int contratoId)
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult<IReadOnlyList<TermoAditivo>>(
                snapshot.TermosAditivos
                    .Where(a => a.ContratoId == contratoId)
                    .OrderByDescending(a => a.DataAssinatura)
                    .ToList());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<TermoAditivo> AdicionarTermoAditivoAsync(TermoAditivo aditivo)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            aditivo.Id = snapshot.TermosAditivos.Count > 0 ? snapshot.TermosAditivos.Max(a => a.Id) + 1 : 1;
            aditivo.DataRegistro = DateTime.UtcNow;
            snapshot.TermosAditivos.Add(aditivo);

            // Atualiza automaticamente o contrato vigente caso o aditivo altere data de término ou valor mensal
            var contrato = snapshot.Contratos.FirstOrDefault(c => c.Id == aditivo.ContratoId);
            if (contrato != null)
            {
                if (aditivo.NovaDataTermino.HasValue)
                {
                    contrato.DataTerminoVigencia = aditivo.NovaDataTermino.Value;
                    if (contrato.Status == Contratos.Core.Enums.StatusContrato.EmRenovacao ||
                        contrato.Status == Contratos.Core.Enums.StatusContrato.Vencido)
                    {
                        contrato.Status = Contratos.Core.Enums.StatusContrato.Vigente;
                    }
                }
                if (aditivo.NovoValorMensal.HasValue && aditivo.NovoValorMensal.Value > 0)
                {
                    contrato.ValorMensalContratado = aditivo.NovoValorMensal.Value;
                    contrato.ValorGlobalContrato = aditivo.NovoValorMensal.Value * 12;
                }
            }

            SaveSnapshotUnlocked(snapshot);
            return Task.FromResult(aditivo);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // ==================== FATURAS & AUDITORIA ====================
    public Task<IReadOnlyList<FaturaGasto>> ObterFaturasAsync()
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult<IReadOnlyList<FaturaGasto>>(
                snapshot.FaturasGastos
                    .OrderByDescending(f => f.AnoReferencia)
                    .ThenByDescending(f => f.MesReferencia)
                    .ThenByDescending(f => f.ValorDesvio)
                    .ToList());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<FaturaGasto?> ObterFaturaPorIdAsync(int id)
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult(snapshot.FaturasGastos.FirstOrDefault(f => f.Id == id));
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<FaturaGasto> SalvarFaturaAsync(FaturaGasto fatura)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            var contrato = snapshot.Contratos.FirstOrDefault(c => c.Id == fatura.ContratoId);
            if (contrato != null)
            {
                fatura.FornecedorId = contrato.FornecedorId;
                if (fatura.ValorContratado <= 0)
                {
                    fatura.ValorContratado = contrato.ValorMensalContratado;
                }
            }

            var existente = snapshot.FaturasGastos.FirstOrDefault(f => f.Id == fatura.Id && fatura.Id > 0);
            if (existente == null)
            {
                fatura.Id = snapshot.FaturasGastos.Count > 0 ? snapshot.FaturasGastos.Max(f => f.Id) + 1 : 1;
                fatura.DataLancamento = DateTime.UtcNow;

                // Se houver desvio no lançamento e estiver pendente, sinaliza automaticamente em disputa caso não tenha sido explicitamente aprovado
                if (fatura.PossuiDesvio && fatura.Status == Contratos.Core.Enums.StatusFatura.Pendente)
                {
                    fatura.Status = Contratos.Core.Enums.StatusFatura.EmDisputaContestada;
                }

                snapshot.FaturasGastos.Add(fatura);
            }
            else
            {
                existente.ContratoId = fatura.ContratoId;
                existente.FornecedorId = fatura.FornecedorId;
                existente.MesReferencia = fatura.MesReferencia;
                existente.AnoReferencia = fatura.AnoReferencia;
                existente.ValorContratado = fatura.ValorContratado;
                existente.ValorCobrado = fatura.ValorCobrado;
                existente.ValorPago = fatura.ValorPago;
                existente.DataVencimento = fatura.DataVencimento;
                existente.DataPagamento = fatura.DataPagamento;
                existente.NumeroNotaFiscal = fatura.NumeroNotaFiscal;
                existente.CodigoBarrasLinhaDigitavel = fatura.CodigoBarrasLinhaDigitavel;
                if (!string.IsNullOrWhiteSpace(fatura.ArquivoFaturaPath))
                {
                    existente.ArquivoFaturaPath = fatura.ArquivoFaturaPath;
                }
                existente.Status = fatura.Status;
                existente.Observacoes = fatura.Observacoes;
            }

            SaveSnapshotUnlocked(snapshot);
            return Task.FromResult(existente ?? fatura);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task ExcluirFaturaAsync(int id)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            snapshot.FaturasGastos.RemoveAll(f => f.Id == id);
            SaveSnapshotUnlocked(snapshot);
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // ==================== RATEIOS POR DEPARTAMENTO ====================
    public Task<IReadOnlyList<RateioDepartamento>> ObterRateiosAsync()
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            var contratosDict = snapshot.Contratos.ToDictionary(c => c.Id);
            foreach (var r in snapshot.RateiosDepartamentos)
            {
                if (contratosDict.TryGetValue(r.ContratoId, out var c))
                {
                    r.Contrato = c;
                }
            }
            return Task.FromResult<IReadOnlyList<RateioDepartamento>>(
                snapshot.RateiosDepartamentos.OrderBy(r => r.ContratoId).ThenByDescending(r => r.Percentual).ToList());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<IReadOnlyList<RateioDepartamento>> ObterRateiosPorContratoAsync(int contratoId)
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult<IReadOnlyList<RateioDepartamento>>(
                snapshot.RateiosDepartamentos.Where(r => r.ContratoId == contratoId).OrderByDescending(r => r.Percentual).ToList());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<RateioDepartamento> SalvarRateioAsync(RateioDepartamento rateio)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            rateio.Id = snapshot.RateiosDepartamentos.Count > 0 ? snapshot.RateiosDepartamentos.Max(r => r.Id) + 1 : 1;
            snapshot.RateiosDepartamentos.Add(rateio);
            SaveSnapshotUnlocked(snapshot);
            return Task.FromResult(rateio);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task ExcluirRateioAsync(int id)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            snapshot.RateiosDepartamentos.RemoveAll(r => r.Id == id);
            SaveSnapshotUnlocked(snapshot);
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // ==================== CONFIGURAÇÕES & AUDITORIA ====================
    public Task<ConfiguracaoSistema> ObterConfiguracaoAsync()
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult(snapshot.Configuracao);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<ConfiguracaoSistema> SalvarConfiguracaoAsync(ConfiguracaoSistema config)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            if (!string.IsNullOrWhiteSpace(config.LdapSenhaLeituraEncrypted) &&
                !_cryptoService.IsEncryptedToken(config.LdapSenhaLeituraEncrypted))
            {
                config.LdapSenhaLeituraEncrypted = _cryptoService.EncryptWithToken(config.LdapSenhaLeituraEncrypted);
            }
            else if (string.IsNullOrWhiteSpace(config.LdapSenhaLeituraEncrypted))
            {
                config.LdapSenhaLeituraEncrypted = snapshot.Configuracao.LdapSenhaLeituraEncrypted;
            }

            if (string.IsNullOrWhiteSpace(config.LogoEmpresaPath))
            {
                config.LogoEmpresaPath = snapshot.Configuracao.LogoEmpresaPath;
            }

            config.UltimaAtualizacao = DateTime.UtcNow;
            snapshot.Configuracao = config;
            SaveSnapshotUnlocked(snapshot);
            return Task.FromResult(config);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task RegistrarAuditoriaAsync(string usuario, string acao, string entidade, string detalhes, string? ip = null)
    {
        _lock.EnterWriteLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            var nextId = snapshot.LogsAuditoria.Count > 0 ? snapshot.LogsAuditoria.Max(l => l.Id) + 1 : 1;
            snapshot.LogsAuditoria.Add(new LogAuditoria
            {
                Id = nextId,
                DataHora = DateTime.UtcNow,
                UsuarioLogin = string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario,
                Acao = acao,
                Entidade = entidade,
                Detalhes = detalhes,
                EnderecoIp = ip
            });
            SaveSnapshotUnlocked(snapshot);
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<IReadOnlyList<LogAuditoria>> ObterLogsAuditoriaRecentesAsync(int quantidade = 100)
    {
        _lock.EnterReadLock();
        try
        {
            var snapshot = LoadSnapshotUnlocked();
            return Task.FromResult<IReadOnlyList<LogAuditoria>>(
                snapshot.LogsAuditoria.OrderByDescending(l => l.DataHora).Take(quantidade).ToList());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
