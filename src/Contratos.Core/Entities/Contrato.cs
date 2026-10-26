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

    /// <summary>
    /// Campo opcional para registrar a parcela de Despesa Operacional (OPEX) mensal do contrato.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ValorOpexMensal { get; set; }

    /// <summary>
    /// Campo opcional para registrar a parcela de Investimento em Capital (CAPEX) mensal do contrato.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ValorCapexMensal { get; set; }

    /// <summary>
    /// Alíquota percentual total de impostos incidentes/retidos no contrato (ISS, PIS, COFINS, IRRF, CSLL, etc.).
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    [Range(0, 99.99, ErrorMessage = "A alíquota de impostos deve estar entre 0% e 99,99%.")]
    public decimal AliquotaImpostosPercentual { get; set; } = 14.25m;

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

    /// <summary>
    /// Retorna o valor mensal de OPEX (Despesa Operacional). Se nem OPEX nem CAPEX forem informados, assume o valor mensal contratado como OPEX.
    /// </summary>
    public decimal ObterOpexEfetivo()
    {
        if (ValorOpexMensal.HasValue)
            return ValorOpexMensal.Value;

        if (ValorCapexMensal.HasValue)
            return Math.Max(0m, ValorMensalContratado - ValorCapexMensal.Value);

        return ValorMensalContratado;
    }

    /// <summary>
    /// Retorna o valor mensal de CAPEX (Investimento de Capital).
    /// </summary>
    public decimal ObterCapexEfetivo()
    {
        return ValorCapexMensal ?? 0m;
    }

    /// <summary>
    /// Calcula o montante mensal de impostos apurados sobre o valor bruto do contrato.
    /// </summary>
    public decimal CalcularImpostosMensais(decimal? aliquotaOverride = null)
    {
        var aliq = Math.Clamp(aliquotaOverride ?? AliquotaImpostosPercentual, 0m, 100m);
        return Math.Round(ValorMensalContratado * (aliq / 100m), 2);
    }

    /// <summary>
    /// Calcula o valor mensal líquido (sem impostos) do contrato.
    /// </summary>
    public decimal CalcularValorLiquidoMensal(decimal? aliquotaOverride = null)
    {
        return Math.Max(0m, ValorMensalContratado - CalcularImpostosMensais(aliquotaOverride));
    }

    /// <summary>
    /// Projeta os valores orçamentários do contrato para uma competência futura (Ano/Mês).
    /// </summary>
    public ProjecaoMensalContratoResult ProjetarCompetencia(
        int ano,
        int mes,
        bool considerarRenovacaoAutomatica = true,
        decimal percentualReajusteAnualSimulado = 0m,
        decimal? aliquotaImpostoOverride = null)
    {
        if (Status == StatusContrato.Rescindido || Status == StatusContrato.Encerrado)
        {
            return ProjecaoMensalContratoResult.Inativo(ano, mes);
        }

        var inicioCompetencia = new DateTime(ano, mes, 1);
        var fimCompetencia = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes));

        if (DataInicioVigencia.Date > fimCompetencia)
        {
            return ProjecaoMensalContratoResult.Inativo(ano, mes);
        }

        bool aposTerminoOriginal = DataTerminoVigencia.Date < inicioCompetencia;
        if (aposTerminoOriginal && !(considerarRenovacaoAutomatica && RenovaAutomaticamente))
        {
            return ProjecaoMensalContratoResult.Inativo(ano, mes);
        }

        // Calcula quantos ciclos de reajuste (ex: a cada 12 meses) ocorreram desde o início da vigência até o mês projetado
        int mesesDecorridos = ((ano - DataInicioVigencia.Year) * 12) + (mes - DataInicioVigencia.Month);
        int periodicidade = PeriodicidadeReajusteMeses > 0 ? PeriodicidadeReajusteMeses : 12;

        // Quantos aniversários futuros além do momento atual o contrato atravessará
        int mesesDesdeHoje = ((ano - DateTime.Today.Year) * 12) + (mes - DateTime.Today.Month);
        bool aplicaReajusteSimulado = IndiceReajuste != IndiceReajuste.FixoSemReajuste
                                      && percentualReajusteAnualSimulado > 0m
                                      && mesesDesdeHoje > 0
                                      && mesesDecorridos >= periodicidade;

        int ciclosFuturos = aplicaReajusteSimulado ? Math.Max(1, mesesDesdeHoje / periodicidade + (aposTerminoOriginal ? 1 : 0)) : 0;
        decimal fatorReajuste = 1m;
        for (int c = 0; c < ciclosFuturos; c++)
        {
            fatorReajuste *= (1m + (percentualReajusteAnualSimulado / 100m));
        }

        decimal valorBruto = Math.Round(ValorMensalContratado * fatorReajuste, 2);
        decimal aliq = Math.Clamp(aliquotaImpostoOverride ?? AliquotaImpostosPercentual, 0m, 100m);
        decimal valorImpostos = Math.Round(valorBruto * (aliq / 100m), 2);
        decimal valorLiquido = Math.Max(0m, valorBruto - valorImpostos);

        decimal opexBase = ObterOpexEfetivo();
        decimal capexBase = ObterCapexEfetivo();
        decimal somaBase = opexBase + capexBase;

        decimal propOpex = somaBase > 0m ? (opexBase / somaBase) : 1m;
        decimal propCapex = somaBase > 0m ? (capexBase / somaBase) : 0m;

        decimal opexBruto = Math.Round(valorBruto * propOpex, 2);
        decimal capexBruto = Math.Round(valorBruto * propCapex, 2);

        decimal opexLiquido = Math.Round(valorLiquido * propOpex, 2);
        decimal capexLiquido = Math.Round(valorLiquido * propCapex, 2);

        return new ProjecaoMensalContratoResult(
            ano,
            mes,
            true,
            valorBruto,
            valorImpostos,
            valorLiquido,
            opexBruto,
            capexBruto,
            opexLiquido,
            capexLiquido,
            ciclosFuturos > 0,
            aposTerminoOriginal);
    }
}

public record ProjecaoMensalContratoResult(
    int Ano,
    int Mes,
    bool AtivoNoMes,
    decimal ValorBruto,
    decimal ValorImpostos,
    decimal ValorLiquido,
    decimal ValorOpexBruto,
    decimal ValorCapexBruto,
    decimal ValorOpexLiquido,
    decimal ValorCapexLiquido,
    bool ReajusteSimuladoAplicado,
    bool EmVigenciaRenovadaAutomaticamente)
{
    public static ProjecaoMensalContratoResult Inativo(int ano, int mes) =>
        new(ano, mes, false, 0m, 0m, 0m, 0m, 0m, 0m, 0m, false, false);
}
