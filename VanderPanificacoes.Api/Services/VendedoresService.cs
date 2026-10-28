using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;
using VanderPanificacoes.Api.Models;

namespace VanderPanificacoes.Api.Services;

/// <summary>Dados de um vendedor disponíveis na consulta administrativa.</summary>
public sealed record VendedorResponse(int Id, string Nome, string Email, bool Ativo);

public interface IVendedoresService
{
    Task<IReadOnlyList<VendedorResponse>> ListarAsync(CancellationToken cancellationToken);
    Task<VendedorResponse?> CriarAsync(string nome, string email, string senha, CancellationToken cancellationToken);
    Task<ResultadoEdicaoVendedor> EditarAsync(int id, string nome, string email, CancellationToken cancellationToken);
    Task<VendedorResponse?> DesativarAsync(int id, CancellationToken cancellationToken);
}

public sealed record ResultadoEdicaoVendedor(VendedorResponse? Vendedor, bool EmailEmUso);

public sealed class VendedoresService(AppDbContext db, SenhaService senhas) : IVendedoresService
{
    public async Task<IReadOnlyList<VendedorResponse>> ListarAsync(CancellationToken cancellationToken)
        => await db.Usuarios.AsNoTracking()
            .Where(usuario => usuario.Perfil == "Vendedor")
            .OrderBy(usuario => usuario.Nome)
            .Select(usuario => new VendedorResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Ativo))
            .ToListAsync(cancellationToken);

    public async Task<VendedorResponse?> CriarAsync(string nome, string email, string senha, CancellationToken cancellationToken)
    {
        if (await db.Usuarios.AnyAsync(usuario => usuario.Email == email, cancellationToken))
            return null;

        var vendedor = new Usuario { Nome = nome, Email = email, Perfil = "Vendedor", Ativo = true };
        senhas.DefinirSenha(vendedor, senha);
        db.Usuarios.Add(vendedor);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            return null;
        }

        return new VendedorResponse(vendedor.Id, vendedor.Nome, vendedor.Email, vendedor.Ativo);
    }

    public async Task<ResultadoEdicaoVendedor> EditarAsync(int id, string nome, string email, CancellationToken cancellationToken)
    {
        var vendedor = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == id && u.Perfil == "Vendedor", cancellationToken);
        if (vendedor is null)
            return new(null, false);

        if (await db.Usuarios.AnyAsync(u => u.Id != id && u.Email == email, cancellationToken))
            return new(null, true);

        vendedor.Nome = nome;
        vendedor.Email = email;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            return new(null, true);
        }

        return new(new VendedorResponse(vendedor.Id, vendedor.Nome, vendedor.Email, vendedor.Ativo), false);
    }

    public async Task<VendedorResponse?> DesativarAsync(int id, CancellationToken cancellationToken)
    {
        var vendedor = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == id && u.Perfil == "Vendedor", cancellationToken);
        if (vendedor is null)
            return null;

        vendedor.Ativo = false;
        await db.SaveChangesAsync(cancellationToken);
        return new VendedorResponse(vendedor.Id, vendedor.Nome, vendedor.Email, vendedor.Ativo);
    }
}
