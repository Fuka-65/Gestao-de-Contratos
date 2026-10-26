using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Contratos.Core.Enums;

namespace Contratos.Core.Entities;

public class Usuario
{
    public const string AdminMasterLogin = "admin@admin";
    public const string AdminShortLogin = "admin";

    public int Id { get; set; }

    [Required(ErrorMessage = "O nome completo é obrigatório.")]
    [StringLength(150)]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "O login é obrigatório.")]
    [StringLength(100)]
    public string Login { get; set; } = string.Empty;

    [Required]
    public string SenhaHash { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Departamento { get; set; }

    [StringLength(100)]
    public string? Cargo { get; set; }

    /// <summary>
    /// Armazenamento em banco dos papéis acumulados separados por vírgula (ex: "2,3").
    /// </summary>
    public string PapeisCsv { get; set; } = ((int)UserRole.GestorContratos).ToString();

    public bool Ativo { get; set; } = true;

    public bool AutenticacaoAd { get; set; } = false;

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public DateTime? UltimoAcesso { get; set; }

    [NotMapped]
    [JsonIgnore]
    public IReadOnlyList<UserRole> Papeis
    {
        get
        {
            if (EhAdminImune())
            {
                return new[] { UserRole.Master };
            }

            if (string.IsNullOrWhiteSpace(PapeisCsv))
            {
                return new[] { UserRole.Auditor };
            }

            var roles = PapeisCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var val) && Enum.IsDefined(typeof(UserRole), val)
                    ? (UserRole?)val
                    : Enum.TryParse<UserRole>(s, true, out var parsed) ? parsed : (UserRole?)null)
                .Where(r => r.HasValue)
                .Select(r => r!.Value)
                .Distinct()
                .OrderBy(r => (int)r)
                .ToList();

            return roles.Count > 0 ? roles : new[] { UserRole.Auditor };
        }
    }

    public void DefinirPapeis(IEnumerable<UserRole> novosPapeis)
    {
        if (EhAdminImune())
        {
            var lista = novosPapeis?.ToList() ?? new List<UserRole>();
            if (!lista.Contains(UserRole.Master))
            {
                throw new InvalidOperationException("O usuário admin@admin possui imunidade perpétua e não pode perder o papel Master.");
            }
            PapeisCsv = ((int)UserRole.Master).ToString();
            return;
        }

        var validos = (novosPapeis ?? Enumerable.Empty<UserRole>())
            .Distinct()
            .OrderBy(r => (int)r)
            .Select(r => ((int)r).ToString())
            .ToList();

        if (validos.Count == 0)
        {
            throw new ArgumentException("O usuário deve possuir pelo menos um papel atribuído.");
        }

        PapeisCsv = string.Join(",", validos);
    }

    /// <summary>
    /// Verifica se o usuário é o administrador perpétuo (admin@admin ou admin).
    /// </summary>
    public bool EhAdminImune()
    {
        return string.Equals(Login?.Trim(), AdminMasterLogin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Login?.Trim(), AdminShortLogin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Email?.Trim(), AdminMasterLogin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Regra de Acúmulo de Papéis: retorna o papel mais permissivo (menor valor numérico no enum: Master=1 > Financeiro=2 > GestorContratos=3 > Auditor=4).
    /// </summary>
    public UserRole ObterPapelMaisPermissivo()
    {
        if (EhAdminImune())
        {
            return UserRole.Master;
        }

        return Papeis.OrderBy(r => (int)r).FirstOrDefault(UserRole.Auditor);
    }

    public bool PossuiPapel(UserRole role)
    {
        if (EhAdminImune() || Papeis.Contains(UserRole.Master))
        {
            return true;
        }
        return Papeis.Contains(role);
    }

    public bool PodeAcessarAdministracaoSistema() => EhAdminImune() || Papeis.Contains(UserRole.Master);

    public bool PodeGerenciarFinanceiro() =>
        EhAdminImune() || Papeis.Contains(UserRole.Master) || Papeis.Contains(UserRole.Financeiro);

    public bool PodeGerenciarContratos() =>
        EhAdminImune() || Papeis.Contains(UserRole.Master) || Papeis.Contains(UserRole.Financeiro) || Papeis.Contains(UserRole.GestorContratos);

    public bool PodeEditarDados() =>
        EhAdminImune() || Papeis.Contains(UserRole.Master) || Papeis.Contains(UserRole.Financeiro) || Papeis.Contains(UserRole.GestorContratos);

    public void GarantirIntegridadeAdmin()
    {
        if (EhAdminImune())
        {
            Ativo = true;
            AutenticacaoAd = false;
            PapeisCsv = ((int)UserRole.Master).ToString();
        }
    }
}
