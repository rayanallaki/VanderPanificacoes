using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanderPanificacoes.Web.Models;
using VanderPanificacoes.Web.Services;

namespace VanderPanificacoes.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class CadastrosController(ApiClient api) : Controller
{
    [HttpGet("cadastros/{tipo}")]
    public async Task<IActionResult> Index(string tipo, string? busca, CancellationToken ct)
    {
        if (!Cadastros.Titulos.TryGetValue(tipo, out var titulo)) return NotFound();
        var registros = await api.Lista("api/" + tipo, ct);
        if (!string.IsNullOrWhiteSpace(busca)) registros = registros.Where(r => r.Any(v => v.Value?.ToString().Contains(busca, StringComparison.OrdinalIgnoreCase) == true)).ToList();
        return View(new ListaVm(tipo, titulo, registros, busca));
    }

    [HttpGet("cadastros/{tipo}/novo")]
    [HttpGet("cadastros/{tipo}/{id:int}/editar")]
    public async Task<IActionResult> Formulario(string tipo, int? id, CancellationToken ct)
    {
        if (!Cadastros.Titulos.ContainsKey(tipo)) return NotFound();
        var form = new CadastroForm { Tipo = tipo, Id = id };
        if (id.HasValue)
        {
            var registro = (await api.Lista("api/" + tipo, ct)).Find(r => Ui.Id(r) == id);
            if (registro is null) return NotFound();
            form.Dados = registro.ToDictionary(x => x.Key, x => x.Value?.ToString());
        }
        else form.Dados["prioridadeVisita"] = "1";
        await Preparar(form, ct); return View(form);
    }

    [HttpPost("cadastros/{tipo}/salvar")]
    public async Task<IActionResult> Salvar(string tipo, CadastroForm form, CancellationToken ct)
    {
        if (!Cadastros.Titulos.ContainsKey(tipo)) return NotFound();
        form.Tipo = tipo;
        try
        {
            var corpo = Cadastros.Corpo(form);
            await api.Enviar(form.Id.HasValue ? HttpMethod.Put : HttpMethod.Post, $"api/{tipo}" + (form.Id.HasValue ? $"/{form.Id}" : ""), corpo, ct);
            TempData["Sucesso"] = "Cadastro salvo com sucesso."; return RedirectToAction(nameof(Index), new { tipo });
        }
        catch (Exception erro) when (erro is FalhaApi or ArgumentException)
        {
            if (erro is FalhaApi { Status: System.Net.HttpStatusCode.Unauthorized }) throw;
            ModelState.AddModelError("", erro.Message); form.Dados.Remove("senha");
            await Preparar(form, ct); return View("Formulario", form);
        }
    }

    [HttpPost("cadastros/{tipo}/{id:int}/situacao")]
    public async Task<IActionResult> Situacao(string tipo, int id, bool reativar, CancellationToken ct)
    {
        if (!Cadastros.Titulos.ContainsKey(tipo) || (reativar && tipo != "clientes")) return NotFound();
        await api.Enviar(HttpMethod.Patch, $"api/{tipo}/{id}/" + (reativar ? "reativar" : "desativar"), null, ct);
        TempData["Sucesso"] = reativar ? "Cliente reativado." : "Cadastro desativado. O histórico foi preservado.";
        return RedirectToAction(nameof(Index), new { tipo });
    }

    private async Task Preparar(CadastroForm form, CancellationToken ct)
    {
        form.Campos = Cadastros.Campos(form.Tipo, form.Id.HasValue);
        if (form.Tipo == "produtos")
        {
            form.Opcoes["categoriaId"] = (await api.Lista("api/categorias", ct)).Where(c => Ui.Ativo(c) || Ui.Id(c).ToString() == form.Dados.GetValueOrDefault("categoriaId")).Select(c => new Opcao(Ui.Id(c).ToString(), Ui.Texto(c, "nome"))).ToList();
            form.Opcoes["unidadeVenda"] = [new("Unidade", "Unidade"), new("Pacote", "Pacote"), new("Caixa", "Caixa")];
        }
        if (form.Tipo == "clientes") form.Opcoes["vendedorId"] = (await api.Lista("api/vendedores", ct)).Where(v => Ui.Ativo(v) || Ui.Id(v).ToString() == form.Dados.GetValueOrDefault("vendedorId")).Select(v => new Opcao(Ui.Id(v).ToString(), Ui.Texto(v, "nome"))).ToList();
    }
}
