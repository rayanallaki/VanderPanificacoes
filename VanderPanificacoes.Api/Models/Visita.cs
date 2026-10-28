namespace VanderPanificacoes.Api.Models;

public class Visita
{
    public int Id { get; set; }
    public int LotePedidosId { get; set; }
    public LotePedidos LotePedidos { get; set; } = null!;
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public int PrioridadeOriginal { get; set; }
    public int OrdemAtendimento { get; set; }
    public string Situacao { get; set; } = string.Empty;
    public string? MotivoSemPedido { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataHoraAtendimento { get; set; }
}
