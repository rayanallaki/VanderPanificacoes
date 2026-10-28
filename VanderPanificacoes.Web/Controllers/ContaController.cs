using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanderPanificacoes.Web.Models;
using VanderPanificacoes.Web.Services;

namespace VanderPanificacoes.Web.Controllers;

public sealed class ContaController(ApiClient api) : Controller
{
    [HttpGet("conta/entrar"), AllowAnonymous]
    public IActionResult Entrar(bool expirou = false)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        if (expirou) ModelState.AddModelError("", "Sua sessão expirou. Entre novamente.");
        return View(new LoginForm());
    }

    [HttpPost("conta/entrar"), AllowAnonymous]
    public async Task<IActionResult> Entrar(LoginForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        try
        {
            var login = (await api.Enviar(HttpMethod.Post, "api/autenticacao/login", new { email = form.Email.Trim(), senha = form.Senha }, ct, login: true))!.AsObject();
            var usuario = login["usuario"]!.AsObject();
            if (usuario["perfil"]?.ToString() != "Administrador")
            {
                ModelState.AddModelError("", "A área web é exclusiva para administradores. Use o aplicativo para entrar como vendedor.");
                form.Senha = ""; return View(form);
            }
            var identidade = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, usuario["id"]!.ToString()), new Claim(ClaimTypes.Name, usuario["nome"]!.ToString()), new Claim(ClaimTypes.Role, "Administrador")
            ], CookieAuthenticationDefaults.AuthenticationScheme);
            var propriedades = new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.Parse(login["expiraEm"]!.ToString()) };
            propriedades.StoreTokens([new AuthenticationToken { Name = "access_token", Value = login["token"]!.ToString() }]);
            await HttpContext.SignInAsync(new ClaimsPrincipal(identidade), propriedades);
            return RedirectToAction("Index", "Home");
        }
        catch (FalhaApi erro) { ModelState.AddModelError("", erro.Message); form.Senha = ""; return View(form); }
    }

    [HttpPost("conta/sair"), Authorize]
    public async Task<IActionResult> Sair() { await HttpContext.SignOutAsync(); return RedirectToAction(nameof(Entrar)); }
}
