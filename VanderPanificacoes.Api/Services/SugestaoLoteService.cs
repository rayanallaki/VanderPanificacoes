using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Sugestão para um item de pedido.</summary>
public sealed record ItemSugestaoResponse(int ItemPedidoId, int ProdutoId, string Produto,
    int QuantidadeSolicitada, int QuantidadeSugerida, int QuantidadeCortada);

/// <summary>Sugestão para um pedido, conforme sua posição no lote.</summary>
public sealed record PedidoSugestaoResponse(int PedidoId, int ClienteId, int SequenciaPedido,
    IReadOnlyList<ItemSugestaoResponse> Itens);

/// <summary>Simulação de atendimento de um lote.</summary>
public sealed record SugestaoLoteResponse(int LoteId, DateOnly DataReferencia,
    IReadOnlyList<PedidoSugestaoResponse> Pedidos);

public sealed record ResultadoSugestao(SugestaoLoteResponse? Sugestao, string? Erro);

public interface ISugestaoLoteService
{
    Task<ResultadoSugestao> CalcularAsync(int loteId, CancellationToken cancellationToken);
}

public sealed class SugestaoLoteService(AppDbContext db) : ISugestaoLoteService
{
    public async Task<ResultadoSugestao> CalcularAsync(int loteId, CancellationToken cancellationToken)
    {
        var lote = await db.LotesPedidos.AsNoTracking().Where(l => l.Id == loteId)
            .Select(l => new { l.Id, l.Situacao }).SingleOrDefaultAsync(cancellationToken);
        if (lote is null) return new(null, "Lote não encontrado.");
        if (lote.Situacao != "Recebido") return new(null, "A sugestão só está disponível para lotes recebidos.");

        var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso));
        var itens = await db.ItensPedido.AsNoTracking().Where(i => i.Pedido.LotePedidosId == loteId)
            .OrderBy(i => i.Pedido.SequenciaPedido).ThenBy(i => i.Id)
            .Select(i => new { ItemId = i.Id, i.PedidoId, i.Pedido.ClienteId, i.Pedido.SequenciaPedido,
                i.ProdutoId, Produto = i.NomeProdutoSnapshot, i.QuantidadeSolicitada,
                i.Produto.Estoque, i.Produto.Ativo, i.Produto.DataValidade })
            .ToListAsync(cancellationToken);

        var distribuicao = DistribuicaoEstoque.Sugerir(itens.Select(i => new SolicitacaoEstoque(
            i.ItemId, i.ProdutoId, i.SequenciaPedido, i.QuantidadeSolicitada, i.Estoque, i.Ativo, i.DataValidade)), hoje)
            .ToDictionary(i => i.ItemId, i => i.QuantidadeSugerida);
        var sugestoes = itens.Select(item => (item.PedidoId, item.ClienteId, Sequencia: item.SequenciaPedido,
            Item: new ItemSugestaoResponse(item.ItemId, item.ProdutoId, item.Produto, item.QuantidadeSolicitada,
                distribuicao[item.ItemId], item.QuantidadeSolicitada - distribuicao[item.ItemId]))).ToList();

        var pedidos = sugestoes.GroupBy(s => new { s.PedidoId, s.ClienteId, s.Sequencia })
            .Select(g => new PedidoSugestaoResponse(g.Key.PedidoId, g.Key.ClienteId, g.Key.Sequencia,
                g.Select(s => s.Item).ToList())).ToList();
        return new(new SugestaoLoteResponse(loteId, hoje, pedidos), null);
    }
}
