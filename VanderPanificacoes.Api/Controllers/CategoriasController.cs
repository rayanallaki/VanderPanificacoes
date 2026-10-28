using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VanderPanificacoes.Api.Data;
using VanderPanificacoes.Api.Models;

namespace VanderPanificacoes.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador,Vendedor")]
[Route("api/[controller]")]
public class CategoriasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var consulta = db.Categorias.AsNoTracking();
        if (User.IsInRole("Vendedor"))
            consulta = consulta.Where(categoria => categoria.Ativo);
        var categorias = await consulta
            .OrderBy(categoria => categoria.Nome)
            .Select(categoria => new { categoria.Id, categoria.Nome, categoria.Ativo })
            .ToListAsync(cancellationToken);

        return Ok(categorias);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Criar(CriarCategoriaRequest request, CancellationToken cancellationToken)
    {
        var nome = request.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return BadRequest(new { mensagem = "Informe o nome da categoria." });

        var categoria = new Categoria { Nome = nome, Ativo = true };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new { categoria.Id, categoria.Nome, categoria.Ativo });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Editar(int id, CriarCategoriaRequest request, CancellationToken cancellationToken)
    {
        var nome = request.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return BadRequest(new { mensagem = "Informe o nome da categoria." });

        var categoria = await db.Categorias.FindAsync([id], cancellationToken);
        if (categoria is null)
            return NotFound(new { mensagem = "Categoria não encontrada." });

        categoria.Nome = nome;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { categoria.Id, categoria.Nome, categoria.Ativo });
    }

    [HttpPatch("{id:int}/desativar")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Desativar(int id, CancellationToken cancellationToken)
    {
        var categoria = await db.Categorias.FindAsync([id], cancellationToken);
        if (categoria is null)
            return NotFound(new { mensagem = "Categoria não encontrada." });

        categoria.Ativo = false;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { categoria.Id, categoria.Nome, categoria.Ativo });
    }
}

public class CriarCategoriaRequest
{
    [Required(ErrorMessage = "Informe o nome da categoria.")]
    [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
    public string Nome { get; set; } = string.Empty;
}
