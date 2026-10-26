using System.Text;
using System.Text.RegularExpressions;
using Contratos.Core.Entities;
using Contratos.Core.Interfaces;
using Contratos.Core.Validators;
using Contratos.Core.ViewModels;

namespace Contratos.Core.Services;

public static class FornecedorCsvService
{
    public const string CabecalhoPadrao =
        "Cnpj;RazaoSocial;NomeFantasia;InscricaoEstadual;InscricaoMunicipal;ContatoComercialNome;ContatoComercialTelefone;ContatoComercialEmail;ContatoFinanceiroNome;ContatoFinanceiroTelefone;ContatoFinanceiroEmail;CentralSuporte;SlaAtendimento;Site;Ativo;Observacoes";

    public static byte[] ExportarParaCsvBytes(IEnumerable<Fornecedor> fornecedores)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CabecalhoPadrao);

        foreach (var f in fornecedores)
        {
            sb.AppendLine(string.Join(";",
                EscaparCsv(f.Cnpj),
                EscaparCsv(f.RazaoSocial),
                EscaparCsv(f.NomeFantasia),
                EscaparCsv(f.InscricaoEstadual),
                EscaparCsv(f.InscricaoMunicipal),
                EscaparCsv(f.ContatoComercialNome),
                EscaparCsv(f.ContatoComercialTelefone),
                EscaparCsv(f.ContatoComercialEmail),
                EscaparCsv(f.ContatoFinanceiroNome),
                EscaparCsv(f.ContatoFinanceiroTelefone),
                EscaparCsv(f.ContatoFinanceiroEmail),
                EscaparCsv(f.CentralSuporte),
                EscaparCsv(f.SlaAtendimento),
                EscaparCsv(f.Site),
                f.Ativo ? "Sim" : "Nao",
                EscaparCsv(f.Observacoes)));
        }

        return AdicionarBomUtf8(sb.ToString());
    }

    public static byte[] GerarModeloCsvBytes()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CabecalhoPadrao);
        sb.AppendLine("04.712.500/0001-07;Microsoft Informatica Ltda;Microsoft Brasil (Cloud & Copilot);116.011.448.118;3.049.211-8;Ricardo Vasconcelos;(11) 5504-2199;ricardo.v@microsoft-partner.com;Billing Enterprise;0800 888 4081;billing.br@microsoft.com;Premier Support 24x7;Critico: 1h | Alta: 4h;https://www.microsoft.com/pt-br;Sim;Exemplo de registro ja existente com divergencia para teste de confirmacao");
        sb.AppendLine("33.000.167/0001-01;equinix Brasil Data Centers S.A.;Equinix Brasil (Colocation SP4);112.440.100.119;4.110.220-1;Gustavo Prado;(11) 3500-8000;gprado@equinix.com;Faturamento Equinix;(11) 3500-8010;faturamento.br@equinix.com;NOC 24x7 IBX;99,999% Disponibilidade Eletrica;https://www.equinix.com.br;Sim;Novo fornecedor de Data Center e Colocation");
        return AdicionarBomUtf8(sb.ToString());
    }

    public static async Task<ResultadoImportacaoFornecedoresViewModel> ProcessarArquivoCsvAsync(
        string conteudoCsv,
        string nomeArquivo,
        IClmRepository repository)
    {
        var resultado = new ResultadoImportacaoFornecedoresViewModel
        {
            NomeArquivo = nomeArquivo,
            DataProcessamento = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(conteudoCsv))
        {
            return resultado;
        }

        var fornecedoresExistentes = await repository.ObterFornecedoresAsync();
        var mapaExistentesPorCnpj = fornecedoresExistentes
            .GroupBy(f => SomenteDigitos(f.Cnpj))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToDictionary(g => g.Key, g => g.First());

        var cnpjsVistosNoArquivo = new HashSet<string>(StringComparer.Ordinal);

        var linhas = conteudoCsv
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .ToList();

        int numeroLinha = 0;
        foreach (var linhaBruta in linhas)
        {
            numeroLinha++;
            if (string.IsNullOrWhiteSpace(linhaBruta))
                continue;

            var linhaTrim = linhaBruta.Trim();
            // Ignora linha de cabeçalho se começar com Cnpj ou RazaoSocial
            if (numeroLinha == 1 &&
                (linhaTrim.StartsWith("Cnpj", StringComparison.OrdinalIgnoreCase) ||
                 linhaTrim.StartsWith("\"Cnpj\"", StringComparison.OrdinalIgnoreCase) ||
                 linhaTrim.StartsWith("RazaoSocial", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            char delimitador = DetectarDelimitador(linhaBruta);
            var colunas = DividirLinhaCsv(linhaBruta, delimitador);

            string cnpjRaw = ObterColuna(colunas, 0);
            string razaoSocial = ObterColuna(colunas, 1);
            string nomeFantasia = ObterColuna(colunas, 2);

            // Validações obrigatórias
            if (string.IsNullOrWhiteSpace(cnpjRaw) || !CnpjValidator.EhValido(cnpjRaw))
            {
                resultado.Itens.Add(new LinhaImportacaoFornecedorItem
                {
                    NumeroLinha = numeroLinha,
                    Cnpj = string.IsNullOrWhiteSpace(cnpjRaw) ? "(Vazio)" : cnpjRaw,
                    RazaoSocial = razaoSocial,
                    NomeFantasia = nomeFantasia,
                    Status = StatusLinhaImportacaoFornecedor.ErroValidacao,
                    MensagemStatus = "CNPJ inválido ou com dígitos verificadores incorretos (Módulo 11)."
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(razaoSocial) || string.IsNullOrWhiteSpace(nomeFantasia))
            {
                resultado.Itens.Add(new LinhaImportacaoFornecedorItem
                {
                    NumeroLinha = numeroLinha,
                    Cnpj = CnpjValidator.Formatar(cnpjRaw),
                    RazaoSocial = razaoSocial,
                    NomeFantasia = nomeFantasia,
                    Status = StatusLinhaImportacaoFornecedor.ErroValidacao,
                    MensagemStatus = "Razão Social e Nome Fantasia são campos obrigatórios."
                });
                continue;
            }

            string cnpjDigitos = SomenteDigitos(cnpjRaw);
            string cnpjFormatado = CnpjValidator.Formatar(cnpjDigitos);

            // Controle de duplicidade dentro do próprio arquivo CSV
            if (!cnpjsVistosNoArquivo.Add(cnpjDigitos))
            {
                resultado.Itens.Add(new LinhaImportacaoFornecedorItem
                {
                    NumeroLinha = numeroLinha,
                    Cnpj = cnpjFormatado,
                    RazaoSocial = razaoSocial,
                    NomeFantasia = nomeFantasia,
                    Status = StatusLinhaImportacaoFornecedor.DuplicadoNoProprioArquivo,
                    MensagemStatus = "CNPJ repetido dentro do mesmo arquivo CSV. Linha duplicada ignorada."
                });
                continue;
            }

            var proposto = new Fornecedor
            {
                Cnpj = cnpjFormatado,
                RazaoSocial = razaoSocial.Trim(),
                NomeFantasia = nomeFantasia.Trim(),
                InscricaoEstadual = NuloSeVazio(ObterColuna(colunas, 3)),
                InscricaoMunicipal = NuloSeVazio(ObterColuna(colunas, 4)),
                ContatoComercialNome = NuloSeVazio(ObterColuna(colunas, 5)),
                ContatoComercialTelefone = NuloSeVazio(ObterColuna(colunas, 6)),
                ContatoComercialEmail = NuloSeVazio(ObterColuna(colunas, 7)),
                ContatoFinanceiroNome = NuloSeVazio(ObterColuna(colunas, 8)),
                ContatoFinanceiroTelefone = NuloSeVazio(ObterColuna(colunas, 9)),
                ContatoFinanceiroEmail = NuloSeVazio(ObterColuna(colunas, 10)),
                CentralSuporte = NuloSeVazio(ObterColuna(colunas, 11)),
                SlaAtendimento = NuloSeVazio(ObterColuna(colunas, 12)),
                Site = NuloSeVazio(ObterColuna(colunas, 13)),
                Ativo = InterpretarBooleano(ObterColuna(colunas, 14), padrao: true),
                Observacoes = NuloSeVazio(ObterColuna(colunas, 15))
            };

            // Verifica se já existe no banco de dados
            if (mapaExistentesPorCnpj.TryGetValue(cnpjDigitos, out var existente))
            {
                proposto.Id = existente.Id;
                var divergencias = CompararDivergencias(existente, proposto);

                if (divergencias.Count == 0)
                {
                    resultado.Itens.Add(new LinhaImportacaoFornecedorItem
                    {
                        NumeroLinha = numeroLinha,
                        Cnpj = cnpjFormatado,
                        RazaoSocial = existente.RazaoSocial,
                        NomeFantasia = existente.NomeFantasia,
                        FornecedorExistenteId = existente.Id,
                        Status = StatusLinhaImportacaoFornecedor.DuplicadoIdenticoMantido,
                        MensagemStatus = "Cadastro idêntico já existente na base. Registro salvo mantido."
                    });
                }
                else
                {
                    resultado.Itens.Add(new LinhaImportacaoFornecedorItem
                    {
                        NumeroLinha = numeroLinha,
                        Cnpj = cnpjFormatado,
                        RazaoSocial = proposto.RazaoSocial,
                        NomeFantasia = proposto.NomeFantasia,
                        FornecedorExistenteId = existente.Id,
                        DadosAtuaisSalvos = ClonarDadosBasicos(existente),
                        DadosPropostosCsv = proposto,
                        Divergencias = divergencias,
                        Status = StatusLinhaImportacaoFornecedor.ConflitoDivergenciaPendente,
                        MensagemStatus = $"CNPJ já cadastrado com {divergencias.Count} campo(s) divergente(s). Confirme se deseja atualizar ou manter o registro salvo."
                    });
                }
            }
            else
            {
                // Novo fornecedor: insere imediatamente e adiciona ao mapa
                var salvo = await repository.SalvarFornecedorAsync(proposto);
                mapaExistentesPorCnpj[cnpjDigitos] = salvo;

                resultado.Itens.Add(new LinhaImportacaoFornecedorItem
                {
                    NumeroLinha = numeroLinha,
                    Cnpj = salvo.Cnpj,
                    RazaoSocial = salvo.RazaoSocial,
                    NomeFantasia = salvo.NomeFantasia,
                    FornecedorExistenteId = salvo.Id,
                    Status = StatusLinhaImportacaoFornecedor.NovoInserido,
                    MensagemStatus = $"Fornecedor inserido com sucesso (ID #{salvo.Id})."
                });
            }
        }

        return resultado;
    }

    public static List<DivergenciaCampoFornecedorDto> CompararDivergencias(Fornecedor atual, Fornecedor proposto)
    {
        var lista = new List<DivergenciaCampoFornecedorDto>();

        VerificarCampo(lista, "Razão Social", atual.RazaoSocial, proposto.RazaoSocial);
        VerificarCampo(lista, "Nome Fantasia", atual.NomeFantasia, proposto.NomeFantasia);
        VerificarCampo(lista, "Inscrição Estadual", atual.InscricaoEstadual, proposto.InscricaoEstadual);
        VerificarCampo(lista, "Inscrição Municipal", atual.InscricaoMunicipal, proposto.InscricaoMunicipal);
        VerificarCampo(lista, "Contato Comercial (Nome)", atual.ContatoComercialNome, proposto.ContatoComercialNome);
        VerificarCampo(lista, "Contato Comercial (Telefone)", atual.ContatoComercialTelefone, proposto.ContatoComercialTelefone);
        VerificarCampo(lista, "Contato Comercial (E-mail)", atual.ContatoComercialEmail, proposto.ContatoComercialEmail);
        VerificarCampo(lista, "Contato Financeiro (Nome)", atual.ContatoFinanceiroNome, proposto.ContatoFinanceiroNome);
        VerificarCampo(lista, "Contato Financeiro (Telefone)", atual.ContatoFinanceiroTelefone, proposto.ContatoFinanceiroTelefone);
        VerificarCampo(lista, "Contato Financeiro (E-mail)", atual.ContatoFinanceiroEmail, proposto.ContatoFinanceiroEmail);
        VerificarCampo(lista, "Central de Suporte", atual.CentralSuporte, proposto.CentralSuporte);
        VerificarCampo(lista, "SLA de Atendimento", atual.SlaAtendimento, proposto.SlaAtendimento);
        VerificarCampo(lista, "Site", atual.Site, proposto.Site);
        VerificarCampo(lista, "Status Ativo", atual.Ativo ? "Sim" : "Não", proposto.Ativo ? "Sim" : "Não");
        VerificarCampo(lista, "Observações", atual.Observacoes, proposto.Observacoes);

        return lista;
    }

    private static void VerificarCampo(
        List<DivergenciaCampoFornecedorDto> lista,
        string nomeCampo,
        string? valorAtual,
        string? valorNovo)
    {
        var a = (valorAtual ?? string.Empty).Trim();
        var b = (valorNovo ?? string.Empty).Trim();

        if (!string.Equals(a, b, StringComparison.Ordinal))
        {
            lista.Add(new DivergenciaCampoFornecedorDto
            {
                NomeCampo = nomeCampo,
                ValorSalvoAtual = string.IsNullOrEmpty(a) ? "(Vazio)" : a,
                NovoValorCsv = string.IsNullOrEmpty(b) ? "(Vazio)" : b
            });
        }
    }

    private static Fornecedor ClonarDadosBasicos(Fornecedor f) => new()
    {
        Id = f.Id,
        Cnpj = f.Cnpj,
        RazaoSocial = f.RazaoSocial,
        NomeFantasia = f.NomeFantasia,
        InscricaoEstadual = f.InscricaoEstadual,
        InscricaoMunicipal = f.InscricaoMunicipal,
        ContatoComercialNome = f.ContatoComercialNome,
        ContatoComercialTelefone = f.ContatoComercialTelefone,
        ContatoComercialEmail = f.ContatoComercialEmail,
        ContatoFinanceiroNome = f.ContatoFinanceiroNome,
        ContatoFinanceiroTelefone = f.ContatoFinanceiroTelefone,
        ContatoFinanceiroEmail = f.ContatoFinanceiroEmail,
        CentralSuporte = f.CentralSuporte,
        SlaAtendimento = f.SlaAtendimento,
        Site = f.Site,
        Ativo = f.Ativo,
        Observacoes = f.Observacoes
    };

    private static string SomenteDigitos(string? input) =>
        string.IsNullOrWhiteSpace(input) ? string.Empty : Regex.Replace(input, @"[^\d]", "");

    private static string? NuloSeVazio(string? input) =>
        string.IsNullOrWhiteSpace(input) ? null : input.Trim();

    private static bool InterpretarBooleano(string? input, bool padrao)
    {
        if (string.IsNullOrWhiteSpace(input)) return padrao;
        var v = input.Trim().ToLowerInvariant();
        if (v is "1" or "true" or "sim" or "s" or "ativo" or "yes") return true;
        if (v is "0" or "false" or "nao" or "não" or "n" or "inativo" or "no") return false;
        return padrao;
    }

    private static char DetectarDelimitador(string linha)
    {
        int qtdPontoVirgula = linha.Count(c => c == ';');
        int qtdVirgula = linha.Count(c => c == ',');
        return qtdPontoVirgula >= qtdVirgula ? ';' : ',';
    }

    private static List<string> DividirLinhaCsv(string linha, char delimitador)
    {
        var resultado = new List<string>();
        var atual = new StringBuilder();
        bool entreAspas = false;

        for (int i = 0; i < linha.Length; i++)
        {
            char c = linha[i];
            if (c == '"')
            {
                if (entreAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else
                {
                    entreAspas = !entreAspas;
                }
            }
            else if (c == delimitador && !entreAspas)
            {
                resultado.Add(atual.ToString().Trim());
                atual.Clear();
            }
            else
            {
                atual.Append(c);
            }
        }

        resultado.Add(atual.ToString().Trim());
        return resultado;
    }

    private static string ObterColuna(List<string> colunas, int indice) =>
        indice >= 0 && indice < colunas.Count ? colunas[indice] : string.Empty;

    private static string EscaparCsv(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return string.Empty;

        var limpo = valor.Replace("\r", " ").Replace("\n", " ").Trim();
        if (limpo.Contains(';') || limpo.Contains('"') || limpo.Contains(','))
        {
            return $"\"{limpo.Replace("\"", "\"\"")}\"";
        }
        return limpo;
    }

    private static byte[] AdicionarBomUtf8(string conteudo)
    {
        var bom = Encoding.UTF8.GetPreamble();
        var bytes = Encoding.UTF8.GetBytes(conteudo);
        var resultado = new byte[bom.Length + bytes.Length];
        Buffer.BlockCopy(bom, 0, resultado, 0, bom.Length);
        Buffer.BlockCopy(bytes, 0, resultado, bom.Length, bytes.Length);
        return resultado;
    }
}
