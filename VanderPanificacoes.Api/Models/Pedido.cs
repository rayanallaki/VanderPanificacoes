namespace VanderPanificacoes.Api.Models;

public class Pedido
{
    public int Id { get; set; }
    public int LotePedidosId { get; set; }
    public LotePedidos LotePedidos { get; set; } = null!;
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public int SequenciaPedido { get; set; }
    public int CondicaoPagamentoId { get; set; }
    public CondicaoPagamento CondicaoPagamento { get; set; } = null!;
    public string NomeCondicaoSnapshot { get; set; } = string.Empty;
    public string PrazosDiasSnapshot { get; set; } = string.Empty;
    public decimal PercentualDescontoSnapshot { get; set; }
    public string FormaPagamentoPrevista { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public decimal TotalPrevisto { get; set; }
    public decimal TotalAtendido { get; set; }
    public decimal Desconto { get; set; }
    public decimal TotalFinal { get; set; }
    public string Situacao { get; set; } = "Recebido";
}

public class ItemPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;
    public string NomeProdutoSnapshot { get; set; } = string.Empty;
    public string UnidadeVendaSnapshot { get; set; } = string.Empty;
    public decimal PrecoUnitario { get; set; }
    public int QuantidadeSolicitada { get; set; }
    public int QuantidadeAtendida { get; set; }
    public decimal SubtotalAtendido { get; set; }
}
