using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

/// <summary>Dados necessários para cancelar um lote recebido.</summary>
public sealed record CancelarLoteRequest
{
    [Required] public Guid? IdentificadorCancelamento { get; init; }
    [Range(1, int.MaxValue)] public int? UsuarioId { get; init; }
    [Required, StringLength(500)] public string Motivo { get; init; } = string.Empty;
}

public sealed record ResultadoCancelamento(string Situacao, string Mensagem);

public interface ICancelamentoLoteService
{
    Task<ResultadoCancelamento> CancelarAsync(int id, CancelarLoteRequest request, CancellationToken cancellationToken);
}

public sealed class CancelamentoLoteService(AppDbContext db) : ICancelamentoLoteService
{
    public async Task<ResultadoCancelamento> CancelarAsync(int id, CancelarLoteRequest request, CancellationToken cancellationToken)
    {
        if (id <= 0 || request.IdentificadorCancelamento is null || request.IdentificadorCancelamento == Guid.Empty ||
            request.UsuarioId is null or <= 0 || string.IsNullOrWhiteSpace(request.Motivo))
            return new("Invalido", "Informe o identificador, um administrador e o motivo do cancelamento.");

        var motivo = request.Motivo.Trim();
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.Usuarios.AnyAsync(u => u.Id == request.UsuarioId && u.Perfil == "Administrador" && u.Ativo, cancellationToken))
            return new("Invalido", "Informe um administrador ativo.");

        int atualizados;
        try
        {
            atualizados = await db.LotesPedidos.Where(l => l.Id == id && l.Situacao == "Recebido")
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.Situacao, "Cancelado")
                    .SetProperty(l => l.MotivoCancelamento, motivo)
                    .SetProperty(l => l.IdentificadorCancelamento, request.IdentificadorCancelamento)
                    .SetProperty(l => l.UsuarioCancelamentoId, request.UsuarioId), cancellationToken);
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            return new("Conflito", "Identificador de cancelamento já utilizado em outro lote.");
        }

        if (atualizados == 0)
        {
            var existente = await db.LotesPedidos.AsNoTracking().Where(l => l.Id == id)
                .Select(l => new { l.Situacao, l.IdentificadorCancelamento, l.UsuarioCancelamentoId, l.MotivoCancelamento })
                .SingleOrDefaultAsync(cancellationToken);
            if (existente is null) return new("NaoEncontrado", "Lote não encontrado.");
            if (existente.Situacao == "Cancelado" && existente.IdentificadorCancelamento == request.IdentificadorCancelamento &&
                existente.UsuarioCancelamentoId == request.UsuarioId && existente.MotivoCancelamento == motivo)
                return new("Existente", "Esse cancelamento já foi registrado.");
            return new("Conflito", "O lote já está confirmado ou foi cancelado com outros dados.");
        }

        await db.Pedidos.Where(p => p.LotePedidosId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Situacao, "Cancelado"), cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return new("Cancelado", "Lote cancelado com sucesso.");
    }
}
