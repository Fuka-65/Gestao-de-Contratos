using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contratos.Web.Controllers;

[Authorize]
public class ContratosController : Controller
{
    private readonly IClmRepository _repository;
    private readonly IWebHostEnvironment _env;

    public ContratosController(IClmRepository repository, IWebHostEnvironment env)
    {
        _repository = repository;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? busca = null,
        StatusContrato? status = null,
        CategoriaContrato? categoria = null,
        string? filtroAlerta = null)
    {
        var hoje = DateTime.Today;
        var contratos = await _repository.ObterContratosAsync();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            contratos = contratos.Where(c =>
                c.NumeroContrato.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                c.TituloObjeto.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                (c.Fornecedor?.NomeFantasia?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false) ||
                c.GestorContratoNome.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                c.DepartamentoResponsavel.Contains(termo, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (status.HasValue)
        {
            contratos = contratos.Where(c => c.Status == status.Value).ToList();
        }

        if (categoria.HasValue)
        {
            contratos = contratos.Where(c => c.CategoriaContrato == categoria.Value).ToList();
        }

        if (string.Equals(filtroAlerta, "vencendo", StringComparison.OrdinalIgnoreCase))
        {
            contratos = contratos.Where(c => c.EstaProximoVencimento(hoje)).ToList();
        }
        else if (string.Equals(filtroAlerta, "vencidos", StringComparison.OrdinalIgnoreCase))
        {
            contratos = contratos.Where(c => c.EstaVencido(hoje)).ToList();
        }

        ViewBag.Busca = busca;
        ViewBag.Status = status;
        ViewBag.Categoria = categoria;
        ViewBag.FiltroAlerta = filtroAlerta;
        return View(contratos);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var contrato = await _repository.ObterContratoPorIdAsync(id);
        if (contrato == null) return NotFound();
        return View(contrato);
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpGet]
    public async Task<IActionResult> Upsert(int? id)
    {
        ViewBag.Fornecedores = await _repository.ObterFornecedoresAsync();
        var config = await _repository.ObterConfiguracaoAsync();

        if (!id.HasValue || id.Value == 0)
        {
            return View(new Contrato
            {
                NumeroContrato = $"CTR-{DateTime.Today.Year}-{Random.Shared.Next(100, 999):D4}",
                DataAssinatura = DateTime.Today,
                DataInicioVigencia = DateTime.Today,
                DataTerminoVigencia = DateTime.Today.AddMonths(12),
                DiasAlertaVencimento = config.MargemGlobalDiasAlertaVencimento,
                DiasAvisoPrevioRescisao = 30,
                PeriodicidadeReajusteMeses = 12,
                Moeda = "BRL",
                Status = StatusContrato.Vigente
            });
        }

        var contrato = await _repository.ObterContratoPorIdAsync(id.Value);
        if (contrato == null) return NotFound();
        return View(contrato);
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upsert(Contrato model, IFormFile? arquivoPdf)
    {
        ViewBag.Fornecedores = await _repository.ObterFornecedoresAsync();

        if (model.DataTerminoVigencia < model.DataInicioVigencia)
        {
            ModelState.AddModelError(nameof(model.DataTerminoVigencia), "A data de término deve ser igual ou posterior à data de início.");
        }

        if (model.ValorGlobalContrato <= 0 && model.ValorMensalContratado > 0)
        {
            var meses = Math.Max(1, (int)Math.Round((model.DataTerminoVigencia - model.DataInicioVigencia).TotalDays / 30.0));
            model.ValorGlobalContrato = model.ValorMensalContratado * meses;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (arquivoPdf != null && arquivoPdf.Length > 0)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "contratos");
            Directory.CreateDirectory(uploadsDir);
            var safeFileName = $"{model.NumeroContrato.Replace("/", "-")}_{Guid.NewGuid():N}.pdf";
            var destPath = Path.Combine(uploadsDir, safeFileName);
            using var stream = new FileStream(destPath, FileMode.Create);
            await arquivoPdf.CopyToAsync(stream);
            model.ArquivoContratoPath = $"/uploads/contratos/{safeFileName}";
        }

        var ehNovo = model.Id == 0;
        var salvo = await _repository.SalvarContratoAsync(model);
        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            ehNovo ? "CRIAR_CONTRATO" : "ATUALIZAR_CONTRATO",
            "Contrato",
            $"Contrato {salvo.NumeroContrato} ({salvo.TituloObjeto}) salvo. Valor mensal: {salvo.ValorMensalContratado:C2}.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = $"Contrato {salvo.NumeroContrato} salvo com sucesso.";
        return RedirectToAction(nameof(Details), new { id = salvo.Id });
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdicionarAditivo(TermoAditivo aditivo, IFormFile? arquivoAditivoPdf)
    {
        if (string.IsNullOrWhiteSpace(aditivo.NumeroAditivo) || string.IsNullOrWhiteSpace(aditivo.DescricaoAlteracao))
        {
            TempData["Erro"] = "Preencha o número e a descrição do Termo Aditivo.";
            return RedirectToAction(nameof(Details), new { id = aditivo.ContratoId });
        }

        if (arquivoAditivoPdf != null && arquivoAditivoPdf.Length > 0)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "aditivos");
            Directory.CreateDirectory(uploadsDir);
            var safeFileName = $"TA_{aditivo.ContratoId}_{Guid.NewGuid():N}.pdf";
            var destPath = Path.Combine(uploadsDir, safeFileName);
            using var stream = new FileStream(destPath, FileMode.Create);
            await arquivoAditivoPdf.CopyToAsync(stream);
            aditivo.ArquivoAditivoPath = $"/uploads/aditivos/{safeFileName}";
        }

        await _repository.AdicionarTermoAditivoAsync(aditivo);
        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            "ADICIONAR_ADITIVO",
            "TermoAditivo",
            $"Termo Aditivo '{aditivo.NumeroAditivo}' vinculado ao Contrato #{aditivo.ContratoId}.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = $"Termo Aditivo '{aditivo.NumeroAditivo}' registrado e vigência/valor contratual atualizados.";
        return RedirectToAction(nameof(Details), new { id = aditivo.ContratoId });
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdicionarRateio(RateioDepartamento rateio)
    {
        if (string.IsNullOrWhiteSpace(rateio.Departamento) || rateio.Percentual <= 0 || rateio.Percentual > 100)
        {
            TempData["Erro"] = "Informe departamento, centro de custo e percentual válido (1% a 100%).";
            return RedirectToAction(nameof(Details), new { id = rateio.ContratoId });
        }

        await _repository.SalvarRateioAsync(rateio);
        TempData["Sucesso"] = $"Rateio de {rateio.Percentual:N2}% para {rateio.Departamento} adicionado.";
        return RedirectToAction(nameof(Details), new { id = rateio.ContratoId });
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExcluirRateio(int id, int contratoId)
    {
        await _repository.ExcluirRateioAsync(id);
        TempData["Sucesso"] = "Item de rateio removido.";
        return RedirectToAction(nameof(Details), new { id = contratoId });
    }

    [Authorize(Policy = "GestorContratosAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _repository.ObterContratoPorIdAsync(id);
        await _repository.ExcluirContratoAsync(id);
        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            "EXCLUIR_CONTRATO",
            "Contrato",
            $"Contrato #{id} ({c?.NumeroContrato}) excluído.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = "Contrato excluído com sucesso.";
        return RedirectToAction(nameof(Index));
    }
}
