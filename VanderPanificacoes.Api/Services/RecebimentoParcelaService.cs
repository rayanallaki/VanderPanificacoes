using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services
{
    /// <summary>Dados do registro manual de uma parcela integral.</summary>
    public sealed record ReceberParcelaRequest
    {
        [Required] public Guid? IdentificadorRecebimento { get; init; }
        [JsonIgnore] public int? UsuarioId { get; init; }
        [Required] public DateOnly? DataRecebimento { get; init; }
        [Required, Range(typeof(decimal), "0", "9999999999.99")] public decimal? ValorRecebido { get; init; }
        [Required, StringLength(30)] public string FormaPagamentoRecebida { get; init; } = string.Empty;
    }

    public sealed record ResultadoRecebimentoParcela(string Situacao, string Mensagem);

    public interface IRecebimentoParcelaService
    {
        Task<ResultadoRecebimentoParcela> ReceberAsync(int id, ReceberParcelaRequest request, CancellationToken cancellationToken);
    }

    public sealed class RecebimentoParcelaService(AppDbContext db) : IRecebimentoParcelaService
    {
        public async Task<ResultadoRecebimentoParcela> ReceberAsync(int id, ReceberParcelaRequest request, CancellationToken cancellationToken)
        {
            if (id <= 0 || request.IdentificadorRecebimento is null || request.IdentificadorRecebimento == Guid.Empty ||
                request.UsuarioId is null or <= 0 || request.DataRecebimento is null || request.ValorRecebido is null ||
                request.ValorRecebido < 0 || decimal.Round(request.ValorRecebido.Value, 2) != request.ValorRecebido ||
                request.FormaPagamentoRecebida is not ("Pix" or "Dinheiro" or "Cartao" or "Boleto"))
                return new("Invalido", "Informe identificador, administrador, data, valor e forma de pagamento válidos.");

            if (!await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == request.UsuarioId && u.Ativo && u.Perfil == "Administrador", cancellationToken))
                return new("Invalido", "Informe um administrador ativo.");

            var parcela = await db.Parcelas.AsNoTracking().Where(p => p.Id == id)
                .Select(p => new { p.Id, p.Situacao, p.ValorBase, p.IdentificadorRecebimento, p.UsuarioRecebimentoId,
                    p.DataRecebimento, p.ValorRecebido, p.FormaPagamentoRecebida, p.Venda.DataConfirmacao })
                .SingleOrDefaultAsync(cancellationToken);
            if (parcela is null) return new("NaoEncontrado", "Parcela não encontrada.");
            if (parcela.Situacao == "Paga")
                return Comparar(parcela.IdentificadorRecebimento, parcela.UsuarioRecebimentoId, parcela.DataRecebimento,
                    parcela.ValorRecebido, parcela.FormaPagamentoRecebida, request);

            var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso));
            var dataVenda = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(parcela.DataConfirmacao, DateTimeKind.Utc), fuso));
            if (request.DataRecebimento < dataVenda || request.DataRecebimento > hoje)
                return new("Invalido", "A data deve estar entre a confirmação da venda e a data atual da loja.");
            if (request.ValorRecebido != parcela.ValorBase)
                return new("Invalido", "O valor recebido deve ser igual ao valor integral da parcela.");

            try
            {
                var atualizadas = await db.Parcelas.Where(p => p.Id == id && p.Situacao == "Aberta" && p.ValorBase == request.ValorRecebido)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Situacao, "Paga")
                        .SetProperty(p => p.IdentificadorRecebimento, request.IdentificadorRecebimento)
                        .SetProperty(p => p.UsuarioRecebimentoId, request.UsuarioId)
                        .SetProperty(p => p.DataRecebimento, request.DataRecebimento)
                        .SetProperty(p => p.ValorRecebido, request.ValorRecebido)
                        .SetProperty(p => p.FormaPagamentoRecebida, request.FormaPagamentoRecebida), cancellationToken);
                if (atualizadas == 1) return new("Recebida", "Recebimento registrado com sucesso.");
            }
            catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
            {
                return new("Conflito", "Identificador de recebimento já usado em outra parcela.");
            }

            var atual = await db.Parcelas.AsNoTracking().Where(p => p.Id == id)
                .Select(p => new { p.Situacao, p.IdentificadorRecebimento, p.UsuarioRecebimentoId,
                    p.DataRecebimento, p.ValorRecebido, p.FormaPagamentoRecebida }).SingleOrDefaultAsync(cancellationToken);
            if (atual?.Situacao == "Paga")
                return Comparar(atual.IdentificadorRecebimento, atual.UsuarioRecebimentoId, atual.DataRecebimento,
                    atual.ValorRecebido, atual.FormaPagamentoRecebida, request);
            return new("Conflito", "A parcela foi alterada; consulte os dados e tente novamente.");
        }

        private static ResultadoRecebimentoParcela Comparar(Guid? identificador, int? usuarioId, DateOnly? data,
            decimal? valor, string? forma, ReceberParcelaRequest request)
            => identificador == request.IdentificadorRecebimento && usuarioId == request.UsuarioId &&
                data == request.DataRecebimento && valor == request.ValorRecebido && forma == request.FormaPagamentoRecebida
                ? new("Existente", "Esse recebimento já foi registrado.")
                : new("Conflito", "A parcela já foi paga com dados diferentes.");
    }
}
