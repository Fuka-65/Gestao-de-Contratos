using Contratos.Core.Enums;

namespace Contratos.Core.ViewModels;

public class OrcamentoProjecaoViewModel
{
    // Parâmetros de Filtro e Simulação
    public TipoPeriodoOrcamento PeriodoSelecionado { get; set; } = TipoPeriodoOrcamento.Anual;
    public int QuantidadeMeses { get; set; } = 12;
    public int MesInicial { get; set; } = DateTime.Today.Month;
    public int AnoInicial { get; set; } = DateTime.Today.Year;
    public ModalidadeValorOrcamento ModalidadeContabil { get; set; } = ModalidadeValorOrcamento.ValorBrutoComImpostos;
    public bool ConsiderarRenovacaoAutomatica { get; set; } = true;
    public decimal PercentualReajusteSimulado { get; set; } = 4.50m;
    public CategoriaContrato? CategoriaFiltro { get; set; }
    public string? DepartamentoFiltro { get; set; }

    public string TituloPeriodo { get; set; } = "Anual (12 Meses)";
    public string IntervaloCompetenciasLabel { get; set; } = string.Empty;

    // KPIs Consolidados do Período Projetado
    public decimal TotalBrutoProjetado { get; set; }
    public decimal TotalImpostosApuradosProjetado { get; set; }
    public decimal TotalLiquidoProjetado { get; set; }

    /// <summary>
    /// Retorna o valor orçamentário principal de acordo com a parametrização contábil (Bruto ou Líquido sem impostos).
    /// </summary>
    public decimal TotalOrcamentoContabilizado =>
        ModalidadeContabil == ModalidadeValorOrcamento.ValorLiquidoSemImpostos
            ? TotalLiquidoProjetado
            : TotalBrutoProjetado;

    public decimal MediaMensalContabilizada =>
        QuantidadeMeses > 0 ? Math.Round(TotalOrcamentoContabilizado / QuantidadeMeses, 2) : 0m;

    // Segregação OPEX vs CAPEX
    public decimal TotalOpexBrutoProjetado { get; set; }
    public decimal TotalCapexBrutoProjetado { get; set; }
    public decimal TotalOpexLiquidoProjetado { get; set; }
    public decimal TotalCapexLiquidoProjetado { get; set; }

    public decimal TotalOpexContabilizado =>
        ModalidadeContabil == ModalidadeValorOrcamento.ValorLiquidoSemImpostos
            ? TotalOpexLiquidoProjetado
            : TotalOpexBrutoProjetado;

    public decimal TotalCapexContabilizado =>
        ModalidadeContabil == ModalidadeValorOrcamento.ValorLiquidoSemImpostos
            ? TotalCapexLiquidoProjetado
            : TotalCapexBrutoProjetado;

    public decimal PercentualOpex =>
        TotalOrcamentoContabilizado > 0
            ? Math.Round((TotalOpexContabilizado / TotalOrcamentoContabilizado) * 100m, 2)
            : 0m;

    public decimal PercentualCapex =>
        TotalOrcamentoContabilizado > 0
            ? Math.Round((TotalCapexContabilizado / TotalOrcamentoContabilizado) * 100m, 2)
            : 0m;

    public decimal ImpactoReajusteInflacionarioNoPeriodo { get; set; }
    public int TotalContratosAtivosNaProjecao { get; set; }

    // Evolução Mensal (Mês a Mês para Gráfico e Matriz)
    public List<OrcamentoMesItemViewModel> EvolucaoMensal { get; set; } = new();

    // Detalhamento por Contrato no Período
    public List<OrcamentoContratoItemViewModel> ContratosProjetados { get; set; } = new();

    // Detalhamento por Departamento / Centro de Custo no Período
    public List<OrcamentoDepartamentoItemViewModel> OrcamentoPorDepartamento { get; set; } = new();

    public List<string> DepartamentosDisponiveis { get; set; } = new();
}

public class OrcamentoMesItemViewModel
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string CompetenciaLabel { get; set; } = string.Empty;
    public int QuantidadeContratosAtivos { get; set; }
    public decimal ValorBruto { get; set; }
    public decimal ValorImpostos { get; set; }
    public decimal ValorLiquido { get; set; }
    public decimal ValorOpexContabilizado { get; set; }
    public decimal ValorCapexContabilizado { get; set; }
    public decimal ValorPrincipalContabilizado { get; set; }
    public int ContratosComReajusteNoMes { get; set; }
}

public class OrcamentoContratoItemViewModel
{
    public int ContratoId { get; set; }
    public string NumeroContrato { get; set; } = string.Empty;
    public string TituloObjeto { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public CategoriaContrato Categoria { get; set; }
    public string Departamento { get; set; } = string.Empty;
    public string CentroDeCusto { get; set; } = string.Empty;
    public IndiceReajuste IndiceReajuste { get; set; }
    public bool RenovaAutomaticamente { get; set; }
    public DateTime DataTerminoVigencia { get; set; }

    public int MesesAtivosNoPeriodo { get; set; }
    public decimal AliquotaImpostosPercentual { get; set; }

    public decimal ValorMensalBrutoAtual { get; set; }
    public decimal ValorMensalImpostosAtual { get; set; }
    public decimal ValorMensalLiquidoAtual { get; set; }

    public decimal TotalBrutoNoPeriodo { get; set; }
    public decimal TotalImpostosNoPeriodo { get; set; }
    public decimal TotalLiquidoNoPeriodo { get; set; }
    public decimal TotalContabilizadoNoPeriodo { get; set; }

    public decimal TotalOpexNoPeriodo { get; set; }
    public decimal TotalCapexNoPeriodo { get; set; }
    public bool SofreuReajusteNaProjecao { get; set; }
    public bool ExigiuRenovacaoNaProjecao { get; set; }
}

public class OrcamentoDepartamentoItemViewModel
{
    public string Departamento { get; set; } = string.Empty;
    public string CentroDeCusto { get; set; } = string.Empty;
    public decimal TotalBrutoPeriodo { get; set; }
    public decimal TotalImpostosPeriodo { get; set; }
    public decimal TotalLiquidoPeriodo { get; set; }
    public decimal TotalContabilizadoPeriodo { get; set; }
    public decimal TotalOpexPeriodo { get; set; }
    public decimal TotalCapexPeriodo { get; set; }
    public decimal ParticipacaoPercentual { get; set; }
}
