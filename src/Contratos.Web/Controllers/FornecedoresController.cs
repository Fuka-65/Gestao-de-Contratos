using System.Text;
using System.Text.Json;
using Contratos.Core.Entities;
using Contratos.Core.Interfaces;
using Contratos.Core.Services;
using Contratos.Core.Validators;
using Contratos.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contratos.Web.Controllers;

[Authorize]
public class FornecedoresController : Controller
{
    private readonly IClmRepository _repository;

    public FornecedoresController(IClmRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? busca = null)
    {
        var fornecedores = await _repository.ObterFornecedoresAsync();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            fornecedores = fornecedores.Where(f =>
                f.RazaoSocial.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                f.NomeFantasia.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                f.Cnpj.Contains(termo, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        ViewBag.Busca = busca;
        return View(fornecedores);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCsv(string? busca = null)
    {
        var fornecedores = await _repository.ObterFornecedoresAsync();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            fornecedores = fornecedores.Where(f =>
                f.RazaoSocial.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                f.NomeFantasia.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                f.Cnpj.Contains(termo, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var csvBytes = FornecedorCsvService.ExportarParaCsvBytes(fornecedores);

        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            "EXPORTAR_FORNECEDORES_CSV",
            "Fornecedor",
            $"Exportação de cadastro de fornecedores via CSV ({fornecedores.Count} registros exportados).",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return File(csvBytes, "text/csv; charset=utf-8", $"Fornecedores_Xhara_{DateTime.Today:yyyyMMdd}.csv");
    }

    [HttpGet]
    public IActionResult BaixarModeloCsv()
    {
        var bytes = FornecedorCsvService.GerarModeloCsvBytes();
        return File(bytes, "text/csv; charset=utf-8", "Modelo_Importacao_Fornecedores_Xhara.csv");
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpGet]
    public IActionResult Importar()
    {
        return View(new ResultadoImportacaoFornecedoresViewModel());
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Importar(IFormFile? arquivoCsv)
    {
        if (arquivoCsv == null || arquivoCsv.Length == 0)
        {
            TempData["Erro"] = "Selecione um arquivo .csv válido para realizar a importação em lote.";
            return View(new ResultadoImportacaoFornecedoresViewModel());
        }

        if (!arquivoCsv.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Erro"] = "Formato de arquivo inválido. Envie exclusivamente um arquivo com extensão .csv.";
            return View(new ResultadoImportacaoFornecedoresViewModel());
        }

        string conteudo;
        using (var reader = new StreamReader(arquivoCsv.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            conteudo = await reader.ReadToEndAsync();
        }

        var resultado = await FornecedorCsvService.ProcessarArquivoCsvAsync(conteudo, arquivoCsv.FileName, _repository);
        resultado.EstadoCargaJson = JsonSerializer.Serialize(resultado.Itens);

        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            "IMPORTAR_LOTE_FORNECEDORES_CSV",
            "Fornecedor",
            $"Arquivo '{arquivoCsv.FileName}' processado: {resultado.TotalLinhasProcessadas} linhas ({resultado.TotalNovosInseridos} novos, {resultado.TotalDuplicadosIdenticos} idênticos mantidos, {resultado.TotalConflitosPendentes} divergências aguardando confirmação, {resultado.TotalErrosValidacao} erros).",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (resultado.PossuiConflitosPendentes)
        {
            TempData["AlertaDesvio"] = $"Foram identificadas {resultado.TotalConflitosPendentes} duplicidade(s) com divergência de campos cadastrais. Confirme abaixo para cada registro se deseja atualizar ou manter os dados já salvos.";
        }
        else
        {
            TempData["Sucesso"] = $"Processamento da carga concluído: {resultado.TotalNovosInseridos} novo(s) fornecedor(es) cadastrado(s) e {resultado.TotalDuplicadosIdenticos} registro(s) idêntico(s) mantido(s).";
        }

        return View(resultado);
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolverDivergencias(
        string nomeArquivo,
        string estadoCargaJson,
        Dictionary<int, string>? decisoes)
    {
        if (string.IsNullOrWhiteSpace(estadoCargaJson))
        {
            TempData["Erro"] = "Sessão de importação expirada ou inválida. Envie o arquivo novamente.";
            return RedirectToAction(nameof(Importar));
        }

        var itens = JsonSerializer.Deserialize<List<LinhaImportacaoFornecedorItem>>(estadoCargaJson)
                    ?? new List<LinhaImportacaoFornecedorItem>();

        int atualizados = 0;
        int mantidos = 0;

        foreach (var item in itens.Where(i => i.Status == StatusLinhaImportacaoFornecedor.ConflitoDivergenciaPendente))
        {
            string acaoEscolhida = "Manter";
            if (decisoes != null && decisoes.TryGetValue(item.NumeroLinha, out var valorDecisao) && !string.IsNullOrWhiteSpace(valorDecisao))
            {
                acaoEscolhida = valorDecisao;
            }

            if (string.Equals(acaoEscolhida, "Atualizar", StringComparison.OrdinalIgnoreCase) && item.DadosPropostosCsv != null)
            {
                await _repository.SalvarFornecedorAsync(item.DadosPropostosCsv);
                item.Status = StatusLinhaImportacaoFornecedor.AtualizadoAposConfirmacao;
                item.MensagemStatus = $"Cadastro atualizado conforme confirmação do usuário ({item.Divergencias.Count} campo(s) atualizado(s)).";
                atualizados++;
            }
            else
            {
                item.Status = StatusLinhaImportacaoFornecedor.MantidoAposConfirmacao;
                item.MensagemStatus = "Registro original já salvo no sistema foi mantido conforme confirmação do usuário.";
                item.RazaoSocial = item.DadosAtuaisSalvos?.RazaoSocial ?? item.RazaoSocial;
                item.NomeFantasia = item.DadosAtuaisSalvos?.NomeFantasia ?? item.NomeFantasia;
                mantidos++;
            }
        }

        var resultadoFinal = new ResultadoImportacaoFornecedoresViewModel
        {
            NomeArquivo = nomeArquivo,
            DataProcessamento = DateTime.UtcNow,
            Itens = itens,
            EstadoCargaJson = JsonSerializer.Serialize(itens)
        };

        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            "RESOLVER_DIVERGENCIAS_CSV_FORNECEDORES",
            "Fornecedor",
            $"Resolução de divergências do arquivo '{nomeArquivo}': {atualizados} registro(s) atualizado(s) e {mantidos} registro(s) original(is) mantido(s).",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = $"Confirmação processada com sucesso: {atualizados} cadastro(s) atualizado(s) e {mantidos} registro(s) salvo(s) mantido(s). Confira o status consolidado da carga abaixo.";
        return View("Importar", resultadoFinal);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var fornecedor = await _repository.ObterFornecedorPorIdAsync(id);
        if (fornecedor == null) return NotFound();
        return View(fornecedor);
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpGet]
    public async Task<IActionResult> Upsert(int? id)
    {
        if (!id.HasValue || id.Value == 0)
        {
            return View(new Fornecedor { Ativo = true });
        }

        var fornecedor = await _repository.ObterFornecedorPorIdAsync(id.Value);
        if (fornecedor == null) return NotFound();
        return View(fornecedor);
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upsert(Fornecedor model)
    {
        if (!CnpjValidator.EhValido(model.Cnpj))
        {
            ModelState.AddModelError(nameof(model.Cnpj), "CNPJ inválido. Verifique os dígitos verificadores (Módulo 11).");
        }
        else
        {
            model.Cnpj = CnpjValidator.Formatar(model.Cnpj);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ehNovo = model.Id == 0;
        var salvo = await _repository.SalvarFornecedorAsync(model);
        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            ehNovo ? "CRIAR_FORNECEDOR" : "ATUALIZAR_FORNECEDOR",
            "Fornecedor",
            $"Fornecedor '{salvo.NomeFantasia}' (CNPJ: {salvo.Cnpj}) salvo com sucesso.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = $"Fornecedor '{salvo.NomeFantasia}' salvo com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var forn = await _repository.ObterFornecedorPorIdAsync(id);
            await _repository.ExcluirFornecedorAsync(id);
            await _repository.RegistrarAuditoriaAsync(
                User.Identity?.Name ?? "usuario",
                "EXCLUIR_FORNECEDOR",
                "Fornecedor",
                $"Fornecedor #{id} ({forn?.NomeFantasia}) excluído.",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Sucesso"] = "Fornecedor removido com sucesso.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
