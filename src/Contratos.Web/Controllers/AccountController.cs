using System.Security.Claims;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Contratos.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contratos.Web.Controllers;

public class AccountController : Controller
{
    private readonly Contratos.Infrastructure.Security.AuthenticationService _authService;
    private readonly IClmRepository _repository;

    public AccountController(Contratos.Infrastructure.Security.AuthenticationService authService, IClmRepository repository)
    {
        _authService = authService;
        _repository = repository;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var config = await _repository.ObterConfiguracaoAsync();
        ViewBag.Config = config;
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string login, string senha, string? returnUrl = null)
    {
        var config = await _repository.ObterConfiguracaoAsync();
        ViewBag.Config = config;
        ViewBag.ReturnUrl = returnUrl;

        var resultado = await _authService.AutenticarAsync(login, senha);
        if (!resultado.Sucesso || resultado.Usuario == null)
        {
            TempData["ErroLogin"] = resultado.MensagemErro;
            return View();
        }

        var usuario = resultado.Usuario;
        var papelMaisPermissivo = usuario.ObterPapelMaisPermissivo();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Login),
            new(ClaimTypes.Email, usuario.Email),
            new("FullName", usuario.NomeCompleto),
            new("Department", usuario.Departamento ?? "Corporativo"),
            new("JobTitle", usuario.Cargo ?? "Colaborador"),
            new("MostPermissiveRole", papelMaisPermissivo.ObterNomeExibicao()),
            new("AllRolesDisplay", string.Join(" + ", usuario.Papeis.Select(p => p.ObterNomeExibicao()))),
            new("AuthOrigin", resultado.OrigemAutenticacao)
        };

        // Adiciona todos os papéis acumulados e garante que a regra do mais permissivo libere as políticas hierárquicas
        foreach (var role in usuario.Papeis)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        if (usuario.EhAdminImune() && !claims.Any(c => c.Type == ClaimTypes.Role && c.Value == nameof(UserRole.Master)))
        {
            claims.Add(new Claim(ClaimTypes.Role, nameof(UserRole.Master)));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        await _repository.RegistrarAuditoriaAsync(
            usuario.Login,
            "LOGIN_SUCESSO",
            "Autenticacao",
            $"Autenticação realizada com sucesso via {resultado.OrigemAutenticacao}. Papel prevalente: {papelMaisPermissivo}.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userLogin = User.Identity?.Name ?? "desconhecido";
        await _repository.RegistrarAuditoriaAsync(
            userLogin,
            "LOGOUT",
            "Autenticacao",
            "Sessão encerrada pelo usuário.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
