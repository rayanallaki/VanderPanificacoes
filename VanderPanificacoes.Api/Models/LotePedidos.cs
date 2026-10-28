namespace VanderPanificacoes.Api.Models;

public class LotePedidos
{
    public int Id { get; set; }
    public Guid IdentificadorEnvio { get; set; }
    public string HashConteudo { get; set; } = string.Empty;
    public string? HashConfirmacao { get; set; }
    public int VendedorId { get; set; }
    public Usuario Vendedor { get; set; } = null!;
    public DateTime DataCriacaoLocal { get; set; }
    public DateTime DataRecebimento { get; set; }
    public DateTime? DataConfirmacao { get; set; }
    public string Situacao { get; set; } = "Recebido";
    public string? MotivoCancelamento { get; set; }
    public Guid? IdentificadorCancelamento { get; set; }
    public int? UsuarioCancelamentoId { get; set; }
    public Usuario? UsuarioCancelamento { get; set; }
    public int? UsuarioConfirmacaoId { get; set; }
    public Usuario? UsuarioConfirmacao { get; set; }
}
