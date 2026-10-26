using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Contratos.Core.Enums;

namespace Contratos.Core.Entities;

public class FaturaGasto
{
    public int Id { get; set; }

    [Required]
    public int ContratoId { get; set; }

    [JsonIgnore]
    public Contrato? Contrato { get; set; }

    [Required]
    public int FornecedorId { get; set; }

    [JsonIgnore]
    public Fornecedor? Fornecedor { get; set; }

    [Range(1, 12, ErrorMessage = "Mês deve ser entre 1 e 12.")]
    public int MesReferencia { get; set; } = DateTime.Today.Month;

    [Range(2000, 2100, ErrorMessage = "Ano inválido.")]
    public int AnoReferencia { get; set; } = DateTime.Today.Year;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ValorContratado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ValorCobrado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ValorPago { get; set; }

    [DataType(DataType.Date)]
    public DateTime DataVencimento { get; set; } = DateTime.Today.AddDays(10);

    [DataType(DataType.Date)]
    public DateTime? DataPagamento { get; set; }

    [Required(ErrorMessage = "O número da Nota Fiscal / Fatura é obrigatório.")]
    [StringLength(80)]
    public string NumeroNotaFiscal { get; set; } = string.Empty;

    [StringLength(150)]
    public string? CodigoBarrasLinhaDigitavel { get; set; }

    [StringLength(400)]
    public string? ArquivoFaturaPath { get; set; }

    public StatusFatura Status { get; set; } = StatusFatura.Pendente;

    [StringLength(1000)]
    public string? Observacoes { get; set; }

    public DateTime DataLancamento { get; set; } = DateTime.UtcNow;

    // Regras de Auditoria Automática
    [NotMapped]
    public bool PossuiDesvio => ValorCobrado > ValorContratado;

    [NotMapped]
    public decimal ValorDesvio => ValorCobrado > ValorContratado ? (ValorCobrado - ValorContratado) : 0m;

    [NotMapped]
    public decimal PorcentagemDesvio =>
        ValorContratado > 0m && ValorCobrado > ValorContratado
            ? Math.Round(((ValorCobrado - ValorContratado) / ValorContratado) * 100m, 2)
            : 0m;

    [NotMapped]
    public string CompetenciaFormatada => $"{MesReferencia:D2}/{AnoReferencia}";
}
