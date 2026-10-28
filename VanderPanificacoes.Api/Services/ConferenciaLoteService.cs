using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Quantidades propostas para os itens de um lote.</summary>
public sealed record ConferenciaLoteRequest
{
    [Required, MinLength(1)] public IReadOnlyList<ItemConferenciaRequest> Itens { get; init; } = [];
}

/// <summary>Quantidade proposta para um item.</summary>
public sealed record ItemConferenciaRequest
{
    [Required, Range(1, int.MaxValue)] public int? ItemPedidoId { get; init; }
    [Required, Range(0, int.MaxValue)] public int? QuantidadeAtendida { get; init; }
}

/// <summary>Valor previsto de uma parcela, antes da confirmação.</summary>
public sealed record ParcelaPrevistaResponse(int Numero, int PrazoDias, decimal Valor);

/// <summary>Resultado previsto de um pedido.</summary>
public sealed record PedidoConferenciaResponse(int PedidoId, int ClienteId, decimal TotalBruto,
    decimal Desconto, decimal TotalLiquido, IReadOnlyList<ParcelaPrevistaResponse> Parcelas);

/// <summary>Quantidade solicitada, proposta e cortada para um item.</summary>
public sealed record ItemConferenciaResponse(int ItemPedidoId, int ProdutoId, int QuantidadeSolicitada,
    int QuantidadeAtendida, int QuantidadeCortada);

/// <summary>Prévia de atendimento de um lote, sem reserva de estoque.</summary>
public sealed record ConferenciaLoteResponse(int LoteId, DateOnly DataReferencia,
    IReadOnlyList<ItemConferenciaResponse> Itens, IReadOnlyList<PedidoConferenciaResponse> Pedidos);

public sealed record ResultadoConferencia(ConferenciaLoteResponse? Conferencia, string? Erro);

public interface IConferenciaLoteService
{
    Task<ResultadoConferencia> SimularAsync(int loteId, ConferenciaLoteRequest request, CancellationToken cancellationToken);
}

public sealed class ConferenciaLoteService(AppDbContext db, CalculoVendaService calculo) : IConferenciaLoteService
{
    public async Task<ResultadoConferencia> SimularAsync(int loteId, ConferenciaLoteRequest request, CancellationToken cancellationToken)
    {
        if (request.Itens is null || request.Itens.Any(i => i is null || !i.ItemPedidoId.HasValue || !i.QuantidadeAtendida.HasValue))
            return new(null, "Informe todos os itens e quantidades.");
        if (request.Itens.GroupBy(i => i.ItemPedidoId).Any(g => g.Count() > 1))
            return new(null, "Um item não pode aparecer mais de uma vez.");

        var lote = await db.LotesPedidos.AsNoTracking().Where(l => l.Id == loteId)
            .Select(l => new { l.Situacao }).SingleOrDefaultAsync(cancellationToken);
        if (lote is null) return new(null, "Lote não encontrado.");
        if (lote.Situacao != "Recebido") return new(null, "A conferência só está disponível para lotes recebidos.");

        var itens = await db.ItensPedido.AsNoTracking().Where(i => i.Pedido.LotePedidosId == loteId)
            .OrderBy(i => i.Pedido.SequenciaPedido).ThenBy(i => i.Id)
            .Select(i => new { i.Id, i.PedidoId, i.Pedido.ClienteId, i.Pedido.PercentualDescontoSnapshot,
                i.Pedido.PrazosDiasSnapshot, i.ProdutoId, i.QuantidadeSolicitada, i.PrecoUnitario,
                i.Produto.Estoque, i.Produto.Ativo, i.Produto.DataValidade })
            .ToListAsync(cancellationToken);
        var quantidades = request.Itens.ToDictionary(i => i.ItemPedidoId!.Value, i => i.QuantidadeAtendida!.Value);
        if (itens.Count == 0 || itens.Count != quantidades.Count || itens.Any(i => !quantidades.ContainsKey(i.Id)))
            return new(null, "Informe exatamente uma quantidade para cada item do lote.");

        var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso));
        foreach (var item in itens)
        {
            var quantidade = quantidades[item.Id];
            if (quantidade < 0 || quantidade > item.QuantidadeSolicitada)
                return new(null, $"Quantidade inválida para o item {item.Id}.");
            if (quantidade > 0 && (!item.Ativo || (item.DataValidade.HasValue && item.DataValidade < hoje)))
                return new(null, $"O produto {item.ProdutoId} está inativo ou vencido.");
        }
        foreach (var grupo in itens.GroupBy(i => i.ProdutoId))
        {
            var soma = grupo.Sum(i => (long)quantidades[i.Id]);
            if (soma > grupo.First().Estoque)
                return new(null, $"A quantidade proposta para o produto {grupo.Key} supera o estoque disponível.");
        }

        var itensResponse = itens.Select(i => new ItemConferenciaResponse(i.Id, i.ProdutoId, i.QuantidadeSolicitada,
            quantidades[i.Id], i.QuantidadeSolicitada - quantidades[i.Id])).ToList();
        var pedidos = new List<PedidoConferenciaResponse>();
        foreach (var grupo in itens.GroupBy(i => new { i.PedidoId, i.ClienteId, i.PercentualDescontoSnapshot, i.PrazosDiasSnapshot }))
        {
            if (grupo.All(i => quantidades[i.Id] == 0))
            {
                pedidos.Add(new PedidoConferenciaResponse(grupo.Key.PedidoId, grupo.Key.ClienteId, 0, 0, 0, []));
                continue;
            }

            var prazos = grupo.Key.PrazosDiasSnapshot.Split(',').Select(int.Parse).ToArray();
            var resultado = calculo.Calcular(grupo.Select(i => new ItemCalculoVenda(i.PrecoUnitario, quantidades[i.Id])).ToList(),
                grupo.Key.PercentualDescontoSnapshot, prazos, hoje);
            var parcelas = resultado.Parcelas.Select((p, indice) => new ParcelaPrevistaResponse(p.Numero, prazos[indice], p.Valor)).ToList();
            pedidos.Add(new PedidoConferenciaResponse(grupo.Key.PedidoId, grupo.Key.ClienteId,
                resultado.TotalBruto, resultado.Desconto, resultado.TotalLiquido, parcelas));
        }
        return new(new ConferenciaLoteResponse(loteId, hoje, itensResponse, pedidos), null);
    }
}
