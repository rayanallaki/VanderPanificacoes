using System.ComponentModel.DataAnnotations;

namespace VanderPanificacoes.Api.Contracts;

/// <summary>Dados de um ciclo concluído enviado pelo aplicativo.</summary>
public sealed record ReceberLoteRequest
{
    [Required] public Guid? IdentificadorEnvio { get; init; }
    [Range(1, int.MaxValue)] public int? VendedorId { get; init; }
    [Required] public DateTimeOffset? DataCriacaoLocal { get; init; }
    [Required, MinLength(1)] public IReadOnlyList<VisitaLoteRequest> Visitas { get; init; } = [];
    [Required, MinLength(1)] public IReadOnlyList<PedidoLoteRequest> Pedidos { get; init; } = [];
}

/// <summary>Visita concluída dentro de um ciclo.</summary>
public sealed record VisitaLoteRequest
{
    [Required, Range(1, int.MaxValue)] public int? ClienteId { get; init; }
    [Required, Range(0, int.MaxValue)] public int? PrioridadeOriginal { get; init; }
    [Required, Range(1, int.MaxValue)] public int? OrdemAtendimento { get; init; }
    [Required, StringLength(30)] public string Situacao { get; init; } = string.Empty;
    [StringLength(30)] public string? MotivoSemPedido { get; init; }
    [StringLength(500)] public string? Observacao { get; init; }
    [Required] public DateTimeOffset? DataHoraAtendimento { get; init; }
}

/// <summary>Pedido de um cliente, na ordem em que será conferido.</summary>
public sealed record PedidoLoteRequest
{
    [Required, Range(1, int.MaxValue)] public int? ClienteId { get; init; }
    [Required, Range(1, int.MaxValue)] public int? CondicaoPagamentoId { get; init; }
    [Required, StringLength(30)] public string FormaPagamentoPrevista { get; init; } = string.Empty;
    [StringLength(500)] public string? Observacao { get; init; }
    [Required, MinLength(1)] public IReadOnlyList<ItemLoteRequest> Itens { get; init; } = [];
}

/// <summary>Produto solicitado e preço informado pelo aplicativo.</summary>
public sealed record ItemLoteRequest
{
    [Required, Range(1, int.MaxValue)] public int? ProdutoId { get; init; }
    [Required, Range(1, int.MaxValue)] public int? QuantidadeSolicitada { get; init; }
    [Required, Range(typeof(decimal), "0", "99999999.99")] public decimal? PrecoUnitario { get; init; }
}
