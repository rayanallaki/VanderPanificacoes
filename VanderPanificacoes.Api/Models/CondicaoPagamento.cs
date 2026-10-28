namespace VanderPanificacoes.Api.Models;

public class CondicaoPagamento
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeParcelas { get; set; }
    public string PrazosDias { get; set; } = string.Empty;
    public decimal PercentualDesconto { get; set; }
}
