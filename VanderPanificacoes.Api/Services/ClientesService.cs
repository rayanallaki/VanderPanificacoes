using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VanderPanificacoes.Api.Data;
using VanderPanificacoes.Api.Models;

namespace VanderPanificacoes.Api.Services;

/// <summary>Dados cadastrais de um cliente.</summary>
public sealed record ClienteResponse(
    int Id, int VendedorId, string Vendedor, string Nome, string Documento,
    string? Telefone, string? Email, string? Rua, string? Numero,
    string? Complemento, string? Bairro, string? Cidade, string? UF,
    string? CEP, int PrioridadeVisita, bool Ativo);

/// <summary>Dados para cadastrar um cliente e vinculá-lo a um vendedor.</summary>
public sealed record CriarClienteRequest
{
    [Required, Range(1, int.MaxValue)] public int? VendedorId { get; init; }
    [Required, StringLength(100)] public string Nome { get; init; } = string.Empty;
    [Required, StringLength(30)] public string Documento { get; init; } = string.Empty;
    [StringLength(20)] public string? Telefone { get; init; }
    [EmailAddress, StringLength(150)] public string? Email { get; init; }
    [StringLength(100)] public string? Rua { get; init; }
    [StringLength(20)] public string? Numero { get; init; }
    [StringLength(100)] public string? Complemento { get; init; }
    [StringLength(100)] public string? Bairro { get; init; }
    [StringLength(100)] public string? Cidade { get; init; }
    [StringLength(2, MinimumLength = 2)] public string? UF { get; init; }
    [StringLength(9)] public string? CEP { get; init; }
    [Required, Range(0, int.MaxValue)] public int? PrioridadeVisita { get; init; }
}

public interface IClientesService
{
    Task<IReadOnlyList<ClienteResponse>> ListarAsync(string? nome, string? documento, int? vendedorId, CancellationToken cancellationToken);
    Task<ResultadoCadastroCliente> CriarAsync(CriarClienteRequest request, string documento, CancellationToken cancellationToken);
    Task<ResultadoCadastroCliente> EditarAsync(int id, CriarClienteRequest request, string documento, CancellationToken cancellationToken);
    Task<ClienteResponse?> DesativarAsync(int id, CancellationToken cancellationToken);
    Task<ClienteResponse?> ReativarAsync(int id, CancellationToken cancellationToken);
}

public sealed record ResultadoCadastroCliente(ClienteResponse? Cliente, string? Erro);

public sealed class ClientesService(AppDbContext db) : IClientesService
{
    public async Task<IReadOnlyList<ClienteResponse>> ListarAsync(string? nome, string? documento, int? vendedorId, CancellationToken cancellationToken)
    {
        var consulta = db.Clientes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(nome))
            consulta = consulta.Where(cliente => cliente.Nome.Contains(nome.Trim()));
        if (!string.IsNullOrEmpty(documento))
            consulta = consulta.Where(cliente => cliente.Documento.Contains(documento));
        if (vendedorId.HasValue)
            consulta = consulta.Where(cliente => cliente.VendedorId == vendedorId.Value);

        return await consulta
            .OrderBy(cliente => cliente.Nome)
            .Select(cliente => new ClienteResponse(
                cliente.Id, cliente.VendedorId, cliente.Vendedor.Nome, cliente.Nome,
                cliente.Documento, cliente.Telefone, cliente.Email, cliente.Rua,
                cliente.Numero, cliente.Complemento, cliente.Bairro, cliente.Cidade,
                cliente.UF, cliente.CEP, cliente.PrioridadeVisita, cliente.Ativo))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResultadoCadastroCliente> CriarAsync(CriarClienteRequest request, string documento, CancellationToken cancellationToken)
    {
        var vendedor = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(
            u => u.Id == request.VendedorId && u.Perfil == "Vendedor" && u.Ativo, cancellationToken);
        if (vendedor is null)
            return new(null, "Vendedor inexistente ou inativo.");

        if (await db.Clientes.AnyAsync(c => c.Documento == documento, cancellationToken))
            return new(null, "Documento já cadastrado.");

        var cliente = new Cliente
        {
            VendedorId = vendedor.Id, Nome = request.Nome.Trim(), Documento = documento,
            Telefone = request.Telefone?.Trim(), Email = request.Email?.Trim(), Rua = request.Rua?.Trim(),
            Numero = request.Numero?.Trim(), Complemento = request.Complemento?.Trim(), Bairro = request.Bairro?.Trim(),
            Cidade = request.Cidade?.Trim(), UF = request.UF?.Trim().ToUpperInvariant(), CEP = request.CEP?.Trim(),
            PrioridadeVisita = request.PrioridadeVisita!.Value, Ativo = true
        };
        db.Clientes.Add(cliente);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            return new(null, "Documento já cadastrado.");
        }

        return new(new ClienteResponse(cliente.Id, vendedor.Id, vendedor.Nome, cliente.Nome,
            cliente.Documento, cliente.Telefone, cliente.Email, cliente.Rua, cliente.Numero,
            cliente.Complemento, cliente.Bairro, cliente.Cidade, cliente.UF, cliente.CEP,
            cliente.PrioridadeVisita, cliente.Ativo), null);
    }

    public async Task<ClienteResponse?> DesativarAsync(int id, CancellationToken cancellationToken)
        => await AlterarSituacaoAsync(id, false, cancellationToken);

    public async Task<ClienteResponse?> ReativarAsync(int id, CancellationToken cancellationToken)
        => await AlterarSituacaoAsync(id, true, cancellationToken);

    private async Task<ClienteResponse?> AlterarSituacaoAsync(int id, bool ativo, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.Include(c => c.Vendedor)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cliente is null)
            return null;

        cliente.Ativo = ativo;
        await db.SaveChangesAsync(cancellationToken);
        return new ClienteResponse(cliente.Id, cliente.VendedorId, cliente.Vendedor.Nome, cliente.Nome,
            cliente.Documento, cliente.Telefone, cliente.Email, cliente.Rua, cliente.Numero,
            cliente.Complemento, cliente.Bairro, cliente.Cidade, cliente.UF, cliente.CEP,
            cliente.PrioridadeVisita, cliente.Ativo);
    }

    public async Task<ResultadoCadastroCliente> EditarAsync(int id, CriarClienteRequest request, string documento, CancellationToken cancellationToken)
    {
        var cliente = await db.Clientes.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cliente is null)
            return new(null, "Cliente não encontrado.");

        var vendedor = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(
            u => u.Id == request.VendedorId && u.Perfil == "Vendedor" && u.Ativo, cancellationToken);
        if (vendedor is null)
            return new(null, "Vendedor inexistente ou inativo.");

        if (await db.Clientes.AnyAsync(c => c.Id != id && c.Documento == documento, cancellationToken))
            return new(null, "Documento já cadastrado.");

        cliente.VendedorId = vendedor.Id;
        cliente.Nome = request.Nome.Trim();
        cliente.Documento = documento;
        cliente.Telefone = request.Telefone?.Trim();
        cliente.Email = request.Email?.Trim();
        cliente.Rua = request.Rua?.Trim();
        cliente.Numero = request.Numero?.Trim();
        cliente.Complemento = request.Complemento?.Trim();
        cliente.Bairro = request.Bairro?.Trim();
        cliente.Cidade = request.Cidade?.Trim();
        cliente.UF = request.UF?.Trim().ToUpperInvariant();
        cliente.CEP = request.CEP?.Trim();
        cliente.PrioridadeVisita = request.PrioridadeVisita!.Value;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            return new(null, "Documento já cadastrado.");
        }

        return new(new ClienteResponse(cliente.Id, vendedor.Id, vendedor.Nome, cliente.Nome,
            cliente.Documento, cliente.Telefone, cliente.Email, cliente.Rua, cliente.Numero,
            cliente.Complemento, cliente.Bairro, cliente.Cidade, cliente.UF, cliente.CEP,
            cliente.PrioridadeVisita, cliente.Ativo), null);
    }
}
