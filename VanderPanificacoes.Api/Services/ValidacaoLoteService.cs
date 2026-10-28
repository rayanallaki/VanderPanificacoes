using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Contracts;
using VanderPanificacoes.Api.Data;

namespace VanderPanificacoes.Api.Services;

public sealed class ValidacaoLoteService(AppDbContext db)
{
    public async Task<IReadOnlyList<string>> ValidarAsync(ReceberLoteRequest lote, CancellationToken cancellationToken)
    {
        var erros = new List<string>();
        ValidarCampos(lote, "Lote", erros);
        if (lote.IdentificadorEnvio == Guid.Empty)
            erros.Add("O identificador de envio não pode ser vazio.");

        if (lote.Visitas is null || lote.Pedidos is null)
            return erros;

        foreach (var visita in lote.Visitas)
        {
            if (visita is null) { erros.Add("A lista de visitas contém um item vazio."); continue; }
            ValidarCampos(visita, "Visita", erros);
        }
        foreach (var pedido in lote.Pedidos)
        {
            if (pedido is null) { erros.Add("A lista de pedidos contém um item vazio."); continue; }
            ValidarCampos(pedido, "Pedido", erros);
            if (pedido.Itens is null) continue;
            foreach (var item in pedido.Itens)
            {
                if (item is null) { erros.Add("A lista de itens contém um item vazio."); continue; }
                ValidarCampos(item, "Item", erros);
            }
        }
        if (erros.Count > 0) return erros;

        if (lote.Visitas.GroupBy(v => v.ClienteId).Any(g => g.Count() > 1))
            erros.Add("Um cliente não pode ter mais de uma visita no lote.");
        if (lote.Visitas.GroupBy(v => v.OrdemAtendimento).Any(g => g.Count() > 1))
            erros.Add("A ordem de atendimento não pode se repetir no lote.");
        if (lote.Pedidos.GroupBy(p => p.ClienteId).Any(g => g.Count() > 1))
            erros.Add("Um cliente não pode ter mais de um pedido no lote.");

        var pedidosPorCliente = lote.Pedidos.Select(p => p.ClienteId!.Value).ToHashSet();
        foreach (var visita in lote.Visitas)
        {
            var temPedido = pedidosPorCliente.Contains(visita.ClienteId!.Value);
            if (visita.Situacao == "PedidoRegistrado")
            {
                if (!temPedido || !string.IsNullOrWhiteSpace(visita.MotivoSemPedido))
                    erros.Add($"Visita do cliente {visita.ClienteId} deve ter pedido e não pode ter motivo sem pedido.");
            }
            else if (visita.Situacao == "SemPedido")
            {
                if (temPedido || visita.MotivoSemPedido is not ("EstabelecimentoFechado" or "ClienteAusente" or "ClienteNaoComprara" or "Outro"))
                    erros.Add($"Visita do cliente {visita.ClienteId} deve ter um motivo válido e não pode ter pedido.");
                if (visita.MotivoSemPedido == "Outro" && string.IsNullOrWhiteSpace(visita.Observacao))
                    erros.Add($"A visita do cliente {visita.ClienteId} exige observação para o motivo Outro.");
            }
            else erros.Add($"Situação inválida na visita do cliente {visita.ClienteId}.");
        }

        var visitasComPedido = lote.Visitas.Where(v => v.Situacao == "PedidoRegistrado")
            .Select(v => v.ClienteId!.Value).ToHashSet();
        foreach (var pedido in lote.Pedidos)
        {
            if (!visitasComPedido.Contains(pedido.ClienteId!.Value))
                erros.Add($"O pedido do cliente {pedido.ClienteId} não possui visita com pedido registrado.");
            if (pedido.FormaPagamentoPrevista is not ("Pix" or "Dinheiro" or "Cartao" or "Boleto"))
                erros.Add($"Forma de pagamento inválida no pedido do cliente {pedido.ClienteId}.");
            if (pedido.Itens.GroupBy(i => i.ProdutoId).Any(g => g.Count() > 1))
                erros.Add($"O pedido do cliente {pedido.ClienteId} possui produto repetido.");
            foreach (var item in pedido.Itens)
                if (decimal.Round(item.PrecoUnitario!.Value, 2) != item.PrecoUnitario.Value)
                    erros.Add($"O preço do produto {item.ProdutoId} deve ter no máximo duas casas decimais.");
        }
        if (erros.Count > 0) return erros;

        var vendedorAtivo = await db.Usuarios.AsNoTracking().AnyAsync(
            u => u.Id == lote.VendedorId && u.Perfil == "Vendedor" && u.Ativo, cancellationToken);
        if (!vendedorAtivo) erros.Add("Vendedor inexistente ou inativo.");

        var clientesIds = lote.Visitas.Select(v => v.ClienteId!.Value).Distinct().ToList();
        var clientes = await db.Clientes.AsNoTracking().Where(c => clientesIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Ativo, c.VendedorId }).ToListAsync(cancellationToken);
        foreach (var id in clientesIds)
        {
            var cliente = clientes.Find(c => c.Id == id);
            if (cliente is null || !cliente.Ativo || cliente.VendedorId != lote.VendedorId)
                erros.Add($"Cliente {id} inexistente, inativo ou vinculado a outro vendedor.");
        }

        var condicoesIds = lote.Pedidos.Select(p => p.CondicaoPagamentoId!.Value).Distinct().ToList();
        var condicoesExistentes = await db.CondicoesPagamento.AsNoTracking().Where(c => condicoesIds.Contains(c.Id))
            .Select(c => c.Id).ToListAsync(cancellationToken);
        foreach (var id in condicoesIds.Except(condicoesExistentes))
            erros.Add($"Condição de pagamento {id} não encontrada.");

        var itens = lote.Pedidos.SelectMany(p => p.Itens).ToArray();
        var produtosIds = itens.Select(i => i.ProdutoId!.Value).Distinct().ToList();
        var produtos = await db.Produtos.AsNoTracking().Where(p => produtosIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Ativo, p.Preco }).ToListAsync(cancellationToken);
        foreach (var item in itens)
        {
            var produto = produtos.Find(p => p.Id == item.ProdutoId);
            if (produto is null || !produto.Ativo)
                erros.Add($"Produto {item.ProdutoId} inexistente ou inativo.");
            else if (produto.Preco != item.PrecoUnitario)
                erros.Add($"Preço divergente para o produto {item.ProdutoId}: preço oficial {produto.Preco:F2}.");
        }
        return erros;
    }

    private static void ValidarCampos(object objeto, string contexto, List<string> erros)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(objeto, new ValidationContext(objeto), resultados, validateAllProperties: true);
        erros.AddRange(resultados.Select(resultado => $"{contexto}: {resultado.ErrorMessage}"));
    }
}
