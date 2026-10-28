namespace VanderPanificacoes.Api.Models;

public class MovimentacaoEstoque
{
    public int Id { get; set; }
    public Guid IdentificadorOperacao { get; set; }
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;
    public string Tipo { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTime DataHora { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int? VendaId { get; set; }
    public Venda? Venda { get; set; }
}
