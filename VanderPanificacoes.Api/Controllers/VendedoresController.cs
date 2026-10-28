using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/[controller]")]
public class VendedoresController(IVendedoresService vendedores, IWebHostEnvironment ambiente) : ControllerBase
{
    /// <summary>Lista os vendedores cadastrados, incluindo os inativos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VendedorResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        return Ok(await vendedores.ListarAsync(cancellationToken));
    }

    /// <summary>Cadastra um vendedor no ambiente de desenvolvimento.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VendedorResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarVendedorRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        var nome = request.Nome.Trim();
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(request.Senha))
            return BadRequest(new { mensagem = "Informe nome e senha válidos." });

        var vendedor = await vendedores.CriarAsync(nome, email, request.Senha, cancellationToken);
        if (vendedor is null)
            return Conflict(new { mensagem = "Este e-mail já está cadastrado." });

        return Created("/api/vendedores", vendedor);
    }

    /// <summary>Atualiza o nome e o e-mail de um vendedor.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(VendedorResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Editar(int id, EditarVendedorRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        var nome = request.Nome.Trim();
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return BadRequest(new { mensagem = "Informe o nome do vendedor." });

        var resultado = await vendedores.EditarAsync(id, nome, email, cancellationToken);
        if (resultado.EmailEmUso)
            return Conflict(new { mensagem = "Este e-mail já está cadastrado." });
        if (resultado.Vendedor is null)
            return NotFound(new { mensagem = "Vendedor não encontrado." });

        return Ok(resultado.Vendedor);
    }

    /// <summary>Desativa um vendedor preservando seu cadastro.</summary>
    [HttpPatch("{id:int}/desativar")]
    [ProducesResponseType(typeof(VendedorResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Desativar(int id, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        var vendedor = await vendedores.DesativarAsync(id, cancellationToken);
        if (vendedor is null)
            return NotFound(new { mensagem = "Vendedor não encontrado." });

        return Ok(vendedor);
    }
}

/// <summary>Dados editáveis de um vendedor.</summary>
public sealed record EditarVendedorRequest
{
    [Required, StringLength(100)] public string Nome { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; init; } = string.Empty;
}

/// <summary>Dados necessários para cadastrar um vendedor.</summary>
public sealed record CriarVendedorRequest
{
    [Required, StringLength(100)] public string Nome { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)] public string Senha { get; init; } = string.Empty;
}
