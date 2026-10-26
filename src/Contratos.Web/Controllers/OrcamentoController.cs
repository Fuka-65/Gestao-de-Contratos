using System.Globalization;
using System.Text;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Contratos.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contratos.Web.Controllers;

[Authorize]
public class OrcamentoController : Controller
{
    private readonly IClmRepository _repository;

    public OrcamentoController(IClmRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        TipoPeriodoOrcamento? periodo = null,
        int? mesesPersonalizado = null,
        int? mesInicial = null,
        int? anoInicial = null,
        ModalidadeValorOrcamento? modalidade = null,
        bool? considerarRenovacao = null,
        decimal? reajusteSimulado = null,
        CategoriaContrato? categoria = null,
        string? departamento = null)
    {
        var vm = await ConstruirProjecaoAsync(
            periodo,
            mesesPersonalizado,
            mesInicial,
            anoInicial,
            modalidade,
            considerarRenovacao,
            reajusteSimulado,
            categoria,
            departamento);

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCsv(
        TipoPeriodoOrcamento? periodo = null,
        int? mesesPersonalizado = null,
        int? mesInicial = null,
        int? anoInicial = null,
        ModalidadeValorOrcamento? modalidade = null,
        bool? considerarRenovacao = null,
        decimal? reajusteSimulado = null,
        CategoriaContrato? categoria = null,
        string? departamento = null)
    {
        var vm = await ConstruirProjecaoAsync(
            periodo,
            mesesPersonalizado,
            mesInicial,
            anoInicial,
            modalidade,
            considerarRenovacao,
            reajusteSimulado,
            categoria,
            departamento);

        var sb = new StringBuilder();
        sb.AppendLine("NumeroContrato;Fornecedor;Objeto;Categoria;Departamento;CentroDeCusto;MesesAtivos;AliquotaImpostos(%);TotalBruto(R$);ImpostosApurados(R$);TotalLiquidoSemImpostos(R$);TotalContabilizado(R$);OPEX(R$);CAPEX(R$)");

        foreach (var item in vm.ContratosProjetados)
        {
            sb.AppendLine(
                $"{item.NumeroContrato};" +
                $"\"{item.FornecedorNome}\";" +
                $"\"{item.TituloObjeto}\";" +
                $"{item.Categoria.ObterNomeExibicao()};" +
                $"\"{item.Departamento}\";" +
                $"{item.CentroDeCusto};" +
                $"{item.MesesAtivosNoPeriodo};" +
                $"{item.AliquotaImpostosPercentual:N2};" +
                $"{item.TotalBrutoNoPeriodo:N2};" +
                $"{item.TotalImpostosNoPeriodo:N2};" +
                $"{item.TotalLiquidoNoPeriodo:N2};" +
                $"{item.TotalContabilizadoNoPeriodo:N2};" +
                $"{item.TotalOpexNoPeriodo:N2};" +
                $"{item.TotalCapexNoPeriodo:N2}");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var bodyBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var finalBytes = new byte[preamble.Length + bodyBytes.Length];
        Buffer.BlockCopy(preamble, 0, finalBytes, 0, preamble.Length);
        Buffer.BlockCopy(bodyBytes, 0, finalBytes, preamble.Length, bodyBytes.Length);

        return File(finalBytes, "text/csv; charset=utf-8", $"Orcamento_CLM_{vm.QuantidadeMeses}m_{DateTime.Today:yyyyMMdd}.csv");
    }

    private async Task<OrcamentoProjecaoViewModel> ConstruirProjecaoAsync(
        TipoPeriodoOrcamento? periodo,
        int? mesesPersonalizado,
        int? mesInicial,
        int? anoInicial,
        ModalidadeValorOrcamento? modalidade,
        bool? considerarRenovacao,
        decimal? reajusteSimulado,
        CategoriaContrato? categoria,
        string? departamento)
    {
        var config = await _repository.ObterConfiguracaoAsync();
        var todosContratos = await _repository.ObterContratosAsync();

        var tipoPeriodo = periodo ?? TipoPeriodoOrcamento.Anual;
        int qtdMeses = tipoPeriodo == TipoPeriodoOrcamento.Personalizado
            ? Math.Clamp(mesesPersonalizado ?? 12, 1, 60)
            : (int)tipoPeriodo;

        if (mesesPersonalizado.HasValue && tipoPeriodo != TipoPeriodoOrcamento.Personalizado && mesesPersonalizado.Value != (int)tipoPeriodo)
        {
            // Se o usuário digitou um número customizado que corresponde a um preset ou personalizado
            qtdMeses = Math.Clamp(mesesPersonalizado.Value, 1, 60);
        }

        int mesIni = Math.Clamp(mesInicial ?? DateTime.Today.Month, 1, 12);
        int anoIni = Math.Clamp(anoInicial ?? DateTime.Today.Year, 2020, 2035);
        var modContabil = modalidade ?? config.ModalidadeOrcamentoPadrao;
        bool renovarAuto = considerarRenovacao ?? true;
        decimal taxaReajuste = Math.Clamp(reajusteSimulado ?? config.TaxaInflacaoSimuladaPadrao, 0m, 50m);

        var departamentosDisponiveis = todosContratos
            .Select(c => c.DepartamentoResponsavel)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d)
            .ToList();

        var contratosFiltrados = todosContratos
            .Where(c => c.Status != StatusContrato.Rescindido && c.Status != StatusContrato.Encerrado)
            .ToList();

        if (categoria.HasValue)
        {
            contratosFiltrados = contratosFiltrados.Where(c => c.CategoriaContrato == categoria.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(departamento))
        {
            contratosFiltrados = contratosFiltrados.Where(c =>
                string.Equals(c.DepartamentoResponsavel, departamento, StringComparison.OrdinalIgnoreCase) ||
                c.Rateios.Any(r => string.Equals(r.Departamento, departamento, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var dataInicioProjecao = new DateTime(anoIni, mesIni, 1);
        var dataFimProjecao = dataInicioProjecao.AddMonths(qtdMeses - 1);
        var ptBr = new CultureInfo("pt-BR");

        var vm = new OrcamentoProjecaoViewModel
        {
            PeriodoSelecionado = tipoPeriodo,
            QuantidadeMeses = qtdMeses,
            MesInicial = mesIni,
            AnoInicial = anoIni,
            ModalidadeContabil = modContabil,
            ConsiderarRenovacaoAutomatica = renovarAuto,
            PercentualReajusteSimulado = taxaReajuste,
            CategoriaFiltro = categoria,
            DepartamentoFiltro = departamento,
            DepartamentosDisponiveis = departamentosDisponiveis,
            TituloPeriodo = tipoPeriodo == TipoPeriodoOrcamento.Personalizado
                ? $"Personalizado ({qtdMeses} {(qtdMeses == 1 ? "Mês" : "Meses")})"
                : tipoPeriodo.ObterNomeExibicao(),
            IntervaloCompetenciasLabel = $"{dataInicioProjecao.ToString("MMM/yyyy", ptBr).ToUpperInvariant()} a {dataFimProjecao.ToString("MMM/yyyy", ptBr).ToUpperInvariant()}"
        };

        // Estrutura acumuladora por contrato e por mês
        var acumuladorContratos = contratosFiltrados.ToDictionary(
            c => c.Id,
            c => new OrcamentoContratoItemViewModel
            {
                ContratoId = c.Id,
                NumeroContrato = c.NumeroContrato,
                TituloObjeto = c.TituloObjeto,
                FornecedorNome = c.Fornecedor?.NomeFantasia ?? "Fornecedor",
                Categoria = c.CategoriaContrato,
                Departamento = c.DepartamentoResponsavel,
                CentroDeCusto = c.CentroDeCusto,
                IndiceReajuste = c.IndiceReajuste,
                RenovaAutomaticamente = c.RenovaAutomaticamente,
                DataTerminoVigencia = c.DataTerminoVigencia,
                AliquotaImpostosPercentual = c.AliquotaImpostosPercentual > 0 ? c.AliquotaImpostosPercentual : config.AliquotaPadraoImpostosPercentual,
                ValorMensalBrutoAtual = c.ValorMensalContratado,
                ValorMensalImpostosAtual = c.CalcularImpostosMensais(c.AliquotaImpostosPercentual > 0 ? c.AliquotaImpostosPercentual : config.AliquotaPadraoImpostosPercentual),
                ValorMensalLiquidoAtual = c.CalcularValorLiquidoMensal(c.AliquotaImpostosPercentual > 0 ? c.AliquotaImpostosPercentual : config.AliquotaPadraoImpostosPercentual)
            });

        var acumuladorDeptos = new Dictionary<string, OrcamentoDepartamentoItemViewModel>(StringComparer.OrdinalIgnoreCase);
        decimal totalSemReajusteContabilizado = 0m;

        for (int m = 0; m < qtdMeses; m++)
        {
            var dataMes = dataInicioProjecao.AddMonths(m);
            var itemMes = new OrcamentoMesItemViewModel
            {
                Ano = dataMes.Year,
                Mes = dataMes.Month,
                CompetenciaLabel = dataMes.ToString("MMM/yy", ptBr).ToUpperInvariant()
            };

            foreach (var contrato in contratosFiltrados)
            {
                decimal aliqEfetiva = contrato.AliquotaImpostosPercentual > 0
                    ? contrato.AliquotaImpostosPercentual
                    : config.AliquotaPadraoImpostosPercentual;

                var proj = contrato.ProjetarCompetencia(
                    dataMes.Year,
                    dataMes.Month,
                    renovarAuto,
                    taxaReajuste,
                    aliqEfetiva);

                if (!proj.AtivoNoMes)
                    continue;

                var projSemReajuste = contrato.ProjetarCompetencia(
                    dataMes.Year,
                    dataMes.Month,
                    renovarAuto,
                    0m,
                    aliqEfetiva);

                decimal valorContabilSemReajuste = modContabil == ModalidadeValorOrcamento.ValorLiquidoSemImpostos
                    ? projSemReajuste.ValorLiquido
                    : projSemReajuste.ValorBruto;

                totalSemReajusteContabilizado += valorContabilSemReajuste;

                decimal valorContabilMes = modContabil == ModalidadeValorOrcamento.ValorLiquidoSemImpostos
                    ? proj.ValorLiquido
                    : proj.ValorBruto;

                decimal opexContabilMes = modContabil == ModalidadeValorOrcamento.ValorLiquidoSemImpostos
                    ? proj.ValorOpexLiquido
                    : proj.ValorOpexBruto;

                decimal capexContabilMes = modContabil == ModalidadeValorOrcamento.ValorLiquidoSemImpostos
                    ? proj.ValorCapexLiquido
                    : proj.ValorCapexBruto;

                // Acumula no mês
                itemMes.QuantidadeContratosAtivos++;
                itemMes.ValorBruto += proj.ValorBruto;
                itemMes.ValorImpostos += proj.ValorImpostos;
                itemMes.ValorLiquido += proj.ValorLiquido;
                itemMes.ValorOpexContabilizado += opexContabilMes;
                itemMes.ValorCapexContabilizado += capexContabilMes;
                itemMes.ValorPrincipalContabilizado += valorContabilMes;
                if (proj.ReajusteSimuladoAplicado) itemMes.ContratosComReajusteNoMes++;

                // Acumula no total geral
                vm.TotalBrutoProjetado += proj.ValorBruto;
                vm.TotalImpostosApuradosProjetado += proj.ValorImpostos;
                vm.TotalLiquidoProjetado += proj.ValorLiquido;
                vm.TotalOpexBrutoProjetado += proj.ValorOpexBruto;
                vm.TotalCapexBrutoProjetado += proj.ValorCapexBruto;
                vm.TotalOpexLiquidoProjetado += proj.ValorOpexLiquido;
                vm.TotalCapexLiquidoProjetado += proj.ValorCapexLiquido;

                // Acumula no contrato
                var itemCtr = acumuladorContratos[contrato.Id];
                itemCtr.MesesAtivosNoPeriodo++;
                itemCtr.TotalBrutoNoPeriodo += proj.ValorBruto;
                itemCtr.TotalImpostosNoPeriodo += proj.ValorImpostos;
                itemCtr.TotalLiquidoNoPeriodo += proj.ValorLiquido;
                itemCtr.TotalContabilizadoNoPeriodo += valorContabilMes;
                itemCtr.TotalOpexNoPeriodo += opexContabilMes;
                itemCtr.TotalCapexNoPeriodo += capexContabilMes;
                if (proj.ReajusteSimuladoAplicado) itemCtr.SofreuReajusteNaProjecao = true;
                if (proj.EmVigenciaRenovadaAutomaticamente) itemCtr.ExigiuRenovacaoNaProjecao = true;

                // Acumula nos Departamentos (usando Rateios se houver, ou Departamento Responsável do Contrato)
                if (contrato.Rateios.Count > 0)
                {
                    foreach (var r in contrato.Rateios)
                    {
                        if (!string.IsNullOrWhiteSpace(departamento) &&
                            !string.Equals(r.Departamento, departamento, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        decimal fator = r.Percentual / 100m;
                        AcumularDepto(
                            acumuladorDeptos,
                            r.Departamento,
                            r.CentroDeCusto,
                            Math.Round(proj.ValorBruto * fator, 2),
                            Math.Round(proj.ValorImpostos * fator, 2),
                            Math.Round(proj.ValorLiquido * fator, 2),
                            Math.Round(valorContabilMes * fator, 2),
                            Math.Round(opexContabilMes * fator, 2),
                            Math.Round(capexContabilMes * fator, 2));
                    }
                }
                else
                {
                    AcumularDepto(
                        acumuladorDeptos,
                        contrato.DepartamentoResponsavel,
                        contrato.CentroDeCusto,
                        proj.ValorBruto,
                        proj.ValorImpostos,
                        proj.ValorLiquido,
                        valorContabilMes,
                        opexContabilMes,
                        capexContabilMes);
                }
            }

            vm.EvolucaoMensal.Add(itemMes);
        }

        vm.ContratosProjetados = acumuladorContratos.Values
            .Where(c => c.MesesAtivosNoPeriodo > 0)
            .OrderByDescending(c => c.TotalContabilizadoNoPeriodo)
            .ToList();

        vm.TotalContratosAtivosNaProjecao = vm.ContratosProjetados.Count;
        vm.ImpactoReajusteInflacionarioNoPeriodo = Math.Max(0m, vm.TotalOrcamentoContabilizado - totalSemReajusteContabilizado);

        decimal somaDeptos = acumuladorDeptos.Values.Sum(d => d.TotalContabilizadoPeriodo);
        foreach (var d in acumuladorDeptos.Values)
        {
            d.ParticipacaoPercentual = somaDeptos > 0
                ? Math.Round((d.TotalContabilizadoPeriodo / somaDeptos) * 100m, 2)
                : 0m;
        }

        vm.OrcamentoPorDepartamento = acumuladorDeptos.Values
            .OrderByDescending(d => d.TotalContabilizadoPeriodo)
            .ToList();

        return vm;
    }

    private static void AcumularDepto(
        Dictionary<string, OrcamentoDepartamentoItemViewModel> dict,
        string depto,
        string cc,
        decimal bruto,
        decimal impostos,
        decimal liquido,
        decimal contabil,
        decimal opex,
        decimal capex)
    {
        string chave = $"{depto}|{cc}";
        if (!dict.TryGetValue(chave, out var item))
        {
            item = new OrcamentoDepartamentoItemViewModel
            {
                Departamento = depto,
                CentroDeCusto = cc
            };
            dict[chave] = item;
        }

        item.TotalBrutoPeriodo += bruto;
        item.TotalImpostosPeriodo += impostos;
        item.TotalLiquidoPeriodo += liquido;
        item.TotalContabilizadoPeriodo += contabil;
        item.TotalOpexPeriodo += opex;
        item.TotalCapexPeriodo += capex;
    }
}
