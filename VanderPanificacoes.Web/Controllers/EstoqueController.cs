using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanderPanificacoes.Web.Models;
using VanderPanificacoes.Web.Services;

namespace VanderPanificacoes.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class EstoqueController(ApiClient api) : Controller
{
    [HttpGet("estoque")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await api.Lista("api/estoque/movimentacoes", ct));

    [HttpGet("estoque/novo")]
    public async Task<IActionResult> Novo(CancellationToken ct) => View(new EstoqueForm { Produtos = (await api.Lista("api/produtos", ct)).Where(Ui.Ativo).ToList() });

    [HttpPost("estoque/novo")]
    public async Task<IActionResult> Novo(EstoqueForm form, CancellationToken ct)
    {
        if (form.Tipo is not ("entradas" or "ajustes-entrada" or "ajustes-saida")) ModelState.AddModelError("", "Escolha um tipo de movimentação válido.");
        if (ModelState.IsValid)
        {
            try
            {
                await api.Enviar(HttpMethod.Post, "api/estoque/" + form.Tipo, new { identificadorOperacao = form.Identificador, produtoId = form.ProdutoId, quantidade = form.Quantidade, motivo = form.Motivo }, ct);
                TempData["Sucesso"] = "Movimentação registrada e saldo atualizado."; return RedirectToAction(nameof(Index));
            }
            catch (FalhaApi erro) when (erro.Status != System.Net.HttpStatusCode.Unauthorized) { ModelState.AddModelError("", erro.Message); }
        }
        form.Produtos = (await api.Lista("api/produtos", ct)).Where(Ui.Ativo).ToList(); return View(form);
    }
}
