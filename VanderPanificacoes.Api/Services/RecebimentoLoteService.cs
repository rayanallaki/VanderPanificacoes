using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Contracts;
using VanderPanificacoes.Api.Data;
using VanderPanificacoes.Api.Models;

namespace VanderPanificacoes.Api.Services;

public sealed record ResultadoRecebimentoLote(string Situacao, int? LoteId, IReadOnlyList<string> Erros);

public interface IRecebimentoLoteService
{
    Task<ResultadoRecebimentoLote> ReceberAsync(ReceberLoteRequest request, CancellationToken cancellationToken);
}

public sealed class RecebimentoLoteService(AppDbContext db, ValidacaoLoteService validacao) : IRecebimentoLoteService
{
    private const decimal LimiteTotal = 9999999999.99m;

    public async Task<ResultadoRecebimentoLote> ReceberAsync(ReceberLoteRequest request, CancellationToken cancellationToken)
    {
        const int tentativasMaximas = 3;
        for (var tentativa = 1; tentativa <= tentativasMaximas; tentativa++)
        {
            try
            {
                return await ReceberTentativaAsync(request, cancellationToken);
            }
            catch (Exception ex) when (EhBloqueioTemporario(ex))
            {
                db.ChangeTracker.Clear();
                if (tentativa == tentativasMaximas)
                    return new("Temporario", null, ["Não foi possível concluir o envio devido a operações simultâneas. Tente novamente com o mesmo identificador e conteúdo."]);
                await Task.Delay(50 * tentativa, cancellationToken);
            }
        }
        throw new InvalidOperationException("Limite de tentativas inesperado.");
    }

    private static bool EhBloqueioTemporario(Exception erro)
    {
        for (Exception? atual = erro; atual is not null; atual = atual.InnerException)
            if (atual is MySqlConnector.MySqlException { Number: 1213 or 1205 }) return true;
        return false;
    }

    private async Task<ResultadoRecebimentoLote> ReceberTentativaAsync(ReceberLoteRequest request, CancellationToken cancellationToken)
    {
        if (request.IdentificadorEnvio is null || request.IdentificadorEnvio == Guid.Empty ||
            request.VendedorId is null || request.DataCriacaoLocal is null || request.Visitas is null || request.Pedidos is null ||
            request.Visitas.Any(v => v is null) || request.Pedidos.Any(p => p is null || p.Itens is null || p.Itens.Any(i => i is null)))
            return new("Invalido", null, ["Informe os dados completos do lote."]);

        var hash = CalcularHash(request);
        await using var transacao = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ativo = await db.Usuarios.AnyAsync(u => u.Id == request.VendedorId && u.Perfil == "Vendedor" && u.Ativo, cancellationToken);
        if (!ativo)
            return new("Invalido", null, ["Vendedor inexistente ou inativo."]);

        var existente = await db.LotesPedidos.AsNoTracking()
            .SingleOrDefaultAsync(l => l.IdentificadorEnvio == request.IdentificadorEnvio, cancellationToken);
        if (existente is not null)
            return Comparar(existente, request.VendedorId.Value, hash);

        var erros = await validacao.ValidarAsync(request, cancellationToken);
        if (erros.Count > 0)
            return new("Invalido", null, erros);

        var produtosIds = request.Pedidos.SelectMany(p => p.Itens).Select(i => i.ProdutoId!.Value).Distinct().ToList();
        var condicoesIds = request.Pedidos.Select(p => p.CondicaoPagamentoId!.Value).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking().Where(p => produtosIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        var condicoes = await db.CondicoesPagamento.AsNoTracking().Where(c => condicoesIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        var totais = new List<decimal>();
        foreach (var pedido in request.Pedidos)
        {
            decimal bruto = 0;
            foreach (var item in pedido.Itens)
            {
                var subtotal = decimal.Round(produtos[item.ProdutoId!.Value].Preco * item.QuantidadeSolicitada!.Value, 2, MidpointRounding.AwayFromZero);
                if (subtotal > LimiteTotal || bruto > LimiteTotal - subtotal)
                    return new("Invalido", null, [$"O total do pedido do cliente {pedido.ClienteId} excede o limite permitido."]);
                bruto += subtotal;
            }
            var desconto = decimal.Round(bruto * condicoes[pedido.CondicaoPagamentoId!.Value].PercentualDesconto / 100m, 2, MidpointRounding.AwayFromZero);
            totais.Add(bruto - desconto);
        }

        var lote = new LotePedidos
        {
            IdentificadorEnvio = request.IdentificadorEnvio.Value, HashConteudo = hash,
            VendedorId = request.VendedorId.Value, DataCriacaoLocal = request.DataCriacaoLocal.Value.UtcDateTime,
            DataRecebimento = DateTime.UtcNow, Situacao = "Recebido"
        };
        db.LotesPedidos.Add(lote);
        foreach (var visita in request.Visitas)
            db.Visitas.Add(new Visita
            {
                LotePedidos = lote, ClienteId = visita.ClienteId!.Value, PrioridadeOriginal = visita.PrioridadeOriginal!.Value,
                OrdemAtendimento = visita.OrdemAtendimento!.Value, Situacao = visita.Situacao,
                MotivoSemPedido = visita.MotivoSemPedido, Observacao = visita.Observacao,
                DataHoraAtendimento = visita.DataHoraAtendimento!.Value.UtcDateTime
            });

        for (var i = 0; i < request.Pedidos.Count; i++)
        {
            var dados = request.Pedidos[i];
            var condicao = condicoes[dados.CondicaoPagamentoId!.Value];
            var pedido = new Pedido
            {
                LotePedidos = lote, ClienteId = dados.ClienteId!.Value, SequenciaPedido = i + 1,
                CondicaoPagamentoId = condicao.Id, NomeCondicaoSnapshot = condicao.Nome,
                PrazosDiasSnapshot = condicao.PrazosDias, PercentualDescontoSnapshot = condicao.PercentualDesconto,
                FormaPagamentoPrevista = dados.FormaPagamentoPrevista, Observacao = dados.Observacao,
                TotalPrevisto = totais[i], Situacao = "Recebido"
            };
            db.Pedidos.Add(pedido);
            foreach (var item in dados.Itens)
            {
                var produto = produtos[item.ProdutoId!.Value];
                db.ItensPedido.Add(new ItemPedido
                {
                    Pedido = pedido, ProdutoId = produto.Id, NomeProdutoSnapshot = produto.Nome,
                    UnidadeVendaSnapshot = produto.UnidadeVenda, PrecoUnitario = produto.Preco,
                    QuantidadeSolicitada = item.QuantidadeSolicitada!.Value
                });
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return new("Criado", lote.Id, []);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException { Number: 1062 })
        {
            await transacao.RollbackAsync(cancellationToken);
            await transacao.DisposeAsync();
            db.ChangeTracker.Clear();
            existente = await db.LotesPedidos.AsNoTracking().SingleOrDefaultAsync(l => l.IdentificadorEnvio == request.IdentificadorEnvio, cancellationToken);
            if (existente is not null)
                return Comparar(existente, request.VendedorId.Value, hash);
            throw;
        }
    }

    private static ResultadoRecebimentoLote Comparar(LotePedidos lote, int vendedorId, string hash)
        => lote.VendedorId == vendedorId && lote.HashConteudo == hash
            ? new("Existente", lote.Id, [])
            : new("Conflito", null, ["Identificador já utilizado para outro conteúdo ou vendedor."]);

    private static string CalcularHash(ReceberLoteRequest lote)
    {
        var conteudo = new
        {
            lote.VendedorId, DataCriacaoLocal = lote.DataCriacaoLocal!.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            Visitas = lote.Visitas.Select(v => new { v.ClienteId, v.PrioridadeOriginal, v.OrdemAtendimento, v.Situacao,
                v.MotivoSemPedido, v.Observacao, DataHoraAtendimento = v.DataHoraAtendimento?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) }),
            Pedidos = lote.Pedidos.Select(p => new { p.ClienteId, p.CondicaoPagamentoId, p.FormaPagamentoPrevista, p.Observacao,
                Itens = p.Itens.Select(i => new { i.ProdutoId, i.QuantidadeSolicitada, PrecoUnitario = i.PrecoUnitario?.ToString("G29", CultureInfo.InvariantCulture) }) })
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(conteudo)));
    }
}
