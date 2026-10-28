using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Identidade de uma conta após a verificação da senha.</summary>
public sealed record IdentidadeAutenticada(int Id, string Nome, string Email, string Perfil);

public interface IAutenticacaoService
{
    Task<IdentidadeAutenticada?> VerificarCredenciaisAsync(string email, string senha, CancellationToken cancellationToken);
}

public sealed class AutenticacaoService(AppDbContext db, SenhaService senhas) : IAutenticacaoService
{
    public async Task<IdentidadeAutenticada?> VerificarCredenciaisAsync(string email, string senha, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            return null;

        var conta = await db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(usuario => usuario.Email == email.Trim(), cancellationToken);
        if (conta is null || !conta.Ativo ||
            senhas.VerificarSenha(conta, senha) == PasswordVerificationResult.Failed)
            return null;

        return new IdentidadeAutenticada(conta.Id, conta.Nome, conta.Email, conta.Perfil);
    }
}
