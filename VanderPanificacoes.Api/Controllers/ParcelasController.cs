using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Controllers;

[ApiController]
[Route("api/parcelas")]
[Authorize(Roles = "Administrador")]
public class ParcelasController(IParcelasService parcelas, IRecebimentoParcelaService recebimento, IWebHostEnvironment ambiente) : ControllerBase
{
    /// <summary>Consulta parcelas por cliente, vencimento e situação.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ParcelaResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] int? clienteId, [FromQuery] DateOnly? inicio,
        [FromQuery] DateOnly? fim, [FromQuery] string? situacao, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        if (clienteId.HasValue && clienteId.Value <= 0)
            return BadRequest(new { mensagem = "O ID do cliente deve ser maior que zero." });
        if (inicio.HasValue && fim.HasValue && inicio > fim)
            return BadRequest(new { mensagem = "A data inicial não pode ser posterior à final." });
        if (situacao is not null && situacao is not ("Aberta" or "Vencida" or "Paga"))
            return BadRequest(new { mensagem = "A situação deve ser Aberta, Vencida ou Paga." });

        return Ok(await parcelas.ListarAsync(clienteId, inicio, fim, situacao, cancellationToken));
    }

    /// <summary>Registra o recebimento integral de uma parcela em desenvolvimento.</summary>
    [HttpPost("{id:int}/recebimento")]
    public async Task<IActionResult> Receber(int id, ReceberParcelaRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        if (!int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var usuarioId) || usuarioId <= 0)
            return Unauthorized();
        var resultado = await recebimento.ReceberAsync(id, request with { UsuarioId = usuarioId }, cancellationToken);
        return resultado.Situacao switch
        {
            "NaoEncontrado" => NotFound(new { mensagem = resultado.Mensagem }),
            "Conflito" => Conflict(new { mensagem = resultado.Mensagem }),
            "Invalido" => BadRequest(new { mensagem = resultado.Mensagem }),
            _ => Ok(new { mensagem = resultado.Mensagem })
        };
    }
}
