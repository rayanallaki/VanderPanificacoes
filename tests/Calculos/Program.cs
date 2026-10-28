using VanderPanificacoes.Api.Services;

var calculo = new CalculoVendaService();
var data = new DateOnly(2026, 10, 27);
var vista = calculo.Calcular([new(100m, 1)], 5m, [0], data);
Verificar(vista.TotalBruto == 100m && vista.Desconto == 5m && vista.TotalLiquido == 95m, "Desconto à vista");
Verificar(vista.Parcelas.Count == 1 && vista.Parcelas[0].Valor == 95m && vista.Parcelas[0].Vencimento == data, "Parcela à vista");
var prazo = calculo.Calcular([new(100m, 1)], 0m, [14, 28, 42], data);
Verificar(prazo.Parcelas.Select(p => p.Valor).SequenceEqual([33.33m, 33.33m, 33.34m]), "Divisão em três parcelas");
Verificar(prazo.Parcelas.Select(p => p.Vencimento).SequenceEqual([data.AddDays(14), data.AddDays(28), data.AddDays(42)]), "Vencimentos");
var centavos = calculo.Calcular([new(0.02m, 1)], 0m, [14, 28, 42], data);
Verificar(centavos.Parcelas.All(p => p.Valor >= 0) && centavos.Parcelas.Sum(p => p.Valor) == centavos.TotalLiquido, "Valor pequeno");
var parcial = calculo.Calcular([new(7.50m, 2), new(4m, 0)], 5m, [0], data);
Verificar(parcial.TotalBruto == 15m && parcial.Desconto == 0.75m && parcial.TotalLiquido == 14.25m, "Quantidade atendida");
var meioCentavo = calculo.Calcular([new(8.50m, 1)], 5m, [0], data);
Verificar(meioCentavo.Desconto == 0.43m && meioCentavo.TotalLiquido == 8.07m, "Arredondamento do desconto");
var itens = new SolicitacaoEstoque[]
{
    new(3, 1, 3, 5, 10, true, data), new(1, 1, 1, 4, 10, true, data), new(2, 1, 2, 6, 10, true, data)
};
var sugestao = DistribuicaoEstoque.Sugerir(itens, data);
Verificar(sugestao.Select(i => i.QuantidadeSugerida).SequenceEqual([4, 6, 0]), "Distribuição por ordem");
var validade = DistribuicaoEstoque.Sugerir([
    new(1, 1, 1, 2, 10, true, data.AddDays(-1)),
    new(2, 2, 1, 2, 10, false, null),
    new(3, 3, 1, 2, 10, true, data)
], data);
Verificar(validade.Select(i => i.QuantidadeSugerida).SequenceEqual([0, 0, 2]), "Validade e situação do produto");
Console.WriteLine("Todos os cenários de cálculo e distribuição passaram.");

static void Verificar(bool condicao, string nome)
{
    if (!condicao) throw new InvalidOperationException($"Falhou: {nome}");
}
