using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace VanderPanificacoes.Api.Services;

public sealed record ConfiguracaoToken(bool Configurada, byte[] Chave);
public sealed record TokenGerado(string Token, DateTimeOffset ExpiraEm);

/// <summary>Emite tokens para testar a autenticação no ambiente de desenvolvimento.</summary>
public sealed class TokenDesenvolvimentoService(ConfiguracaoToken configuracao)
{
    public TokenGerado Gerar(IdentidadeAutenticada identidade)
    {
        if (!configuracao.Configurada) throw new InvalidOperationException("Chave de desenvolvimento não configurada.");
        var agora = DateTimeOffset.UtcNow;
        var expira = agora.AddHours(1);
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(configuracao.Chave), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, identidade.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("role", identidade.Perfil),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(issuer: "VanderPanificacoes.Dev", audience: "VanderPanificacoes.Api",
            claims: claims, notBefore: agora.UtcDateTime, expires: expira.UtcDateTime, signingCredentials: credenciais);
        return new TokenGerado(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
