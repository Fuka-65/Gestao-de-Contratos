using System.ComponentModel.DataAnnotations;

namespace Contratos.Core.Enums;

public enum UserRole
{
    [Display(Name = "Master (Acesso Total)")]
    Master = 1,

    [Display(Name = "Financeiro & Auditoria")]
    Financeiro = 2,

    [Display(Name = "Gestor de Contratos")]
    GestorContratos = 3,

    [Display(Name = "Auditor (Somente Leitura)")]
    Auditor = 4
}

public enum CategoriaContrato
{
    [Display(Name = "Serviços de TI & Cloud")]
    ServicosTI = 1,

    [Display(Name = "Licenciamento de Software (SaaS)")]
    LicenciamentoSoftware = 2,

    [Display(Name = "Consultoria Especializada")]
    Consultoria = 3,

    [Display(Name = "Facilities, Limpeza & Segurança")]
    Facilities = 4,

    [Display(Name = "Locação de Imóvel")]
    LocacaoImovel = 5,

    [Display(Name = "Locação de Máquinas & Equipamentos")]
    MaquinasEquipamentos = 6,

    [Display(Name = "Assessoria Jurídica & Contábil")]
    AssessoriaJuridica = 7,

    [Display(Name = "Seguros Corporativos")]
    Seguros = 8,

    [Display(Name = "Outros Serviços")]
    Outros = 9
}

public enum IndiceReajuste
{
    [Display(Name = "IPCA (IBGE)")]
    IPCA = 1,

    [Display(Name = "IGP-M (FGV)")]
    IGPM = 2,

    [Display(Name = "INPC")]
    INPC = 3,

    [Display(Name = "Fixo / Sem Reajuste")]
    FixoSemReajuste = 4,

    [Display(Name = "Outro Índice Setorial")]
    Outro = 5
}

public enum StatusContrato
{
    [Display(Name = "Vigente")]
    Vigente = 1,

    [Display(Name = "Em Renovação / Aditivo")]
    EmRenovacao = 2,

    [Display(Name = "Vencido")]
    Vencido = 3,

    [Display(Name = "Rescindido")]
    Rescindido = 4,

    [Display(Name = "Encerrado")]
    Encerrado = 5
}

public enum TipoAditivo
{
    [Display(Name = "Prorrogação de Prazo")]
    ProrrogacaoPrazo = 1,

    [Display(Name = "Alteração de Valor / Reajuste")]
    AlteracaoValor = 2,

    [Display(Name = "Prazo e Valor (Renovação + Reajuste)")]
    PrazoEValor = 3,

    [Display(Name = "Alteração Qualitativa de Escopo")]
    AlteracaoEscopo = 4
}

public enum StatusFatura
{
    [Display(Name = "Pendente de Conferência")]
    Pendente = 1,

    [Display(Name = "Aprovada para Pagamento")]
    AprovadaParaPagamento = 2,

    [Display(Name = "Paga / Liquidada")]
    Paga = 3,

    [Display(Name = "Em Disputa / Contestada")]
    EmDisputaContestada = 4
}

public enum TipoPeriodoOrcamento
{
    [Display(Name = "Mensal (1 Mês)")]
    Mensal = 1,

    [Display(Name = "Bimestral (2 Meses)")]
    Bimestral = 2,

    [Display(Name = "Trimestral (3 Meses)")]
    Trimestral = 3,

    [Display(Name = "Quadrimestral (4 Meses)")]
    Quadrimestral = 4,

    [Display(Name = "Semestral (6 Meses)")]
    Semestral = 6,

    [Display(Name = "Anual (12 Meses)")]
    Anual = 12,

    [Display(Name = "Período Personalizado (N Meses)")]
    Personalizado = 99
}

public enum ModalidadeValorOrcamento
{
    [Display(Name = "Valor Bruto (Com Impostos Inclusos)")]
    ValorBrutoComImpostos = 1,

    [Display(Name = "Valor Líquido (Sem Impostos / Deduzido)")]
    ValorLiquidoSemImpostos = 2
}
