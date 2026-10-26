using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Contratos.Core.Enums;

namespace Contratos.Core.Entities;

public class TermoAditivo
{
    public int Id { get; set; }

    [Required]
    public int ContratoId { get; set; }

    [JsonIgnore]
    public Contrato? Contrato { get; set; }

    [Required(ErrorMessage = "O número do termo aditivo é obrigatório.")]
    [StringLength(60)]
    public string NumeroAditivo { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime DataAssinatura { get; set; } = DateTime.Today;

    public TipoAditivo TipoAditivo { get; set; } = TipoAditivo.ProrrogacaoPrazo;

    [DataType(DataType.Date)]
    public DateTime? NovaDataTermino { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? NovoValorMensal { get; set; }

    [Required(ErrorMessage = "A descrição da alteração é obrigatória.")]
    [StringLength(1000)]
    public string DescricaoAlteracao { get; set; } = string.Empty;

    [StringLength(400)]
    public string? ArquivoAditivoPath { get; set; }

    public DateTime DataRegistro { get; set; } = DateTime.UtcNow;
}
