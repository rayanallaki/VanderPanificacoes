namespace VanderPanificacoes.Api.Services;

public sealed record SolicitacaoEstoque(int ItemId, int ProdutoId, int SequenciaPedido,
    int QuantidadeSolicitada, int Estoque, bool Ativo, DateOnly? DataValidade);

public sealed record DistribuicaoItem(int ItemId, int QuantidadeSugerida);

/// <summary>Simula a distribuição do saldo entre pedidos em ordem de sequência.</summary>
public static class DistribuicaoEstoque
{
    public static IReadOnlyList<DistribuicaoItem> Sugerir(IEnumerable<SolicitacaoEstoque> itens, DateOnly dataReferencia)
    {
        var saldoPorProduto = new Dictionary<int, int>();
        var resultado = new List<DistribuicaoItem>();
        foreach (var item in itens.OrderBy(i => i.SequenciaPedido).ThenBy(i => i.ItemId))
        {
            if (!saldoPorProduto.ContainsKey(item.ProdutoId))
                saldoPorProduto[item.ProdutoId] = item.Ativo && (!item.DataValidade.HasValue || item.DataValidade >= dataReferencia)
                    ? item.Estoque : 0;
            var quantidade = Math.Min(item.QuantidadeSolicitada, saldoPorProduto[item.ProdutoId]);
            saldoPorProduto[item.ProdutoId] -= quantidade;
            resultado.Add(new DistribuicaoItem(item.ItemId, quantidade));
        }
        return resultado;
    }
}
