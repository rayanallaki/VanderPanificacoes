using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador,Vendedor")]
[Route("api/condicoes-pagamento")]
public class CondicoesPagamentoController(ICondicoesPagamentoService condicoes, IWebHostEnvironment ambiente) : ControllerBase
{
    /// <summary>Lista as condições fixas de pagamento.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CondicaoPagamentoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        return Ok(await condicoes.ListarAsync(cancellationToken));
    }
}
