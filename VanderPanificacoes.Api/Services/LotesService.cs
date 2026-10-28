using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Dados gerais de um lote recebido pela API.</summary>
public sealed record LoteResponse(int Id, Guid IdentificadorEnvio, int VendedorId, string Vendedor,
    DateTimeOffset DataCriacaoLocal, DateTimeOffset DataRecebimento, DateTimeOffset? DataConfirmacao,
    string Situacao, string? MotivoCancelamento);

public interface ILotesService
{
    Task<IReadOnlyList<LoteResponse>> ListarAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LoteResponse>> ListarDoVendedorAsync(int vendedorId, CancellationToken cancellationToken);
    Task<LoteResponse?> BuscarPorEnvioAsync(Guid identificadorEnvio, int vendedorId, CancellationToken cancellationToken);
}

public sealed class LotesService(AppDbContext db) : ILotesService
{
    public Task<IReadOnlyList<LoteResponse>> ListarAsync(CancellationToken cancellationToken) => ListarAsync(null, cancellationToken);

    public Task<IReadOnlyList<LoteResponse>> ListarDoVendedorAsync(int vendedorId, CancellationToken cancellationToken) => ListarAsync(vendedorId, cancellationToken);

    private async Task<IReadOnlyList<LoteResponse>> ListarAsync(int? vendedorId, CancellationToken cancellationToken)
    {
        var lotes = await db.LotesPedidos.AsNoTracking()
            .Where(lote => !vendedorId.HasValue || lote.VendedorId == vendedorId.Value)
            .OrderByDescending(lote => lote.DataRecebimento).ThenByDescending(lote => lote.Id)
            .Select(lote => new
            {
                lote.Id, lote.IdentificadorEnvio, lote.VendedorId, Vendedor = lote.Vendedor.Nome,
                lote.DataCriacaoLocal, lote.DataRecebimento, lote.DataConfirmacao,
                lote.Situacao, lote.MotivoCancelamento
            }).ToListAsync(cancellationToken);

        return lotes.Select(lote => new LoteResponse(
            lote.Id, lote.IdentificadorEnvio, lote.VendedorId, lote.Vendedor,
            EmUtc(lote.DataCriacaoLocal), EmUtc(lote.DataRecebimento),
            lote.DataConfirmacao.HasValue ? EmUtc(lote.DataConfirmacao.Value) : null,
            lote.Situacao, lote.MotivoCancelamento)).ToList();
    }

    private static DateTimeOffset EmUtc(DateTime data) => new(DateTime.SpecifyKind(data, DateTimeKind.Utc));

    public async Task<LoteResponse?> BuscarPorEnvioAsync(Guid identificadorEnvio, int vendedorId, CancellationToken cancellationToken)
    {
        var lote = await db.LotesPedidos.AsNoTracking()
            .Where(l => l.IdentificadorEnvio == identificadorEnvio && l.VendedorId == vendedorId &&
                l.Vendedor.Ativo && l.Vendedor.Perfil == "Vendedor")
            .Select(l => new { l.Id, l.IdentificadorEnvio, l.VendedorId, Vendedor = l.Vendedor.Nome,
                l.DataCriacaoLocal, l.DataRecebimento, l.DataConfirmacao, l.Situacao, l.MotivoCancelamento })
            .SingleOrDefaultAsync(cancellationToken);
        return lote is null ? null : new LoteResponse(lote.Id, lote.IdentificadorEnvio, lote.VendedorId,
            lote.Vendedor, EmUtc(lote.DataCriacaoLocal), EmUtc(lote.DataRecebimento),
            lote.DataConfirmacao.HasValue ? EmUtc(lote.DataConfirmacao.Value) : null, lote.Situacao, lote.MotivoCancelamento);
    }
}
