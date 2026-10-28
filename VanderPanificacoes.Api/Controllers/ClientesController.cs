using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public class ClientesController(IClientesService clientes, IWebHostEnvironment ambiente) : ControllerBase
{
    /// <summary>Lista os clientes cadastrados, inclusive os inativos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClienteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] string? nome, [FromQuery] string? documento,
        [FromQuery] int? vendedorId, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        if (vendedorId.HasValue && vendedorId.Value <= 0)
            return BadRequest(new { mensagem = "O ID do vendedor deve ser maior que zero." });

        string? documentoNormalizado = null;
        if (!string.IsNullOrWhiteSpace(documento))
        {
            if (documento.Any(c => !(c is >= '0' and <= '9' || c is '.' or '-' or '/' or ' ')))
                return BadRequest(new { mensagem = "Informe um documento válido." });

            documentoNormalizado = new string(documento.Where(c => c is >= '0' and <= '9').ToArray());
            if (documentoNormalizado.Length is < 1 or > 20)
                return BadRequest(new { mensagem = "O documento deve conter de 1 a 20 dígitos." });
        }

        return Ok(await clientes.ListarAsync(nome, documentoNormalizado, vendedorId, cancellationToken));
    }

    /// <summary>Cadastra um cliente vinculado a um vendedor ativo.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClienteResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarClienteRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        var (documento, erro) = ValidarDados(request);
        if (erro is not null)
            return BadRequest(new { mensagem = erro });

        var resultado = await clientes.CriarAsync(request, documento!, cancellationToken);
        if (resultado.Erro == "Documento já cadastrado.")
            return Conflict(new { mensagem = resultado.Erro });
        if (resultado.Erro is not null)
            return BadRequest(new { mensagem = resultado.Erro });
        return Created("/api/clientes", resultado.Cliente);
    }

    /// <summary>Atualiza os dados de um cliente e seu vendedor responsável.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ClienteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Editar(int id, CriarClienteRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        var (documento, erro) = ValidarDados(request);
        if (erro is not null)
            return BadRequest(new { mensagem = erro });

        var resultado = await clientes.EditarAsync(id, request, documento!, cancellationToken);
        if (resultado.Erro == "Cliente não encontrado.")
            return NotFound(new { mensagem = resultado.Erro });
        if (resultado.Erro == "Documento já cadastrado.")
            return Conflict(new { mensagem = resultado.Erro });
        if (resultado.Erro is not null)
            return BadRequest(new { mensagem = resultado.Erro });
        return Ok(resultado.Cliente);
    }

    /// <summary>Desativa um cliente preservando seu cadastro.</summary>
    [HttpPatch("{id:int}/desativar")]
    [ProducesResponseType(typeof(ClienteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Desativar(int id, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        var cliente = await clientes.DesativarAsync(id, cancellationToken);
        if (cliente is null)
            return NotFound(new { mensagem = "Cliente não encontrado." });

        return Ok(cliente);
    }

    /// <summary>Reativa um cliente preservando seu cadastro.</summary>
    [HttpPatch("{id:int}/reativar")]
    [ProducesResponseType(typeof(ClienteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reativar(int id, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        var cliente = await clientes.ReativarAsync(id, cancellationToken);
        if (cliente is null)
            return NotFound(new { mensagem = "Cliente não encontrado." });

        return Ok(cliente);
    }

    private static (string? Documento, string? Erro) ValidarDados(CriarClienteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) ||
            request.Documento.Any(c => !(c is >= '0' and <= '9' || c is '.' or '-' or '/' or ' ')))
            return (null, "Informe um nome e um documento válidos.");

        var documento = new string(request.Documento.Where(c => c is >= '0' and <= '9').ToArray());
        return documento.Length is < 1 or > 20
            ? (null, "O documento deve conter de 1 a 20 dígitos.")
            : (documento, null);
    }
}
