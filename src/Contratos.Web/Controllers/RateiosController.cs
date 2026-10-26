using Contratos.Core.Entities;
using Contratos.Core.Interfaces;
using Contratos.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contratos.Web.Controllers;

[Authorize]
public class RateiosController : Controller
{
    private readonly IClmRepository _repository;

    public RateiosController(IClmRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rateios = await _repository.ObterRateiosAsync();
        var contratos = await _repository.ObterContratosAsync();
        var contratosDict = contratos.ToDictionary(c => c.Id);

        var resumoPorDepto = new List<RateioResumoDepartamentoViewModel>();
        decimal totalCorporativo = 0m;

        foreach (var grupo in rateios.GroupBy(r => new { r.Departamento, r.CentroDeCusto }))
        {
            decimal somaAlocada = 0m;
            foreach (var item in grupo)
            {
                if (contratosDict.TryGetValue(item.ContratoId, out var c))
                {
                    somaAlocada += Math.Round(c.ValorMensalContratado * (item.Percentual / 100m), 2);
                }
            }

            totalCorporativo += somaAlocada;
            resumoPorDepto.Add(new RateioResumoDepartamentoViewModel
            {
                Departamento = grupo.Key.Departamento,
                CentroDeCusto = grupo.Key.CentroDeCusto,
                QuantidadeContratos = grupo.Select(x => x.ContratoId).Distinct().Count(),
                CustoMensalAlocado = somaAlocada
            });
        }

        foreach (var item in resumoPorDepto)
        {
            item.PercentualSobreTotalCorporativo = totalCorporativo > 0
                ? Math.Round((item.CustoMensalAlocado / totalCorporativo) * 100m, 2)
                : 0m;
        }

        ViewBag.ResumoDepartamentos = resumoPorDepto.OrderByDescending(x => x.CustoMensalAlocado).ToList();
        ViewBag.TotalCorporativoAlocado = totalCorporativo;
        ViewBag.Contratos = contratos;

        return View(rateios);
    }

    [Authorize(Policy = "FinanceiroAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RateioDepartamento model)
    {
        if (model.ContratoId <= 0 || string.IsNullOrWhiteSpace(model.Departamento) || model.Percentual <= 0 || model.Percentual > 100)
        {
            TempData["Erro"] = "Preencha corretamente o contrato, departamento, centro de custo e percentual.";
            return RedirectToAction(nameof(Index));
        }

        await _repository.SalvarRateioAsync(model);
        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            "CRIAR_RATEIO",
            "RateioDepartamento",
            $"Rateio de {model.Percentual:N2}% alocado ao departamento '{model.Departamento}' ({model.CentroDeCusto}) para o Contrato #{model.ContratoId}.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = "Alocação de rateio por centro de custos registrada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "FinanceiroAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _repository.ExcluirRateioAsync(id);
        TempData["Sucesso"] = "Regra de rateio excluída.";
        return RedirectToAction(nameof(Index));
    }
}
