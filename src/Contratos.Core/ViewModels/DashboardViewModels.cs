using Contratos.Core.Entities;
using Contratos.Core.Enums;

namespace Contratos.Core.ViewModels;

public class DashboardViewModel
{
    // KPI 1: Despesa Total Mensal
    public decimal DespesaTotalMensal { get; set; }
    public int TotalFaturasCompetenciaAtual { get; set; }
    public string CompetenciaAtualLabel { get; set; } = string.Empty;

    // KPI 2: Contratos Vigentes Ativos
    public int TotalContratosVigentes { get; set; }
    public decimal ValorGlobalSobGestao { get; set; }
    public decimal ComprometimentoMensalContratado { get; set; }

    // KPI 3: Alertas de Vencimento Próximo
    public int TotalContratosProximoVencimento { get; set; }
    public int TotalContratosRenovacaoAutomaticaCritica { get; set; }
    public int TotalContratosVencidos { get; set; }

    // KPI 4: Auditoria de Desvios Financeiros
    public int TotalFaturasComDesvioMes { get; set; }
    public decimal ValorTotalDesvioMes { get; set; }
    public decimal ValorDesvioAcumuladoGeral { get; set; }

    // Indicadores Contábeis de Impostos Apurados e OPEX / CAPEX (Competência Atual)
    public decimal TotalImpostosApuradosMes { get; set; }
    public decimal TotalLiquidoSemImpostosMes { get; set; }
    public decimal TotalOpexMensalAtivo { get; set; }
    public decimal TotalCapexMensalAtivo { get; set; }
    public ModalidadeValorOrcamento ModalidadeOrcamentoPadrao { get; set; } = ModalidadeValorOrcamento.ValorBrutoComImpostos;

    // Listas de Acompanhamento Rápido e Alertas
    public List<ContratoAlertaItemViewModel> ContratosAtencaoImediata { get; set; } = new();
    public List<FaturaAuditoriaItemViewModel> FaturasRecentesAuditoria { get; set; } = new();

    // Dados para Gráficos Chart.js
    public List<string> LabelsEvolucao12Meses { get; set; } = new();
    public List<decimal> ValoresContratados12Meses { get; set; } = new();
    public List<decimal> ValoresCobrados12Meses { get; set; } = new();

    public List<string> LabelsTopFornecedores { get; set; } = new();
    public List<decimal> ValoresTopFornecedores { get; set; } = new();

    public List<string> LabelsCategorias { get; set; } = new();
    public List<decimal> ValoresCategorias { get; set; } = new();
}

public class ContratoAlertaItemViewModel
{
    public int Id { get; set; }
    public string NumeroContrato { get; set; } = string.Empty;
    public string TituloObjeto { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public CategoriaContrato Categoria { get; set; }
    public DateTime DataTerminoVigencia { get; set; }
    public int DiasRestantes { get; set; }
    public decimal ValorMensal { get; set; }
    public bool RenovaAutomaticamente { get; set; }
    public int DiasAvisoPrevioRescisao { get; set; }
    public string GestorNome { get; set; } = string.Empty;
    public StatusContrato Status { get; set; }
    public bool EmJanelaCriticaAvisoPrevio { get; set; }
}

public class FaturaAuditoriaItemViewModel
{
    public int Id { get; set; }
    public int ContratoId { get; set; }
    public string NumeroContrato { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public string NumeroNotaFiscal { get; set; } = string.Empty;
    public string Competencia { get; set; } = string.Empty;
    public DateTime DataVencimento { get; set; }
    public decimal ValorContratado { get; set; }
    public decimal ValorCobrado { get; set; }
    public bool PossuiDesvio { get; set; }
    public decimal ValorDesvio { get; set; }
    public decimal PorcentagemDesvio { get; set; }
    public StatusFatura Status { get; set; }
    public string? Observacoes { get; set; }
}

public class RateioResumoDepartamentoViewModel
{
    public string Departamento { get; set; } = string.Empty;
    public string CentroDeCusto { get; set; } = string.Empty;
    public int QuantidadeContratos { get; set; }
    public decimal CustoMensalAlocado { get; set; }
    public decimal PercentualSobreTotalCorporativo { get; set; }
}
