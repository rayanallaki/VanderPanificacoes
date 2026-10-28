namespace VanderPanificacoes.Api.Models;

public class Venda
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public DateTime DataConfirmacao { get; set; }
    public decimal TotalBruto { get; set; }
    public decimal Desconto { get; set; }
    public decimal TotalLiquido { get; set; }
}

public class Parcela
{
    public int Id { get; set; }
    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;
    public int Numero { get; set; }
    public DateOnly Vencimento { get; set; }
    public decimal ValorBase { get; set; }
    public string Situacao { get; set; } = "Aberta";
    public string FormaPagamentoPrevista { get; set; } = string.Empty;
    public string? FormaPagamentoRecebida { get; set; }
    public DateOnly? DataRecebimento { get; set; }
    public decimal? ValorRecebido { get; set; }
    public int? UsuarioRecebimentoId { get; set; }
    public Usuario? UsuarioRecebimento { get; set; }
    public Guid? IdentificadorRecebimento { get; set; }
}
