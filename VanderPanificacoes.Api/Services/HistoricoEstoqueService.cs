using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Movimentação de estoque apresentada na consulta de histórico.</summary>
public sealed record MovimentacaoEstoqueResponse(
    int Id, Guid IdentificadorOperacao, int ProdutoId, string Produto,
    int UsuarioId, string Usuario, string Tipo, int Quantidade, string Motivo,
    DateTimeOffset DataHora);

public interface IHistoricoEstoqueService
{
    Task<IReadOnlyList<MovimentacaoEstoqueResponse>> ListarAsync(int? produtoId, CancellationToken cancellationToken);
}

public sealed class HistoricoEstoqueService(AppDbContext db) : IHistoricoEstoqueService
{
    public async Task<IReadOnlyList<MovimentacaoEstoqueResponse>> ListarAsync(int? produtoId, CancellationToken cancellationToken)
    {
        var consulta = db.MovimentacoesEstoque.AsNoTracking();
        if (produtoId.HasValue)
            consulta = consulta.Where(m => m.ProdutoId == produtoId.Value);

        var registros = await consulta.OrderByDescending(m => m.DataHora).ThenByDescending(m => m.Id)
            .Select(m => new
            {
                m.Id, m.IdentificadorOperacao, m.ProdutoId, Produto = m.Produto.Nome,
                m.UsuarioId, Usuario = m.Usuario.Nome, m.Tipo, m.Quantidade, m.Motivo, m.DataHora
            }).ToListAsync(cancellationToken);

        return registros.Select(m => new MovimentacaoEstoqueResponse(
            m.Id, m.IdentificadorOperacao, m.ProdutoId, m.Produto, m.UsuarioId,
            m.Usuario, m.Tipo, m.Quantidade, m.Motivo,
            new DateTimeOffset(DateTime.SpecifyKind(m.DataHora, DateTimeKind.Utc)))).ToList();
    }
}
