using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using VanderPanificacoes.Web.Models;
using VanderPanificacoes.Web.Services;

namespace VanderPanificacoes.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class LotesController(ApiClient api) : Controller
{
    [HttpGet("lotes")]
    public async Task<IActionResult> Index(string? situacao, CancellationToken ct)
    {
        var registros = await api.Lista("api/lotes", ct);
        if (!string.IsNullOrEmpty(situacao)) registros = registros.Where(l => Ui.Texto(l, "situacao") == situacao).ToList();
        ViewData["Situacao"] = situacao;
        return View(registros);
    }

    [HttpGet("lotes/{id:int}")]
    public async Task<IActionResult> Detalhes(int id, CancellationToken ct) => View(await Preparar(id, null, null, ct));

    [HttpPost("lotes/{id:int}/conferir")]
    public async Task<IActionResult> Conferir(int id, ConferenciaForm form, CancellationToken ct)
    {
        JsonObject? previa = null;
        if (ModelState.IsValid)
        {
            try { previa = (await api.Enviar(HttpMethod.Post, $"api/lotes/{id}/conferencia", new { itens = form.Itens }, ct))!.AsObject(); }
            catch (FalhaApi erro) when (erro.Status != System.Net.HttpStatusCode.Unauthorized) { ModelState.AddModelError("", erro.Message); }
        }
        return View("Detalhes", await Preparar(id, form.Itens, previa, ct));
    }

    [HttpPost("lotes/{id:int}/revisar")]
    public async Task<IActionResult> Revisar(int id, ConferenciaForm form, CancellationToken ct) => View("Detalhes", await Preparar(id, form.Itens, null, ct));

    [HttpPost("lotes/{id:int}/confirmar")]
    public async Task<IActionResult> Confirmar(int id, ConferenciaForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await api.Enviar(HttpMethod.Post, $"api/lotes/{id}/confirmacao", new { itens = form.Itens }, ct);
                TempData["Sucesso"] = "Lote confirmado. Vendas e parcelas foram geradas e o estoque foi atualizado.";
                return RedirectToAction(nameof(Detalhes), new { id });
            }
            catch (FalhaApi erro) when (erro.Status != System.Net.HttpStatusCode.Unauthorized) { ModelState.AddModelError("", erro.Message); }
        }
        return View("Detalhes", await Preparar(id, form.Itens, null, ct));
    }

    [HttpGet("lotes/{id:int}/cancelar")]
    public IActionResult Cancelar(int id) => View(new CancelamentoForm { Id = id });

    [HttpPost("lotes/{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id, CancelamentoForm form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                await api.Enviar(HttpMethod.Post, $"api/lotes/{id}/cancelamento", new { identificadorCancelamento = form.Identificador, motivo = form.Motivo }, ct);
                TempData["Sucesso"] = "Lote cancelado sem baixa de estoque."; return RedirectToAction(nameof(Index));
            }
            catch (FalhaApi erro) when (erro.Status != System.Net.HttpStatusCode.Unauthorized) { ModelState.AddModelError("", erro.Message); }
        }
        return View(form);
    }

    private async Task<LoteVm> Preparar(int id, List<ItemForm>? itens, JsonObject? previa, CancellationToken ct)
    {
        var detalhes = await api.Objeto($"api/lotes/{id}", ct);
        if (itens is null)
        {
            itens = [];
            if (detalhes["lote"]?["situacao"]?.ToString() == "Recebido")
            {
                var sugestao = await api.Objeto($"api/lotes/{id}/sugestao", ct);
                foreach (var pedido in sugestao["pedidos"]!.AsArray())
                    foreach (var item in pedido!["itens"]!.AsArray()) itens.Add(new ItemForm { ItemPedidoId = item!["itemPedidoId"]!.GetValue<int>(), QuantidadeAtendida = item["quantidadeSugerida"]!.GetValue<int>() });
            }
        }
        return new LoteVm(detalhes, itens, previa);
    }
}
