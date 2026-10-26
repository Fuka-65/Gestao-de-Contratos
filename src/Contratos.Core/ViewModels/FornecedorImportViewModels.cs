using System.ComponentModel.DataAnnotations;
using Contratos.Core.Entities;

namespace Contratos.Core.ViewModels;

public enum StatusLinhaImportacaoFornecedor
{
    [Display(Name = "Novo Cadastro Inserido")]
    NovoInserido = 1,

    [Display(Name = "Já Cadastrado (Sem Divergência - Mantido)")]
    DuplicadoIdenticoMantido = 2,

    [Display(Name = "Divergência Detectada (Aguardando Confirmação)")]
    ConflitoDivergenciaPendente = 3,

    [Display(Name = "Cadastro Atualizado (Confirmado)")]
    AtualizadoAposConfirmacao = 4,

    [Display(Name = "Registro Salvo Mantido (Confirmado)")]
    MantidoAposConfirmacao = 5,

    [Display(Name = "CNPJ Repetido no Mesmo Arquivo (Ignorado)")]
    DuplicadoNoProprioArquivo = 6,

    [Display(Name = "Erro de Validação")]
    ErroValidacao = 7
}

public class DivergenciaCampoFornecedorDto
{
    public string NomeCampo { get; set; } = string.Empty;
    public string ValorSalvoAtual { get; set; } = string.Empty;
    public string NovoValorCsv { get; set; } = string.Empty;
}

public class LinhaImportacaoFornecedorItem
{
    public int NumeroLinha { get; set; }
    public string Cnpj { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public StatusLinhaImportacaoFornecedor Status { get; set; }
    public string MensagemStatus { get; set; } = string.Empty;
    public int? FornecedorExistenteId { get; set; }
    public Fornecedor? DadosAtuaisSalvos { get; set; }
    public Fornecedor? DadosPropostosCsv { get; set; }
    public List<DivergenciaCampoFornecedorDto> Divergencias { get; set; } = new();
}

public class ResultadoImportacaoFornecedoresViewModel
{
    public string NomeArquivo { get; set; } = string.Empty;
    public DateTime DataProcessamento { get; set; } = DateTime.UtcNow;

    public int TotalLinhasProcessadas => Itens.Count;
    public int TotalNovosInseridos => Itens.Count(i => i.Status == StatusLinhaImportacaoFornecedor.NovoInserido);
    public int TotalDuplicadosIdenticos => Itens.Count(i => i.Status == StatusLinhaImportacaoFornecedor.DuplicadoIdenticoMantido);
    public int TotalConflitosPendentes => Itens.Count(i => i.Status == StatusLinhaImportacaoFornecedor.ConflitoDivergenciaPendente);
    public int TotalAtualizadosConfirmados => Itens.Count(i => i.Status == StatusLinhaImportacaoFornecedor.AtualizadoAposConfirmacao);
    public int TotalMantidosConfirmados => Itens.Count(i => i.Status == StatusLinhaImportacaoFornecedor.MantidoAposConfirmacao);
    public int TotalDuplicadosNoArquivo => Itens.Count(i => i.Status == StatusLinhaImportacaoFornecedor.DuplicadoNoProprioArquivo);
    public int TotalErrosValidacao => Itens.Count(i => i.Status == StatusLinhaImportacaoFornecedor.ErroValidacao);

    public bool PossuiConflitosPendentes => TotalConflitosPendentes > 0;

    public List<LinhaImportacaoFornecedorItem> Itens { get; set; } = new();

    /// <summary>
    /// Estado serializado da carga para processar a confirmação das divergências sem perda do histórico das demais linhas.
    /// </summary>
    public string EstadoCargaJson { get; set; } = string.Empty;
}
