using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Controllers;

/// <summary>Consulta as vendas geradas pela confirmação dos lotes.</summary>
[ApiController]
[Route("api/vendas")]
[Authorize(Roles = "Administrador")]
public class VendasController(AppDbContext db, IWebHostEnvironment ambiente) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();

        var vendas = await db.Vendas.AsNoTracking()
            .OrderByDescending(venda => venda.DataConfirmacao).ThenByDescending(venda => venda.Id)
            .Select(venda => new
            {
                venda.Id, venda.PedidoId, venda.Pedido.LotePedidosId,
                venda.Pedido.ClienteId, Cliente = venda.Pedido.Cliente.Nome,
                venda.DataConfirmacao, venda.TotalBruto, venda.Desconto, venda.TotalLiquido
            })
            .ToListAsync(cancellationToken);
        return Ok(vendas);
    }
}
