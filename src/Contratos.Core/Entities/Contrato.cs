using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Contratos.Core.Enums;

namespace Contratos.Core.Entities;

public class Contrato
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O número do contrato é obrigatório.")]
    [StringLength(60)]
    public string NumeroContrato { get; set; } = string.Empty;

    [Required(ErrorMessage = "O título/objeto do contrato é obrigatório.")]
    [StringLength(300)]
    public string TituloObjeto { get; set; } = string.Empty;

    [Required]
    public int FornecedorId { get; set; }

    [JsonIgnore]
    public Fornecedor? Fornecedor { get; set; }

    public CategoriaContrato CategoriaContrato { get; set; } = CategoriaContrato.ServicosTI;

    [DataType(DataType.Date)]
    public DateTime DataAssinatura { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime DataInicioVigencia { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime DataTerminoVigencia { get; set; } = DateTime.Today.AddMonths(12);

    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 999999999.99, ErrorMessage = "Informe um valor mensal válido.")]
    public decimal ValorMensalContratado { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ValorGlobalContrato { get; set; }

    [StringLength(10)]
    public string Moeda { get; set; } = "BRL";

    public IndiceReajuste IndiceReajuste { get; set; } = IndiceReajuste.IPCA;

    public int PeriodicidadeReajusteMeses { get; set; } = 12;

    public bool RenovaAutomaticamente { get; set; } = false;

    public int DiasAvisoPrevioRescisao { get; set; } = 30;

    public int DiasAlertaVencimento { get; set; } = 60;

    [Required(ErrorMessage = "O centro de custo é obrigatório.")]
    [StringLength(80)]
    public string CentroDeCusto { get; set; } = string.Empty;

    [Required(ErrorMessage = "O departamento responsável é obrigatório.")]
    [StringLength(120)]
    public string DepartamentoResponsavel { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome do gestor do contrato é obrigatório.")]
    [StringLength(150)]
    public string GestorContratoNome { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail do gestor do contrato é obrigatório.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(150)]
    public string GestorContratoEmail { get; set; } = string.Empty;

    public StatusContrato Status { get; set; } = StatusContrato.Vigente;

    [StringLength(400)]
    public string? ArquivoContratoPath { get; set; }

    [StringLength(1500)]
    public string? Observacoes { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public List<TermoAditivo> TermosAditivos { get; set; } = new();

    [JsonIgnore]
    public List<FaturaGasto> Faturas { get; set; } = new();

    [JsonIgnore]
    public List<RateioDepartamento> Rateios { get; set; } = new();

    /// <summary>
    /// Quantidade exata de dias até a data de término da vigência.
    /// </summary>
    public int DiasRestantes(DateTime? dataReferencia = null)
    {
        var hoje = (dataReferencia ?? DateTime.Today).Date;
        return (DataTerminoVigencia.Date - hoje).Days;
    }

    /// <summary>
    /// Retorna true se Status == Vigente (ou EmRenovacao) e dias restantes >= 0 e <= DiasAlertaVencimento.
    /// </summary>
    public bool EstaProximoVencimento(DateTime? dataReferencia = null)
    {
        if (Status != StatusContrato.Vigente && Status != StatusContrato.EmRenovacao)
            return false;

        var dias = DiasRestantes(dataReferencia);
        return dias >= 0 && dias <= DiasAlertaVencimento;
    }

    /// <summary>
    /// Retorna true se Status == Vigente e DataTerminoVigencia < DataAtual.
    /// </summary>
    public bool EstaVencido(DateTime? dataReferencia = null)
    {
        if (Status == StatusContrato.Vencido)
            return true;

        if (Status != StatusContrato.Vigente)
            return false;

        var hoje = (dataReferencia ?? DateTime.Today).Date;
        return DataTerminoVigencia.Date < hoje;
    }

    /// <summary>
    /// Verifica se o contrato com renovação automática está dentro ou próximo da janela crítica de notificação de não-renovação.
    /// </summary>
    public bool EmJanelaCriticaAvisoPrevio(DateTime? dataReferencia = null)
    {
        if (!RenovaAutomaticamente || Status != StatusContrato.Vigente)
            return false;

        var dias = DiasRestantes(dataReferencia);
        return dias >= 0 && dias <= (DiasAvisoPrevioRescisao + 15);
    }
}
