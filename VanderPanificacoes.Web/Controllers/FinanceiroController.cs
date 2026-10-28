using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanderPanificacoes.Web.Models;
using VanderPanificacoes.Web.Services;

namespace VanderPanificacoes.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class FinanceiroController(ApiClient api) : Controller
{
    [HttpGet("vendas")]
    public async Task<IActionResult> Vendas(CancellationToken ct) => View(await api.Lista("api/vendas", ct));

    [HttpGet("parcelas")]
    public async Task<IActionResult> Parcelas(string? situacao, CancellationToken ct)
    {
        var rota = "api/parcelas" + (string.IsNullOrEmpty(situacao) ? "" : "?situacao=" + Uri.EscapeDataString(situacao));
        ViewData["Situacao"] = situacao;
        return View(await api.Lista(rota, ct));
    }

    [HttpGet("parcelas/{id:int}/receber")]
    public async Task<IActionResult> Receber(int id, CancellationToken ct)
    {
        var parcela = (await api.Lista("api/parcelas", ct)).Find(p => Ui.Id(p) == id);
        if (parcela is null) return NotFound();
        return View(new RecebimentoForm { Id = id, Parcela = parcela, FormaPagamento = Ui.Texto(parcela, "formaPagamentoPrevista") });
    }

    [HttpPost("parcelas/{id:int}/receber")]
    public async Task<IActionResult> Receber(int id, RecebimentoForm form, CancellationToken ct)
    {
        var parcela = (await api.Lista("api/parcelas", ct)).Find(p => Ui.Id(p) == id);
        if (parcela is null) return NotFound();
        form.Id = id; form.Parcela = parcela;
        if (ModelState.IsValid)
        {
            try
            {
                await api.Enviar(HttpMethod.Post, $"api/parcelas/{id}/recebimento", new { identificadorRecebimento = form.Identificador, dataRecebimento = form.DataRecebimento, formaPagamentoRecebida = form.FormaPagamento, valorRecebido = Ui.Valor(parcela, "valorBase") }, ct);
                TempData["Sucesso"] = "Recebimento registrado. A parcela está paga."; return RedirectToAction(nameof(Parcelas));
            }
            catch (FalhaApi erro) when (erro.Status != System.Net.HttpStatusCode.Unauthorized) { ModelState.AddModelError("", erro.Message); }
        }
        return View(form);
    }
}
