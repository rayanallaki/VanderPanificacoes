namespace VanderPanificacoes.Api.Services;

public sealed record ItemCalculoVenda(decimal PrecoUnitario, int QuantidadeAtendida);
public sealed record ParcelaCalculada(int Numero, DateOnly Vencimento, decimal Valor);
public sealed record VendaCalculada(decimal TotalBruto, decimal Desconto, decimal TotalLiquido,
    IReadOnlyList<ParcelaCalculada> Parcelas);

/// <summary>Calcula valores de venda e parcelas a partir das quantidades atendidas.</summary>
public sealed class CalculoVendaService
{
    private const decimal LimitePreco = 99999999.99m;
    private const decimal LimiteTotal = 9999999999.99m;

    public VendaCalculada Calcular(IReadOnlyList<ItemCalculoVenda> itens, decimal percentualDesconto,
        IReadOnlyList<int> prazosDias, DateOnly dataConfirmacao)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(prazosDias);
        if (itens.Count == 0 || percentualDesconto is < 0 or > 100 ||
            decimal.Round(percentualDesconto, 2) != percentualDesconto ||
            prazosDias.Count is < 1 or > 3 || prazosDias.Any(prazo => prazo < 0))
            throw new ArgumentException("Dados de cálculo inválidos.");

        decimal bruto = 0;
        foreach (var item in itens)
        {
            if (item.PrecoUnitario is < 0 or > LimitePreco ||
                decimal.Round(item.PrecoUnitario, 2) != item.PrecoUnitario || item.QuantidadeAtendida < 0)
                throw new ArgumentException("Preço ou quantidade inválidos.");

            var subtotal = Arredondar(item.PrecoUnitario * item.QuantidadeAtendida);
            if (subtotal > LimiteTotal || bruto > LimiteTotal - subtotal)
                throw new ArgumentOutOfRangeException(nameof(itens), "O total excede o limite permitido.");
            bruto += subtotal;
        }

        var desconto = Arredondar(bruto * percentualDesconto / 100m);
        var liquido = bruto - desconto;
        var valorBase = Arredondar(liquido / prazosDias.Count);
        var parcelas = new List<ParcelaCalculada>(prazosDias.Count);
        decimal acumulado = 0;
        for (var i = 0; i < prazosDias.Count; i++)
        {
            var valor = i == prazosDias.Count - 1 ? liquido - acumulado : valorBase;
            parcelas.Add(new ParcelaCalculada(i + 1, dataConfirmacao.AddDays(prazosDias[i]), valor));
            acumulado += valor;
        }

        return new VendaCalculada(bruto, desconto, liquido, parcelas);
    }

    private static decimal Arredondar(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}
