using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contratos.Web.Controllers;

[Authorize]
public class FaturasController : Controller
{
    private readonly IClmRepository _repository;
    private readonly IWebHostEnvironment _env;

    public FaturasController(IClmRepository repository, IWebHostEnvironment env)
    {
        _repository = repository;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool apenasDesvios = false, StatusFatura? status = null, int? contratoId = null)
    {
        var faturas = await _repository.ObterFaturasAsync();

        if (apenasDesvios)
        {
            faturas = faturas.Where(f => f.PossuiDesvio).ToList();
        }

        if (status.HasValue)
        {
            faturas = faturas.Where(f => f.Status == status.Value).ToList();
        }

        if (contratoId.HasValue)
        {
            faturas = faturas.Where(f => f.ContratoId == contratoId.Value).ToList();
        }

        ViewBag.ApenasDesvios = apenasDesvios;
        ViewBag.Status = status;
        ViewBag.ContratoId = contratoId;
        ViewBag.Contratos = await _repository.ObterContratosAsync();
        return View(faturas);
    }

    [Authorize(Policy = "FinanceiroAccess")]
    [HttpGet]
    public async Task<IActionResult> Upsert(int? id, int? contratoId = null)
    {
        var contratos = await _repository.ObterContratosAsync();
        ViewBag.Contratos = contratos;

        if (!id.HasValue || id.Value == 0)
        {
            var contratoSelecionado = contratoId.HasValue
                ? contratos.FirstOrDefault(c => c.Id == contratoId.Value)
                : contratos.FirstOrDefault();

            return View(new FaturaGasto
            {
                ContratoId = contratoSelecionado?.Id ?? 0,
                FornecedorId = contratoSelecionado?.FornecedorId ?? 0,
                MesReferencia = DateTime.Today.Month,
                AnoReferencia = DateTime.Today.Year,
                ValorContratado = contratoSelecionado?.ValorMensalContratado ?? 0m,
                ValorCobrado = contratoSelecionado?.ValorMensalContratado ?? 0m,
                DataVencimento = DateTime.Today.AddDays(10),
                NumeroNotaFiscal = $"NFS-{DateTime.Today:yyyyMM}-{Random.Shared.Next(100, 999)}",
                Status = StatusFatura.Pendente
            });
        }

        var fatura = await _repository.ObterFaturaPorIdAsync(id.Value);
        if (fatura == null) return NotFound();
        return View(fatura);
    }

    [Authorize(Policy = "FinanceiroAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upsert(FaturaGasto model, IFormFile? arquivoFaturaPdf)
    {
        var contratos = await _repository.ObterContratosAsync();
        ViewBag.Contratos = contratos;

        var contrato = contratos.FirstOrDefault(c => c.Id == model.ContratoId);
        if (contrato != null)
        {
            model.FornecedorId = contrato.FornecedorId;
            if (model.ValorContratado <= 0)
            {
                model.ValorContratado = contrato.ValorMensalContratado;
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (arquivoFaturaPdf != null && arquivoFaturaPdf.Length > 0)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "faturas");
            Directory.CreateDirectory(uploadsDir);
            var safeFileName = $"FAT_{model.NumeroNotaFiscal.Replace("/", "-")}_{Guid.NewGuid():N}.pdf";
            var destPath = Path.Combine(uploadsDir, safeFileName);
            using var stream = new FileStream(destPath, FileMode.Create);
            await arquivoFaturaPdf.CopyToAsync(stream);
            model.ArquivoFaturaPath = $"/uploads/faturas/{safeFileName}";
        }

        var salvo = await _repository.SalvarFaturaAsync(model);
        var msgAuditoria = salvo.PossuiDesvio
            ? $"Fatura {salvo.NumeroNotaFiscal} registrada COM DESVIO de {salvo.ValorDesvio:C2} (+{salvo.PorcentagemDesvio:N2}%)."
            : $"Fatura {salvo.NumeroNotaFiscal} registrada em conformidade com o valor contratado ({salvo.ValorContratado:C2}).";

        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            salvo.PossuiDesvio ? "FATURA_COM_DESVIO" : "FATURA_REGISTRADA",
            "FaturaGasto",
            msgAuditoria,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (salvo.PossuiDesvio)
        {
            TempData["AlertaDesvio"] = $"ALERTA DE AUDITORIA: A fatura {salvo.NumeroNotaFiscal} apresenta valor cobrado ({salvo.ValorCobrado:C2}) superior ao contrato ({salvo.ValorContratado:C2}), gerando desvio de {salvo.ValorDesvio:C2} (+{salvo.PorcentagemDesvio:N2}%).";
        }
        else
        {
            TempData["Sucesso"] = $"Fatura {salvo.NumeroNotaFiscal} auditada e salva com sucesso.";
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "FinanceiroAccess")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtualizarStatus(int id, StatusFatura novoStatus, string? justificativa)
    {
        var fatura = await _repository.ObterFaturaPorIdAsync(id);
        if (fatura == null) return NotFound();

        fatura.Status = novoStatus;
        if (novoStatus == StatusFatura.Paga)
        {
            fatura.DataPagamento = DateTime.Today;
            fatura.ValorPago = fatura.ValorCobrado;
        }

        if (!string.IsNullOrWhiteSpace(justificativa))
        {
            fatura.Observacoes = justificativa;
        }

        await _repository.SalvarFaturaAsync(fatura);
        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "usuario",
            "STATUS_FATURA_ALTERADO",
            "FaturaGasto",
            $"Fatura {fatura.NumeroNotaFiscal} alterada para status {novoStatus.ObterNomeExibicao()}.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = $"Status da fatura {fatura.NumeroNotaFiscal} atualizado para {novoStatus.ObterNomeExibicao()}.";
        return RedirectToAction(nameof(Index));
    }
}
