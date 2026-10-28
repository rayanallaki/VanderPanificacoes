using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Condição de pagamento disponível para os pedidos.</summary>
public sealed record CondicaoPagamentoResponse(
    int Id, string Nome, int QuantidadeParcelas, IReadOnlyList<int> PrazosDias, decimal PercentualDesconto);

public interface ICondicoesPagamentoService
{
    Task<IReadOnlyList<CondicaoPagamentoResponse>> ListarAsync(CancellationToken cancellationToken);
}

public sealed class CondicoesPagamentoService(AppDbContext db) : ICondicoesPagamentoService
{
    public async Task<IReadOnlyList<CondicaoPagamentoResponse>> ListarAsync(CancellationToken cancellationToken)
    {
        var condicoes = await db.CondicoesPagamento.AsNoTracking()
            .OrderBy(condicao => condicao.Id)
            .Select(condicao => new { condicao.Id, condicao.Nome, condicao.QuantidadeParcelas,
                condicao.PrazosDias, condicao.PercentualDesconto })
            .ToListAsync(cancellationToken);

        return condicoes.Select(condicao => new CondicaoPagamentoResponse(
            condicao.Id, condicao.Nome, condicao.QuantidadeParcelas,
            condicao.PrazosDias.Split(',').Select(int.Parse).ToArray(), condicao.PercentualDesconto)).ToList();
    }
}
