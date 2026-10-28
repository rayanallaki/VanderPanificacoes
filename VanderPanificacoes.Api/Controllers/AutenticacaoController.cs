using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Controllers;

/// <summary>Credenciais para testar o login em desenvolvimento.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(150)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(128)] public string Senha { get; init; } = string.Empty;
}

/// <summary>Token temporário gerado em desenvolvimento.</summary>
public sealed record LoginResponse(string Token, DateTimeOffset ExpiraEm, IdentidadeAutenticada Usuario);

[ApiController]
[Route("api/autenticacao")]
public class AutenticacaoController(IAutenticacaoService autenticacao, TokenDesenvolvimentoService tokens,
    ConfiguracaoToken configuracao, IWebHostEnvironment ambiente) : ControllerBase
{
    /// <summary>Testa as credenciais e gera um token de desenvolvimento.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        if (!configuracao.Configurada)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensagem = "Configure a chave de desenvolvimento para testar o login." });

        var identidade = await autenticacao.VerificarCredenciaisAsync(request.Email, request.Senha, cancellationToken);
        if (identidade is null) return Unauthorized(new { mensagem = "Credenciais inválidas ou conta inativa." });
        var token = tokens.Gerar(identidade);
        Response.Headers.CacheControl = "no-store";
        return Ok(new LoginResponse(token.Token, token.ExpiraEm, identidade));
    }

    /// <summary>Confirma que um token de desenvolvimento é válido.</summary>
    [Authorize]
    [HttpGet("eu")]
    public IActionResult Eu() => ambiente.IsDevelopment()
        ? Ok(new { id = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, perfil = User.FindFirst("role")?.Value })
        : NotFound();
}
