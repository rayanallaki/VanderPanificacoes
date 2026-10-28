using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Controllers;

[ApiController]
[Route("api/carteira")]
[Authorize(Roles = "Vendedor")]
public class CarteiraController(ICarteiraService carteira, IWebHostEnvironment ambiente) : ControllerBase
{
    /// <summary>Lista os clientes ativos vinculados ao vendedor autenticado.</summary>
    [HttpGet("clientes")]
    [ProducesResponseType(typeof(IReadOnlyList<ClienteCarteiraResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarClientes(CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        if (!int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var vendedorId) || vendedorId <= 0)
            return Unauthorized();
        return Ok(await carteira.ListarAsync(vendedorId, cancellationToken));
    }
}
