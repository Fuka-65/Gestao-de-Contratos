using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Contratos.Core.Entities;

public class Fornecedor
{
    public int Id { get; set; }

    [Required(ErrorMessage = "A Razão Social é obrigatória.")]
    [StringLength(200)]
    public string RazaoSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "O Nome Fantasia é obrigatório.")]
    [StringLength(150)]
    public string NomeFantasia { get; set; } = string.Empty;

    [Required(ErrorMessage = "O CNPJ é obrigatório.")]
    [StringLength(20)]
    public string Cnpj { get; set; } = string.Empty;

    [StringLength(50)]
    public string? InscricaoEstadual { get; set; }

    [StringLength(50)]
    public string? InscricaoMunicipal { get; set; }

    [StringLength(120)]
    public string? ContatoComercialNome { get; set; }

    [StringLength(40)]
    public string? ContatoComercialTelefone { get; set; }

    [StringLength(150)]
    public string? ContatoComercialEmail { get; set; }

    [StringLength(120)]
    public string? ContatoFinanceiroNome { get; set; }

    [StringLength(40)]
    public string? ContatoFinanceiroTelefone { get; set; }

    [StringLength(150)]
    public string? ContatoFinanceiroEmail { get; set; }

    [StringLength(100)]
    public string? CentralSuporte { get; set; }

    [StringLength(100)]
    public string? SlaAtendimento { get; set; }

    [StringLength(250)]
    public string? Site { get; set; }

    [StringLength(300)]
    public string? LogoPath { get; set; }

    public bool Ativo { get; set; } = true;

    [StringLength(1000)]
    public string? Observacoes { get; set; }

    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public List<Contrato> Contratos { get; set; } = new();

    [JsonIgnore]
    public List<FaturaGasto> Faturas { get; set; } = new();
}
