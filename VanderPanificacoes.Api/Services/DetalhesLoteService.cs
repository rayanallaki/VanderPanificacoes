using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Item solicitado e informações registradas no recebimento.</summary>
public sealed record ItemLoteResponse(int Id, int ProdutoId, string NomeProduto, string UnidadeVenda,
    decimal PrecoUnitario, int QuantidadeSolicitada, int QuantidadeAtendida, decimal SubtotalAtendido);

/// <summary>Pedido recebido e seus itens.</summary>
public sealed record PedidoDetalheResponse(int Id, int ClienteId, string Cliente, int SequenciaPedido,
    string CondicaoPagamento, string PrazosDias, decimal PercentualDesconto, string FormaPagamentoPrevista,
    string? Observacao, decimal TotalPrevisto, decimal TotalAtendido, decimal Desconto, decimal TotalFinal,
    string Situacao, IReadOnlyList<ItemLoteResponse> Itens);

/// <summary>Visita concluída no ciclo.</summary>
public sealed record VisitaDetalheResponse(int Id, int ClienteId, string Cliente, int PrioridadeOriginal,
    int OrdemAtendimento, string Situacao, string? MotivoSemPedido, string? Observacao, DateTimeOffset DataHoraAtendimento);

/// <summary>Informações completas de um lote para conferência.</summary>
public sealed record DetalhesLoteResponse(LoteResponse Lote, IReadOnlyList<VisitaDetalheResponse> Visitas,
    IReadOnlyList<PedidoDetalheResponse> Pedidos);

public interface IDetalhesLoteService
{
    Task<DetalhesLoteResponse?> BuscarAsync(int id, int? vendedorId, CancellationToken cancellationToken);
}

public sealed class DetalhesLoteService(AppDbContext db) : IDetalhesLoteService
{
    public async Task<DetalhesLoteResponse?> BuscarAsync(int id, int? vendedorId, CancellationToken cancellationToken)
    {
        var lote = await db.LotesPedidos.AsNoTracking().Where(l => l.Id == id && (!vendedorId.HasValue || l.VendedorId == vendedorId.Value))
            .Select(l => new { l.Id, l.IdentificadorEnvio, l.VendedorId, Vendedor = l.Vendedor.Nome,
                l.DataCriacaoLocal, l.DataRecebimento, l.DataConfirmacao, l.Situacao, l.MotivoCancelamento })
            .SingleOrDefaultAsync(cancellationToken);
        if (lote is null) return null;

        var visitas = await db.Visitas.AsNoTracking().Where(v => v.LotePedidosId == id)
            .OrderBy(v => v.OrdemAtendimento)
            .Select(v => new { v.Id, v.ClienteId, Cliente = v.Cliente.Nome, v.PrioridadeOriginal,
                v.OrdemAtendimento, v.Situacao, v.MotivoSemPedido, v.Observacao, v.DataHoraAtendimento })
            .ToListAsync(cancellationToken);
        var pedidos = await db.Pedidos.AsNoTracking().Where(p => p.LotePedidosId == id)
            .OrderBy(p => p.SequenciaPedido)
            .Select(p => new { p.Id, p.ClienteId, Cliente = p.Cliente.Nome, p.SequenciaPedido,
                p.NomeCondicaoSnapshot, p.PrazosDiasSnapshot, p.PercentualDescontoSnapshot, p.FormaPagamentoPrevista,
                p.Observacao, p.TotalPrevisto, p.TotalAtendido, p.Desconto, p.TotalFinal, p.Situacao })
            .ToListAsync(cancellationToken);
        var idsPedidos = pedidos.Select(p => p.Id).ToList();
        var itens = await db.ItensPedido.AsNoTracking().Where(i => idsPedidos.Contains(i.PedidoId))
            .OrderBy(i => i.Id)
            .Select(i => new { i.PedidoId, i.Id, i.ProdutoId, i.NomeProdutoSnapshot, i.UnidadeVendaSnapshot,
                i.PrecoUnitario, i.QuantidadeSolicitada, i.QuantidadeAtendida, i.SubtotalAtendido })
            .ToListAsync(cancellationToken);

        var loteResponse = new LoteResponse(lote.Id, lote.IdentificadorEnvio, lote.VendedorId, lote.Vendedor,
            EmUtc(lote.DataCriacaoLocal), EmUtc(lote.DataRecebimento),
            lote.DataConfirmacao.HasValue ? EmUtc(lote.DataConfirmacao.Value) : null, lote.Situacao, lote.MotivoCancelamento);
        var visitasResponse = visitas.Select(v => new VisitaDetalheResponse(v.Id, v.ClienteId, v.Cliente,
            v.PrioridadeOriginal, v.OrdemAtendimento, v.Situacao, v.MotivoSemPedido, v.Observacao, EmUtc(v.DataHoraAtendimento))).ToList();
        var pedidosResponse = pedidos.Select(p => new PedidoDetalheResponse(p.Id, p.ClienteId, p.Cliente, p.SequenciaPedido,
            p.NomeCondicaoSnapshot, p.PrazosDiasSnapshot, p.PercentualDescontoSnapshot, p.FormaPagamentoPrevista,
            p.Observacao, p.TotalPrevisto, p.TotalAtendido, p.Desconto, p.TotalFinal, p.Situacao,
            itens.Where(i => i.PedidoId == p.Id).Select(i => new ItemLoteResponse(i.Id, i.ProdutoId,
                i.NomeProdutoSnapshot, i.UnidadeVendaSnapshot, i.PrecoUnitario, i.QuantidadeSolicitada,
                i.QuantidadeAtendida, i.SubtotalAtendido)).ToList())).ToList();
        return new DetalhesLoteResponse(loteResponse, visitasResponse, pedidosResponse);
    }

    private static DateTimeOffset EmUtc(DateTime data) => new(DateTime.SpecifyKind(data, DateTimeKind.Utc));
}
