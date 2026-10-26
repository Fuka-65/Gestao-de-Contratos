using System.Globalization;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Contratos.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contratos.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IClmRepository _repository;

    public HomeController(IClmRepository repository)
    {
        _repository = repository;
    }

    public async Task<IActionResult> Index()
    {
        var hoje = DateTime.Today;
        var config = await _repository.ObterConfiguracaoAsync();
        var contratos = await _repository.ObterContratosAsync();
        var faturas = await _repository.ObterFaturasAsync();
        var fornecedores = await _repository.ObterFornecedoresAsync();

        ViewBag.Config = config;

        int mesAtual = hoje.Month;
        int anoAtual = hoje.Year;

        var faturasMesAtual = faturas
            .Where(f => f.MesReferencia == mesAtual && f.AnoReferencia == anoAtual)
            .ToList();

        var contratosAtivos = contratos
            .Where(c => c.Status == StatusContrato.Vigente || c.Status == StatusContrato.EmRenovacao)
            .ToList();

        var contratosProximoVencimento = contratosAtivos
            .Where(c => c.EstaProximoVencimento(hoje) || (c.DiasRestantes(hoje) >= 0 && c.DiasRestantes(hoje) <= config.MargemGlobalDiasAlertaVencimento))
            .ToList();

        var contratosVencidos = contratos
            .Where(c => c.EstaVencido(hoje))
            .ToList();

        var faturasComDesvioMes = faturasMesAtual
            .Where(f => f.PossuiDesvio)
            .ToList();

        var vm = new DashboardViewModel
        {
            CompetenciaAtualLabel = $"{mesAtual:D2}/{anoAtual}",
            DespesaTotalMensal = faturasMesAtual.Sum(f => f.ValorCobrado),
            TotalFaturasCompetenciaAtual = faturasMesAtual.Count,

            TotalContratosVigentes = contratosAtivos.Count,
            ValorGlobalSobGestao = contratosAtivos.Sum(c => c.ValorGlobalContrato),
            ComprometimentoMensalContratado = contratosAtivos.Sum(c => c.ValorMensalContratado),

            TotalContratosProximoVencimento = contratosProximoVencimento.Count,
            TotalContratosRenovacaoAutomaticaCritica = contratosProximoVencimento.Count(c => c.RenovaAutomaticamente),
            TotalContratosVencidos = contratosVencidos.Count,

            TotalFaturasComDesvioMes = faturasComDesvioMes.Count,
            ValorTotalDesvioMes = faturasComDesvioMes.Sum(f => f.ValorDesvio),
            ValorDesvioAcumuladoGeral = faturas.Where(f => f.PossuiDesvio).Sum(f => f.ValorDesvio),

            TotalImpostosApuradosMes = contratosAtivos.Sum(c => c.CalcularImpostosMensais(c.AliquotaImpostosPercentual > 0 ? c.AliquotaImpostosPercentual : config.AliquotaPadraoImpostosPercentual)),
            TotalLiquidoSemImpostosMes = contratosAtivos.Sum(c => c.CalcularValorLiquidoMensal(c.AliquotaImpostosPercentual > 0 ? c.AliquotaImpostosPercentual : config.AliquotaPadraoImpostosPercentual)),
            TotalOpexMensalAtivo = contratosAtivos.Sum(c => c.ObterOpexEfetivo()),
            TotalCapexMensalAtivo = contratosAtivos.Sum(c => c.ObterCapexEfetivo()),
            ModalidadeOrcamentoPadrao = config.ModalidadeOrcamentoPadrao
        };

        // Tabela de Contratos que exigem atenção imediata (Vencidos ou próximos do vencimento)
        vm.ContratosAtencaoImediata = contratos
            .Where(c => c.EstaVencido(hoje) || c.EstaProximoVencimento(hoje) || c.DiasRestantes(hoje) <= config.MargemGlobalDiasAlertaVencimento)
            .OrderBy(c => c.DiasRestantes(hoje))
            .Select(c => new ContratoAlertaItemViewModel
            {
                Id = c.Id,
                NumeroContrato = c.NumeroContrato,
                TituloObjeto = c.TituloObjeto,
                FornecedorNome = c.Fornecedor?.NomeFantasia ?? "Fornecedor",
                Categoria = c.CategoriaContrato,
                DataTerminoVigencia = c.DataTerminoVigencia,
                DiasRestantes = c.DiasRestantes(hoje),
                ValorMensal = c.ValorMensalContratado,
                RenovaAutomaticamente = c.RenovaAutomaticamente,
                DiasAvisoPrevioRescisao = c.DiasAvisoPrevioRescisao,
                GestorNome = c.GestorContratoNome,
                Status = c.Status,
                EmJanelaCriticaAvisoPrevio = c.EmJanelaCriticaAvisoPrevio(hoje)
            })
            .ToList();

        // Tabela de Faturas Recentes e Auditoria
        vm.FaturasRecentesAuditoria = faturas
            .OrderByDescending(f => f.AnoReferencia)
            .ThenByDescending(f => f.MesReferencia)
            .ThenByDescending(f => f.ValorDesvio)
            .Take(8)
            .Select(f => new FaturaAuditoriaItemViewModel
            {
                Id = f.Id,
                ContratoId = f.ContratoId,
                NumeroContrato = f.Contrato?.NumeroContrato ?? $"CTR #{f.ContratoId}",
                FornecedorNome = f.Fornecedor?.NomeFantasia ?? "Fornecedor",
                NumeroNotaFiscal = f.NumeroNotaFiscal,
                Competencia = f.CompetenciaFormatada,
                DataVencimento = f.DataVencimento,
                ValorContratado = f.ValorContratado,
                ValorCobrado = f.ValorCobrado,
                PossuiDesvio = f.PossuiDesvio,
                ValorDesvio = f.ValorDesvio,
                PorcentagemDesvio = f.PorcentagemDesvio,
                Status = f.Status,
                Observacoes = f.Observacoes
            })
            .ToList();

        // Gráfico 1: Evolução de Gastos Contratuais (Últimos 6 a 12 meses)
        var ptBr = new CultureInfo("pt-BR");
        for (int i = 5; i >= 0; i--)
        {
            var dataMes = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-i);
            var nomeMes = dataMes.ToString("MMM/yy", ptBr).ToUpperInvariant();
            var faturasDoMes = faturas.Where(f => f.MesReferencia == dataMes.Month && f.AnoReferencia == dataMes.Year).ToList();

            vm.LabelsEvolucao12Meses.Add(nomeMes);
            vm.ValoresContratados12Meses.Add(faturasDoMes.Sum(f => f.ValorContratado));
            vm.ValoresCobrados12Meses.Add(faturasDoMes.Sum(f => f.ValorCobrado));
        }

        // Gráfico 2: Top Fornecedores (Market Share por valor mensal contratado)
        var topFornecedores = contratosAtivos
            .GroupBy(c => c.Fornecedor?.NomeFantasia ?? "Outros")
            .Select(g => new { Fornecedor = g.Key, TotalMensal = g.Sum(x => x.ValorMensalContratado) })
            .OrderByDescending(x => x.TotalMensal)
            .Take(5)
            .ToList();

        foreach (var item in topFornecedores)
        {
            vm.LabelsTopFornecedores.Add(item.Fornecedor);
            vm.ValoresTopFornecedores.Add(item.TotalMensal);
        }

        // Categorias de Contratos
        var porCategoria = contratosAtivos
            .GroupBy(c => c.CategoriaContrato.ObterNomeExibicao())
            .Select(g => new { Categoria = g.Key, Total = g.Sum(x => x.ValorMensalContratado) })
            .OrderByDescending(x => x.Total)
            .ToList();

        foreach (var cat in porCategoria)
        {
            vm.LabelsCategorias.Add(cat.Categoria);
            vm.ValoresCategorias.Add(cat.Total);
        }

        return View(vm);
    }
}
