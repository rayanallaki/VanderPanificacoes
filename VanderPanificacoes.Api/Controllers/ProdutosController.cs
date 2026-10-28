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
public class ProdutosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? nome, [FromQuery] int? categoriaId, [FromQuery] string? validade, CancellationToken cancellationToken)
    {
        var consulta = db.Produtos.AsNoTracking().AsQueryable();
        if (User.IsInRole("Vendedor"))
            consulta = consulta.Where(produto => produto.Ativo && produto.Categoria.Ativo);

        if (!string.IsNullOrWhiteSpace(nome))
        {
            var termo = nome.Trim();
            consulta = consulta.Where(produto => produto.Nome.Contains(termo));
        }

        if (categoriaId.HasValue)
        {
            if (categoriaId.Value <= 0)
                return BadRequest(new { mensagem = "O ID da categoria deve ser maior que zero." });
            consulta = consulta.Where(produto => produto.CategoriaId == categoriaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(validade))
        {
            var fusoLoja = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fusoLoja));

            if (validade.Equals("vencidos", StringComparison.OrdinalIgnoreCase))
            {
                consulta = consulta.Where(produto => produto.DataValidade != null && produto.DataValidade < hoje);
            }
            else if (validade is "7" or "15" or "30")
            {
                var limite = hoje.AddDays(int.Parse(validade));
                consulta = consulta.Where(produto => produto.DataValidade != null && produto.DataValidade >= hoje && produto.DataValidade <= limite);
            }
            else
            {
                return BadRequest(new { mensagem = "Use vencidos, 7, 15 ou 30 no filtro de validade." });
            }
        }

        var produtos = await consulta
            .OrderBy(produto => produto.Nome)
            .Select(produto => new
            {
                produto.Id,
                produto.Nome,
                produto.Descricao,
                produto.CategoriaId,
                Categoria = produto.Categoria.Nome,
                produto.UnidadeVenda,
                produto.Preco,
                produto.Estoque,
                produto.Perecivel,
                produto.DataValidade,
                produto.Ativo
            })
            .ToListAsync(cancellationToken);

        return Ok(produtos);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Criar(CriarProdutoRequest request, CancellationToken cancellationToken)
    {
        var erro = await ValidarProduto(request, cancellationToken);
        if (erro is not null)
            return BadRequest(new { mensagem = erro });

        var produto = new Produto
        {
            Nome = request.Nome.Trim(),
            Descricao = request.Descricao?.Trim(),
            CategoriaId = request.CategoriaId!.Value,
            UnidadeVenda = request.UnidadeVenda,
            Preco = request.Preco!.Value,
            Estoque = 0,
            Perecivel = request.Perecivel!.Value,
            DataValidade = request.DataValidade,
            Ativo = true
        };
        db.Produtos.Add(produto);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            produto.Id, produto.Nome, produto.Descricao, produto.CategoriaId,
            produto.UnidadeVenda, produto.Preco, produto.Estoque, produto.Perecivel,
            produto.DataValidade, produto.Ativo
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Editar(int id, CriarProdutoRequest request, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos.FindAsync([id], cancellationToken);
        if (produto is null)
            return NotFound(new { mensagem = "Produto não encontrado." });

        var erro = await ValidarProduto(request, cancellationToken);
        if (erro is not null)
            return BadRequest(new { mensagem = erro });

        produto.Nome = request.Nome.Trim();
        produto.Descricao = request.Descricao?.Trim();
        produto.CategoriaId = request.CategoriaId!.Value;
        produto.UnidadeVenda = request.UnidadeVenda;
        produto.Preco = request.Preco!.Value;
        produto.Perecivel = request.Perecivel!.Value;
        produto.DataValidade = request.DataValidade;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            produto.Id, produto.Nome, produto.Descricao, produto.CategoriaId,
            produto.UnidadeVenda, produto.Preco, produto.Estoque, produto.Perecivel,
            produto.DataValidade, produto.Ativo
        });
    }

    [HttpPatch("{id:int}/desativar")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Desativar(int id, CancellationToken cancellationToken)
    {
        var produto = await db.Produtos.FindAsync([id], cancellationToken);
        if (produto is null)
            return NotFound(new { mensagem = "Produto não encontrado." });

        produto.Ativo = false;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { produto.Id, produto.Nome, produto.Ativo });
    }

    private async Task<string?> ValidarProduto(CriarProdutoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return "Informe o nome do produto.";
        if (request.UnidadeVenda is not ("Unidade" or "Pacote" or "Caixa"))
            return "A unidade de venda deve ser Unidade, Pacote ou Caixa.";
        if (request.Perecivel == true && request.DataValidade is null)
            return "Informe a validade do produto perecível.";
        var categoriaAtiva = await db.Categorias
            .AnyAsync(categoria => categoria.Id == request.CategoriaId && categoria.Ativo, cancellationToken);
        return categoriaAtiva ? null : "Selecione uma categoria existente e ativa.";
    }
}

public class CriarProdutoRequest : IValidatableObject
{
    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descricao { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? CategoriaId { get; set; }

    [Required]
    public string UnidadeVenda { get; set; } = string.Empty;

    [Required]
    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal? Preco { get; set; }

    [Required]
    public bool? Perecivel { get; set; }

    public DateOnly? DataValidade { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Preco.HasValue && decimal.Round(Preco.Value, 2) != Preco.Value)
            yield return new ValidationResult("O preço deve ter no máximo duas casas decimais.", [nameof(Preco)]);
    }
}
