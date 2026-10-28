using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanderPanificacoes.Web.Models;
using VanderPanificacoes.Web.Services;

namespace VanderPanificacoes.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class HomeController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var lotes = api.Lista("api/lotes", ct);
        var parcelas = api.Lista("api/parcelas", ct);
        var produtos = api.Lista("api/produtos", ct);
        await Task.WhenAll(lotes, parcelas, produtos);
        return View(new PainelVm(await lotes, await parcelas, await produtos));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View("~/Views/Shared/Falha.cshtml", "Não foi possível carregar esta página. Tente novamente.");
    }
}
