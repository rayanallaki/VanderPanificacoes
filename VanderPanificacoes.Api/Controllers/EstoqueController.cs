using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;
using VanderPanificacoes.Api.Models;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Controllers;

[ApiController]
[Route("api/estoque")]
[Authorize(Roles = "Administrador")]
public class EstoqueController(AppDbContext db, IWebHostEnvironment ambiente, IHistoricoEstoqueService historico) : ControllerBase
{
    /// <summary>Consulta o histórico de estoque, opcionalmente filtrado por produto.</summary>
    [HttpGet("movimentacoes")]
    [ProducesResponseType(typeof(IReadOnlyList<MovimentacaoEstoqueResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarMovimentacoes([FromQuery] int? produtoId, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();
        if (produtoId.HasValue && produtoId <= 0)
            return BadRequest(new { mensagem = "O ID do produto deve ser maior que zero." });

        return Ok(await historico.ListarAsync(produtoId, cancellationToken));
    }

    [HttpPost("entradas")]
    public async Task<IActionResult> RegistrarEntrada(EntradaEstoqueRequest request, CancellationToken cancellationToken)
        => await RegistrarMovimentacao(request, "Entrada", cancellationToken);

    /// <summary>Registra uma correção que aumenta o estoque.</summary>
    [HttpPost("ajustes-entrada")]
    public async Task<IActionResult> RegistrarAjusteEntrada(EntradaEstoqueRequest request, CancellationToken cancellationToken)
        => await RegistrarMovimentacao(request, "AjusteEntrada", cancellationToken);

    [HttpPost("ajustes-saida")]
    public async Task<IActionResult> RegistrarAjusteSaida(EntradaEstoqueRequest request, CancellationToken cancellationToken)
        => await RegistrarMovimentacao(request, "AjusteSaida", cancellationToken);

    private async Task<IActionResult> RegistrarMovimentacao(EntradaEstoqueRequest request, string tipo, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        if (!int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var usuarioId) || usuarioId <= 0)
            return Unauthorized();
        request.UsuarioId = usuarioId;

        var motivo = request.Motivo.Trim();
        if (request.IdentificadorOperacao == Guid.Empty || string.IsNullOrWhiteSpace(motivo))
            return BadRequest(new { mensagem = "Informe um identificador válido e o motivo da entrada." });

        var anterior = await db.MovimentacoesEstoque.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdentificadorOperacao == request.IdentificadorOperacao, cancellationToken);
        if (anterior is not null)
            return ResultadoRepeticao(anterior, request, motivo, tipo);

        if (!await db.Usuarios.AnyAsync(x => x.Id == request.UsuarioId && x.Ativo && x.Perfil == "Administrador", cancellationToken))
            return BadRequest(new { mensagem = "Informe um administrador ativo como responsável." });

        var quantidade = request.Quantidade!.Value;
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var produtos = db.Produtos.Where(x => x.Id == request.ProdutoId && x.Ativo);
            var alterados = tipo is "Entrada" or "AjusteEntrada"
                ? await produtos.Where(x => x.Estoque <= int.MaxValue - quantidade)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.Estoque, p => p.Estoque + quantidade), cancellationToken)
                : await produtos.Where(x => x.Estoque >= quantidade)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.Estoque, p => p.Estoque - quantidade), cancellationToken);

            if (alterados == 0)
            {
                await transacao.RollbackAsync(cancellationToken);
                await transacao.DisposeAsync();
                var repetida = await db.MovimentacoesEstoque.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.IdentificadorOperacao == request.IdentificadorOperacao, cancellationToken);
                if (repetida is not null)
                    return ResultadoRepeticao(repetida, request, motivo, tipo);
                return BadRequest(new { mensagem = "Produto inexistente ou inativo, ou quantidade incompatível com o estoque disponível." });
            }

            var movimentacao = new MovimentacaoEstoque
            {
                IdentificadorOperacao = request.IdentificadorOperacao!.Value,
                ProdutoId = request.ProdutoId!.Value,
                UsuarioId = request.UsuarioId!.Value,
                Tipo = tipo,
                Quantidade = request.Quantidade!.Value,
                Motivo = motivo,
                DataHora = DateTime.UtcNow
            };
            db.MovimentacoesEstoque.Add(movimentacao);
            await db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return StatusCode(StatusCodes.Status201Created, Resposta(movimentacao));
        }
        catch (DbUpdateException)
        {
            await transacao.RollbackAsync(cancellationToken);
            await transacao.DisposeAsync();
            db.ChangeTracker.Clear();
            var existente = await db.MovimentacoesEstoque.AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdentificadorOperacao == request.IdentificadorOperacao, cancellationToken);
            if (existente is not null)
                return ResultadoRepeticao(existente, request, motivo, tipo);
            throw;
        }
    }

    private IActionResult ResultadoRepeticao(MovimentacaoEstoque anterior, EntradaEstoqueRequest request, string motivo, string tipo)
    {
        if (anterior.Tipo != tipo || anterior.ProdutoId != request.ProdutoId ||
            anterior.UsuarioId != request.UsuarioId || anterior.Quantidade != request.Quantidade || anterior.Motivo != motivo)
            return Conflict(new { mensagem = "Identificador já utilizado para uma operação diferente." });
        return Ok(Resposta(anterior));
    }

    private static object Resposta(MovimentacaoEstoque x) => new
    {
        x.Id, x.IdentificadorOperacao, x.ProdutoId, x.UsuarioId, x.Tipo, x.Quantidade, x.Motivo, x.DataHora
    };
}

public class EntradaEstoqueRequest
{
    [Required] public Guid? IdentificadorOperacao { get; set; }
    [Required, Range(1, int.MaxValue)] public int? ProdutoId { get; set; }
    [JsonIgnore] public int? UsuarioId { get; set; }
    [Required, Range(1, int.MaxValue)] public int? Quantidade { get; set; }
    [Required, StringLength(500)] public string Motivo { get; set; } = string.Empty;
}
