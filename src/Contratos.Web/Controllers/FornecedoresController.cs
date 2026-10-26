using Contratos.Core.Entities;
using Contratos.Core.Interfaces;
using Contratos.Core.Validators;
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
