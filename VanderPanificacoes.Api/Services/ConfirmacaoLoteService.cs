using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Data;
using VanderPanificacoes.Api.Models;

namespace VanderPanificacoes.Api.Services
{
    /// <summary>Dados da confirmação de um lote em desenvolvimento.</summary>
    public sealed record ConfirmarLoteRequest
    {
        [Range(1, int.MaxValue)] public int? UsuarioId { get; init; }
        [Required, MinLength(1)] public IReadOnlyList<ItemConferenciaRequest> Itens { get; init; } = [];
    }

    public sealed record ResultadoConfirmacao(string Situacao, string Mensagem);

    public interface IConfirmacaoLoteService
    {
        Task<ResultadoConfirmacao> ConfirmarAsync(int id, ConfirmarLoteRequest request, CancellationToken cancellationToken);
    }

    public sealed class ConfirmacaoLoteService(AppDbContext db) : IConfirmacaoLoteService
    {
        public async Task<ResultadoConfirmacao> ConfirmarAsync(int id, ConfirmarLoteRequest request, CancellationToken cancellationToken)
        {
            if (request.UsuarioId is null or <= 0 || request.Itens is null || request.Itens.Count == 0 ||
                request.Itens.Any(i => i is null || i.ItemPedidoId is null or <= 0 || i.QuantidadeAtendida is null or < 0) ||
                request.Itens.GroupBy(i => i.ItemPedidoId).Any(g => g.Count() > 1))
                return new("Invalido", "Informe o administrador e uma quantidade válida para cada item.");

            var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                request.UsuarioId,
                Itens = request.Itens.OrderBy(i => i.ItemPedidoId).Select(i => new { i.ItemPedidoId, i.QuantidadeAtendida })
            })));
            var agora = DateTime.UtcNow;
            var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var dataLoja = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agora, fuso));
            await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);
            if (!await db.Usuarios.AnyAsync(u => u.Id == request.UsuarioId && u.Ativo && u.Perfil == "Administrador", cancellationToken))
                return new("Invalido", "Informe um administrador ativo.");

            var atualizados = await db.LotesPedidos.Where(l => l.Id == id && l.Situacao == "Recebido")
                .ExecuteUpdateAsync(set => set.SetProperty(l => l.Situacao, "Confirmado")
                    .SetProperty(l => l.DataConfirmacao, agora)
                    .SetProperty(l => l.UsuarioConfirmacaoId, request.UsuarioId)
                    .SetProperty(l => l.HashConfirmacao, hash), cancellationToken);
            if (atualizados == 0)
            {
                var existente = await db.LotesPedidos.AsNoTracking().SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
                if (existente is null) return new("NaoEncontrado", "Lote não encontrado.");
                if (existente.Situacao == "Confirmado" && existente.HashConfirmacao == hash)
                    return new("Existente", "Lote já confirmado com os mesmos dados.");
                return new("Conflito", "Lote já confirmado com outros dados ou cancelado.");
            }

            // A conferência só aceita lotes Recebidos. Dentro da transação, a atualização acima
            // bloqueia confirmações concorrentes; por isso, validamos a proposta diretamente abaixo.
            var itens = await db.ItensPedido.Where(i => i.Pedido.LotePedidosId == id)
                .OrderBy(i => i.Id).ToListAsync(cancellationToken);
            var quantidades = request.Itens.ToDictionary(i => i.ItemPedidoId!.Value, i => i.QuantidadeAtendida!.Value);
            if (itens.Count == 0 || itens.Count != quantidades.Count || itens.Any(i => !quantidades.ContainsKey(i.Id)))
                return new("Invalido", "Informe exatamente uma quantidade para cada item do lote.");
            if (itens.Any(i => quantidades[i.Id] > i.QuantidadeSolicitada))
                return new("Invalido", "A quantidade atendida não pode superar a solicitada.");
            if (itens.All(i => quantidades[i.Id] == 0))
                return new("Invalido", "Um lote sem itens atendidos deve ser cancelado.");

            var pedidos = await db.Pedidos.Where(p => p.LotePedidosId == id).OrderBy(p => p.SequenciaPedido).ToListAsync(cancellationToken);
            var calculo = new CalculoVendaService();
            var propostas = new Dictionary<int, VendaCalculada>();
            foreach (var pedido in pedidos)
            {
                var itensPedido = itens.Where(i => i.PedidoId == pedido.Id).ToList();
                if (itensPedido.Count == 0) return new("Invalido", "Um pedido do lote não possui itens.");
                if (itensPedido.All(i => quantidades[i.Id] == 0)) continue;
                var prazos = pedido.PrazosDiasSnapshot.Split(',').Select(int.Parse).ToArray();
                propostas.Add(pedido.Id, calculo.Calcular(itensPedido.Select(i => new ItemCalculoVenda(i.PrecoUnitario, quantidades[i.Id])).ToList(),
                    pedido.PercentualDescontoSnapshot, prazos, dataLoja));
            }

            if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso)) != dataLoja)
                return new("Conflito", "A data da loja mudou. Revise e confirme o lote novamente.");

            foreach (var grupo in itens.GroupBy(i => i.ProdutoId).OrderBy(g => g.Key))
            {
                var total = grupo.Sum(i => (long)quantidades[i.Id]);
                if (total == 0) continue;
                if (total > int.MaxValue) return new("Conflito", $"Quantidade excessiva para o produto {grupo.Key}.");
                var quantidade = (int)total;
                var baixados = await db.Produtos.Where(p => p.Id == grupo.Key && p.Ativo &&
                        (!p.DataValidade.HasValue || p.DataValidade >= dataLoja) && p.Estoque >= quantidade)
                    .ExecuteUpdateAsync(set => set.SetProperty(p => p.Estoque, p => p.Estoque - quantidade), cancellationToken);
                if (baixados == 0)
                    return new("Conflito", $"Produto {grupo.Key} sem estoque suficiente, inativo ou vencido. Revise a conferência.");
            }

            foreach (var pedido in pedidos)
            {
                var itensPedido = itens.Where(i => i.PedidoId == pedido.Id).ToList();
                foreach (var item in itensPedido)
                {
                    item.QuantidadeAtendida = quantidades[item.Id];
                    item.SubtotalAtendido = decimal.Round(item.PrecoUnitario * item.QuantidadeAtendida, 2, MidpointRounding.AwayFromZero);
                }
                if (!propostas.TryGetValue(pedido.Id, out var valores))
                {
                    pedido.Situacao = "NaoAtendido";
                    continue;
                }

                pedido.Situacao = itensPedido.All(i => i.QuantidadeAtendida == i.QuantidadeSolicitada) ? "AtendidoTotal" : "AtendidoParcial";
                pedido.TotalAtendido = valores.TotalBruto;
                pedido.Desconto = valores.Desconto;
                pedido.TotalFinal = valores.TotalLiquido;
                var venda = new Venda { PedidoId = pedido.Id, DataConfirmacao = agora,
                    TotalBruto = valores.TotalBruto, Desconto = valores.Desconto, TotalLiquido = valores.TotalLiquido };
                db.Vendas.Add(venda);
                foreach (var parcela in valores.Parcelas)
                    db.Parcelas.Add(new Parcela { Venda = venda, Numero = parcela.Numero, Vencimento = parcela.Vencimento,
                        ValorBase = parcela.Valor, Situacao = "Aberta", FormaPagamentoPrevista = pedido.FormaPagamentoPrevista });
                foreach (var item in itensPedido.Where(i => i.QuantidadeAtendida > 0))
                    db.MovimentacoesEstoque.Add(new MovimentacaoEstoque { IdentificadorOperacao = Guid.NewGuid(),
                        ProdutoId = item.ProdutoId, Tipo = "Venda", Quantidade = item.QuantidadeAtendida,
                        Motivo = $"Confirmação do lote {id}", DataHora = agora, UsuarioId = request.UsuarioId.Value, Venda = venda });
            }

            await db.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            return new("Confirmado", "Lote confirmado com sucesso.");
        }
    }
}
