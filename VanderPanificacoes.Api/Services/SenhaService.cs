using Microsoft.AspNetCore.Identity;
using VanderPanificacoes.Api.Models;

namespace VanderPanificacoes.Api.Services;

public class SenhaService(IPasswordHasher<Usuario> passwordHasher)
{
    public void DefinirSenha(Usuario usuario, string senha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senha);
        usuario.SenhaHash = passwordHasher.HashPassword(usuario, senha);
    }

    public PasswordVerificationResult VerificarSenha(Usuario usuario, string senha)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(usuario.SenhaHash))
            return PasswordVerificationResult.Failed;

        return passwordHasher.VerifyHashedPassword(usuario, usuario.SenhaHash, senha);
    }
}
