using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Parcela e dados de seu recebimento, quando houver.</summary>
public sealed record ParcelaResponse(int Id, int VendaId, int ClienteId, string Cliente, int Numero,
    DateOnly Vencimento, decimal ValorBase, string Situacao, string FormaPagamentoPrevista,
    string? FormaPagamentoRecebida, DateOnly? DataRecebimento, decimal? ValorRecebido);

public interface IParcelasService
{
    Task<IReadOnlyList<ParcelaResponse>> ListarAsync(int? clienteId, DateOnly? inicio, DateOnly? fim,
        string? situacao, CancellationToken cancellationToken);
}

public sealed class ParcelasService(AppDbContext db) : IParcelasService
{
    public async Task<IReadOnlyList<ParcelaResponse>> ListarAsync(int? clienteId, DateOnly? inicio, DateOnly? fim,
        string? situacao, CancellationToken cancellationToken)
    {
        var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso));
        var consulta = db.Parcelas.AsNoTracking();
        if (clienteId.HasValue) consulta = consulta.Where(p => p.Venda.Pedido.ClienteId == clienteId.Value);
        if (inicio.HasValue) consulta = consulta.Where(p => p.Vencimento >= inicio.Value);
        if (fim.HasValue) consulta = consulta.Where(p => p.Vencimento <= fim.Value);
        if (situacao == "Paga") consulta = consulta.Where(p => p.Situacao == "Paga");
        if (situacao == "Aberta") consulta = consulta.Where(p => p.Situacao == "Aberta" && p.Vencimento >= hoje);
        if (situacao == "Vencida") consulta = consulta.Where(p => p.Situacao == "Aberta" && p.Vencimento < hoje);

        var parcelas = await consulta.OrderBy(p => p.Vencimento).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.VendaId, p.Venda.Pedido.ClienteId, Cliente = p.Venda.Pedido.Cliente.Nome,
                p.Numero, p.Vencimento, p.ValorBase, p.Situacao, p.FormaPagamentoPrevista,
                p.FormaPagamentoRecebida, p.DataRecebimento, p.ValorRecebido })
            .ToListAsync(cancellationToken);
        return parcelas.Select(p => new ParcelaResponse(p.Id, p.VendaId, p.ClienteId, p.Cliente, p.Numero,
            p.Vencimento, p.ValorBase, p.Situacao == "Aberta" && p.Vencimento < hoje ? "Vencida" : p.Situacao,
            p.FormaPagamentoPrevista, p.FormaPagamentoRecebida, p.DataRecebimento, p.ValorRecebido)).ToList();
    }
}
