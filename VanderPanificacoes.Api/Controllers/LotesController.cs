using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using VanderPanificacoes.Api.Contracts;
using VanderPanificacoes.Api.Services;

namespace VanderPanificacoes.Api.Controllers;

[ApiController]
[Route("api/lotes")]
[Authorize]
public class LotesController(ILotesService lotes, IDetalhesLoteService detalhes, ISugestaoLoteService sugestao, IConferenciaLoteService conferencia, IConfirmacaoLoteService confirmacao, ICancelamentoLoteService cancelamento, ValidacaoLoteService validacao,
    IRecebimentoLoteService recebimento, IWebHostEnvironment ambiente) : ControllerBase
{
    /// <summary>Lista os lotes recebidos para conferência administrativa.</summary>
    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(IReadOnlyList<LoteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();
        return Ok(await lotes.ListarAsync(cancellationToken));
    }

    /// <summary>Lista os lotes pertencentes ao vendedor autenticado.</summary>
    [HttpGet("meus")]
    [Authorize(Roles = "Vendedor")]
    [ProducesResponseType(typeof(IReadOnlyList<LoteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarMeus(CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        if (!TryObterUsuarioId(out var vendedorId)) return Unauthorized();
        return Ok(await lotes.ListarDoVendedorAsync(vendedorId, cancellationToken));
    }

    /// <summary>Consulta as visitas, pedidos e itens de um lote.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Administrador,Vendedor")]
    [ProducesResponseType(typeof(DetalhesLoteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> BuscarDetalhes(int id, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        if (!TryObterUsuarioId(out var usuarioId)) return Unauthorized();
        int? vendedorId = User.IsInRole("Vendedor") ? usuarioId : null;
        var resultado = await detalhes.BuscarAsync(id, vendedorId, cancellationToken);
        return resultado is null ? NotFound(new { mensagem = "Lote não encontrado." }) : Ok(resultado);
    }

    /// <summary>Simula o atendimento do lote sem reservar ou alterar estoque.</summary>
    [HttpGet("{id:int}/sugestao")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(SugestaoLoteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sugerir(int id, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        var resultado = await sugestao.CalcularAsync(id, cancellationToken);
        if (resultado.Erro == "Lote não encontrado.") return NotFound(new { mensagem = resultado.Erro });
        if (resultado.Erro is not null) return Conflict(new { mensagem = resultado.Erro });
        return Ok(resultado.Sugestao);
    }

    /// <summary>Calcula uma prévia das quantidades e valores sem confirmar o lote.</summary>
    [HttpPost("{id:int}/conferencia")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ConferenciaLoteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Conferir(int id, ConferenciaLoteRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        var resultado = await conferencia.SimularAsync(id, request, cancellationToken);
        if (resultado.Erro == "Lote não encontrado.") return NotFound(new { mensagem = resultado.Erro });
        if (resultado.Erro == "A conferência só está disponível para lotes recebidos.") return Conflict(new { mensagem = resultado.Erro });
        if (resultado.Erro is not null) return BadRequest(new { mensagem = resultado.Erro });
        return Ok(resultado.Conferencia);
    }

    /// <summary>Confirma as quantidades atendidas e registra vendas, parcelas e movimentações.</summary>
    [HttpPost("{id:int}/confirmacao")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Confirmar(int id, ConfirmarLoteRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        if (!TryObterUsuarioId(out var usuarioId)) return Unauthorized();
        var resultado = await confirmacao.ConfirmarAsync(id, request with { UsuarioId = usuarioId }, cancellationToken);
        return resultado.Situacao switch
        {
            "NaoEncontrado" => NotFound(new { mensagem = resultado.Mensagem }),
            "Conflito" => Conflict(new { mensagem = resultado.Mensagem }),
            "Invalido" => BadRequest(new { mensagem = resultado.Mensagem }),
            _ => Ok(new { mensagem = resultado.Mensagem })
        };
    }

    /// <summary>Cancela um lote recebido e preserva seu histórico.</summary>
    [HttpPost("{id:int}/cancelamento")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Cancelar(int id, CancelarLoteRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        if (!TryObterUsuarioId(out var usuarioId)) return Unauthorized();
        var resultado = await cancelamento.CancelarAsync(id, request with { UsuarioId = usuarioId }, cancellationToken);
        return resultado.Situacao switch
        {
            "NaoEncontrado" => NotFound(new { mensagem = resultado.Mensagem }),
            "Conflito" => Conflict(new { mensagem = resultado.Mensagem }),
            "Invalido" => BadRequest(new { mensagem = resultado.Mensagem }),
            _ => Ok(new { mensagem = resultado.Mensagem })
        };
    }

    /// <summary>Consulta o recebimento de um lote pelo identificador de envio.</summary>
    [HttpGet("envios/{identificadorEnvio:guid}")]
    [Authorize(Roles = "Vendedor")]
    [ProducesResponseType(typeof(LoteResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> BuscarPorEnvio(Guid identificadorEnvio, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();
        if (!TryObterUsuarioId(out var vendedorId)) return Unauthorized();
        if (identificadorEnvio == Guid.Empty)
            return BadRequest(new { mensagem = "Informe um identificador de envio válido." });
        var lote = await lotes.BuscarPorEnvioAsync(identificadorEnvio, vendedorId, cancellationToken);
        return lote is null ? NotFound(new { mensagem = "Lote não encontrado para este vendedor." }) : Ok(lote);
    }

    /// <summary>Valida um lote para testes sem gravá-lo no banco.</summary>
    [HttpPost("validacao")]
    [Authorize(Roles = "Vendedor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validar(ReceberLoteRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        if (!TryObterUsuarioId(out var vendedorId)) return Unauthorized();
        var erros = await validacao.ValidarAsync(request with { VendedorId = vendedorId }, cancellationToken);
        if (erros.Count > 0)
            return BadRequest(new { mensagem = "O lote não passou na validação.", erros });

        return Ok(new { mensagem = "Lote válido para esta etapa; nenhum dado foi gravado." });
    }

    /// <summary>Recebe um lote concluído no ambiente de desenvolvimento.</summary>
    [HttpPost]
    [Authorize(Roles = "Vendedor")]
    public async Task<IActionResult> Receber(ReceberLoteRequest request, CancellationToken cancellationToken)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        if (!TryObterUsuarioId(out var vendedorId)) return Unauthorized();
        var resultado = await recebimento.ReceberAsync(request with { VendedorId = vendedorId }, cancellationToken);
        return resultado.Situacao switch
        {
            "Criado" => Created("/api/lotes", new { id = resultado.LoteId, mensagem = "Lote recebido." }),
            "Existente" => Ok(new { id = resultado.LoteId, mensagem = "Lote já recebido." }),
            "Conflito" => Conflict(new { erros = resultado.Erros }),
            "Temporario" => StatusCode(StatusCodes.Status503ServiceUnavailable, new { erros = resultado.Erros }),
            _ => BadRequest(new { erros = resultado.Erros })
        };
    }

    private bool TryObterUsuarioId(out int id)
        => int.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out id) && id > 0;
}
