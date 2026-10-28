using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Cliente ativo da carteira de um vendedor.</summary>
public sealed record ClienteCarteiraResponse(int Id, string Nome, string Documento, string? Telefone,
    string? Email, string? Rua, string? Numero, string? Complemento, string? Bairro,
    string? Cidade, string? UF, string? CEP, int PrioridadeVisita);

public interface ICarteiraService
{
    Task<IReadOnlyList<ClienteCarteiraResponse>> ListarAsync(int vendedorId, CancellationToken cancellationToken);
}

public sealed class CarteiraService(AppDbContext db) : ICarteiraService
{
    public async Task<IReadOnlyList<ClienteCarteiraResponse>> ListarAsync(int vendedorId, CancellationToken cancellationToken)
        => await db.Clientes.AsNoTracking().Where(c => c.VendedorId == vendedorId && c.Ativo)
            .OrderBy(c => c.PrioridadeVisita).ThenBy(c => c.Nome)
            .Select(c => new ClienteCarteiraResponse(c.Id, c.Nome, c.Documento, c.Telefone,
                c.Email, c.Rua, c.Numero, c.Complemento, c.Bairro, c.Cidade, c.UF, c.CEP, c.PrioridadeVisita))
            .ToListAsync(cancellationToken);
}
