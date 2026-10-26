using System.Text.RegularExpressions;
using Contratos.Core.Entities;
using Contratos.Core.Enums;
using Contratos.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Contratos.Web.Controllers;

[Authorize(Policy = "MasterOnly")]
public class AdminController : Controller
{
    private readonly IClmRepository _repository;
    private readonly ICryptoService _cryptoService;
    private readonly ILdapService _ldapService;
    private readonly IWebHostEnvironment _env;

    public AdminController(
        IClmRepository repository,
        ICryptoService cryptoService,
        ILdapService ldapService,
        IWebHostEnvironment env)
    {
        _repository = repository;
        _cryptoService = cryptoService;
        _ldapService = ldapService;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string aba = "empresa")
    {
        var config = await _repository.ObterConfiguracaoAsync();
        var usuarios = await _repository.ObterUsuariosAsync();
        var logs = await _repository.ObterLogsAuditoriaRecentesAsync(50);

        ViewBag.AbaAtiva = aba;
        ViewBag.Usuarios = usuarios;
        ViewBag.Logs = logs;
        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarEmpresa(ConfiguracaoSistema model, IFormFile? logoUpload)
    {
        var atual = await _repository.ObterConfiguracaoAsync();

        atual.RazaoSocialEmpresa = model.RazaoSocialEmpresa;
        atual.NomeFantasiaEmpresa = model.NomeFantasiaEmpresa;
        atual.CnpjEmpresa = model.CnpjEmpresa;
        atual.SplashAtivo = model.SplashAtivo;
        atual.SplashTempoSegundos = Math.Clamp(model.SplashTempoSegundos, 1, 15);
        atual.SplashTitulo = model.SplashTitulo;
        atual.SplashSlogan = model.SplashSlogan;
        atual.MargemGlobalDiasAlertaVencimento = Math.Clamp(model.MargemGlobalDiasAlertaVencimento, 15, 180);
        atual.AliquotaPadraoImpostosPercentual = Math.Clamp(model.AliquotaPadraoImpostosPercentual, 0m, 99.99m);
        atual.ModalidadeOrcamentoPadrao = model.ModalidadeOrcamentoPadrao;
        atual.TaxaInflacaoSimuladaPadrao = Math.Clamp(model.TaxaInflacaoSimuladaPadrao, 0m, 50m);

        if (logoUpload != null && logoUpload.Length > 0)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "logos");
            Directory.CreateDirectory(uploadsDir);
            var ext = Path.GetExtension(logoUpload.FileName);
            var fileName = $"logo_corp_{Guid.NewGuid():N}{ext}";
            var dest = Path.Combine(uploadsDir, fileName);
            using var stream = new FileStream(dest, FileMode.Create);
            await logoUpload.CopyToAsync(stream);
            atual.LogoEmpresaPath = $"/uploads/logos/{fileName}";
        }

        await _repository.SalvarConfiguracaoAsync(atual);
        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "admin@admin",
            "ATUALIZAR_PARAMETROS_EMPRESA",
            "ConfiguracaoSistema",
            $"Parâmetros institucionais e Splash Screen atualizados ({atual.NomeFantasiaEmpresa}).",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = "Configurações institucionais e parâmetros de alerta salvos com sucesso.";
        return RedirectToAction(nameof(Index), new { aba = "empresa" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarLdap(ConfiguracaoSistema model, string? novaSenhaLdap)
    {
        var atual = await _repository.ObterConfiguracaoAsync();

        atual.LdapAtivo = model.LdapAtivo;
        atual.LdapDominio = model.LdapDominio;
        atual.LdapServidor = model.LdapServidor;
        atual.LdapPorta = model.LdapPorta > 0 ? model.LdapPorta : 389;
        atual.LdapBaseDn = model.LdapBaseDn;
        atual.LdapUsuarioLeitura = model.LdapUsuarioLeitura;

        if (!string.IsNullOrWhiteSpace(novaSenhaLdap))
        {
            atual.LdapSenhaLeituraEncrypted = _cryptoService.EncryptWithToken(novaSenhaLdap);
        }

        await _repository.SalvarConfiguracaoAsync(atual);
        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "admin@admin",
            "ATUALIZAR_CONFIG_LDAP",
            "ConfiguracaoSistema",
            $"Integração LDAP/AD configurada (Servidor: {atual.LdapServidor}, Ativo: {atual.LdapAtivo}). Senha armazenada com token AES-256.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = "Configurações de Active Directory (LDAP) criptografadas com AES-256 e salvas com sucesso.";
        return RedirectToAction(nameof(Index), new { aba = "ldap" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestarLdap([FromForm] string? servidor, [FromForm] int porta, [FromForm] string? dominio, [FromForm] string? usuario)
    {
        var config = await _repository.ObterConfiguracaoAsync();
        if (!string.IsNullOrWhiteSpace(servidor)) config.LdapServidor = servidor;
        if (porta > 0) config.LdapPorta = porta;
        if (!string.IsNullOrWhiteSpace(dominio)) config.LdapDominio = dominio;
        if (!string.IsNullOrWhiteSpace(usuario)) config.LdapUsuarioLeitura = usuario;

        var resultado = await _ldapService.TestarConexaoAsync(config);
        return Json(new
        {
            sucesso = resultado.Sucesso,
            mensagem = resultado.Mensagem,
            latenciaMs = resultado.LatenciaMs
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SincronizarLdap()
    {
        var config = await _repository.ObterConfiguracaoAsync();
        var colaboradoresAd = await _ldapService.SincronizarColaboradoresAsync(config);
        var usuariosExistentes = await _repository.ObterUsuariosAsync();
        int novosImportados = 0;

        foreach (var colab in colaboradoresAd)
        {
            if (!usuariosExistentes.Any(u => string.Equals(u.Login, colab.Login, StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(u.Email, colab.Email, StringComparison.OrdinalIgnoreCase)))
            {
                var novo = new Usuario
                {
                    NomeCompleto = colab.NomeCompleto,
                    Email = colab.Email,
                    Login = colab.Login,
                    Departamento = colab.Departamento,
                    Cargo = colab.Cargo,
                    SenhaHash = _cryptoService.HashPassword("AdSync@2026!"),
                    PapeisCsv = ((int)UserRole.GestorContratos).ToString(),
                    Ativo = true,
                    AutenticacaoAd = true
                };
                await _repository.SalvarUsuarioAsync(novo);
                novosImportados++;
            }
        }

        await _repository.RegistrarAuditoriaAsync(
            User.Identity?.Name ?? "admin@admin",
            "SINCRONIZAR_COLABORADORES_LDAP",
            "Usuario",
            $"Sincronização Active Directory concluída ({colaboradoresAd.Count} lidos, {novosImportados} novos importados).",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Sucesso"] = $"Sincronização LDAP concluída: {colaboradoresAd.Count} colaboradores processados ({novosImportados} novos importados).";
        return RedirectToAction(nameof(Index), new { aba = "ldap" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarUsuario(
        int id,
        string nomeCompleto,
        string email,
        string login,
        string? departamento,
        string? cargo,
        string? senha,
        bool ativo,
        bool autenticacaoAd,
        List<int>? papeisSelecionados)
    {
        try
        {
            var existente = id > 0
                ? await _repository.ObterUsuarioPorIdAsync(id)
                : await _repository.ObterUsuarioPorLoginAsync(login);

            if (existente != null && existente.EhAdminImune())
            {
                // Para admin@admin, apenas redefinição de senha é permitida
                if (string.IsNullOrWhiteSpace(senha))
                {
                    TempData["Erro"] = "O usuário admin@admin possui imunidade perpétua. Seus papéis, login e status ativo não podem ser alterados (apenas redefinição de senha é permitida).";
                    return RedirectToAction(nameof(Index), new { aba = "usuarios" });
                }

                if (!ValidarSenhaForte(senha))
                {
                    TempData["Erro"] = "A nova senha do admin@admin deve atender ao padrão forte (mínimo 8 caracteres, maiúscula, minúscula, número e símbolo).";
                    return RedirectToAction(nameof(Index), new { aba = "usuarios" });
                }

                existente.SenhaHash = _cryptoService.HashPassword(senha);
                existente.GarantirIntegridadeAdmin();
                await _repository.SalvarUsuarioAsync(existente);
                TempData["Sucesso"] = "Senha do administrador perpétuo (admin@admin) redefinida com sucesso.";
                return RedirectToAction(nameof(Index), new { aba = "usuarios" });
            }

            if (papeisSelecionados == null || papeisSelecionados.Count == 0)
            {
                TempData["Erro"] = "Selecione pelo menos um papel de acesso (RBAC) para o usuário.";
                return RedirectToAction(nameof(Index), new { aba = "usuarios" });
            }

            var roles = papeisSelecionados
                .Where(r => Enum.IsDefined(typeof(UserRole), r))
                .Select(r => (UserRole)r)
                .ToList();

            var usuario = existente ?? new Usuario();
            usuario.NomeCompleto = nomeCompleto;
            usuario.Email = email;
            usuario.Login = login;
            usuario.Departamento = departamento;
            usuario.Cargo = cargo;
            usuario.Ativo = ativo;
            usuario.AutenticacaoAd = autenticacaoAd;
            usuario.DefinirPapeis(roles);

            if (existente == null || !string.IsNullOrWhiteSpace(senha))
            {
                if (string.IsNullOrWhiteSpace(senha) || !ValidarSenhaForte(senha))
                {
                    TempData["Erro"] = "A senha deve possuir no mínimo 8 caracteres, contendo letras maiúsculas, minúsculas, números e caracteres especiais.";
                    return RedirectToAction(nameof(Index), new { aba = "usuarios" });
                }
                usuario.SenhaHash = _cryptoService.HashPassword(senha);
            }

            await _repository.SalvarUsuarioAsync(usuario);
            await _repository.RegistrarAuditoriaAsync(
                User.Identity?.Name ?? "admin@admin",
                existente == null ? "CRIAR_USUARIO" : "ATUALIZAR_USUARIO",
                "Usuario",
                $"Usuário '{usuario.Login}' salvo com papéis [{string.Join(", ", usuario.Papeis)}]. Papel prevalente: {usuario.ObterPapelMaisPermissivo()}.",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Sucesso"] = $"Usuário '{usuario.NomeCompleto}' salvo com sucesso. Papel mais permissivo ativo: {usuario.ObterPapelMaisPermissivo().ObterNomeExibicao()}.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { aba = "usuarios" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExcluirUsuario(int id)
    {
        try
        {
            var alvo = await _repository.ObterUsuarioPorIdAsync(id);
            await _repository.ExcluirUsuarioAsync(id, User.Identity?.Name ?? "admin@admin");
            await _repository.RegistrarAuditoriaAsync(
                User.Identity?.Name ?? "admin@admin",
                "EXCLUIR_USUARIO",
                "Usuario",
                $"Usuário '{alvo?.Login}' excluído do sistema.",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Sucesso"] = "Usuário removido com sucesso.";
        }
        catch (Exception ex)
        {
            TempData["Erro"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { aba = "usuarios" });
    }

    private static bool ValidarSenhaForte(string senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < 8)
            return false;

        return Regex.IsMatch(senha, "[A-Z]") &&
               Regex.IsMatch(senha, "[a-z]") &&
               Regex.IsMatch(senha, "[0-9]") &&
               Regex.IsMatch(senha, "[^a-zA-Z0-9]");
    }
}
