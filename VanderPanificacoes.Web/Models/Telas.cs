using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Nodes;

namespace VanderPanificacoes.Web.Models;

public sealed class LoginForm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Senha { get; set; } = "";
}
public sealed record PainelVm(List<JsonObject> Lotes, List<JsonObject> Parcelas, List<JsonObject> Produtos);
public sealed record ListaVm(string Tipo, string Titulo, List<JsonObject> Registros, string? Busca = null);
public sealed record Opcao(string Valor, string Texto);
public sealed record CampoDef(string Nome, string Rotulo, string Tipo = "text", bool Obrigatorio = false, int? Maximo = null, string? Min = null, string? Step = null);
public sealed class CadastroForm
{
    public string Tipo { get; set; } = "";
    public int? Id { get; set; }
    public Dictionary<string, string?> Dados { get; set; } = [];
    public List<CampoDef> Campos { get; set; } = [];
    public Dictionary<string, List<Opcao>> Opcoes { get; set; } = [];
}
public sealed class ItemForm
{
    [Range(1, int.MaxValue)] public int ItemPedidoId { get; set; }
    [Range(0, int.MaxValue)] public int QuantidadeAtendida { get; set; }
}
public sealed class ConferenciaForm { public List<ItemForm> Itens { get; set; } = []; }
public sealed record LoteVm(JsonObject Detalhes, List<ItemForm> Itens, JsonObject? Previa = null);
public sealed class CancelamentoForm
{
    public int Id { get; set; }
    public Guid Identificador { get; set; } = Guid.NewGuid();
    [Required, StringLength(500)] public string Motivo { get; set; } = "";
}
public sealed class EstoqueForm
{
    public Guid Identificador { get; set; } = Guid.NewGuid();
    [Range(1, int.MaxValue)] public int ProdutoId { get; set; }
    [Required] public string Tipo { get; set; } = "entradas";
    [Range(1, int.MaxValue)] public int Quantidade { get; set; }
    [Required, StringLength(500)] public string Motivo { get; set; } = "";
    public List<JsonObject> Produtos { get; set; } = [];
}
public sealed class RecebimentoForm
{
    public int Id { get; set; }
    public Guid Identificador { get; set; } = Guid.NewGuid();
    [Required] public DateOnly? DataRecebimento { get; set; } = Ui.Hoje();
    [Required] public string FormaPagamento { get; set; } = "Pix";
    public JsonObject Parcela { get; set; } = new();
}
public static class Ui
{
    public static string Texto(JsonObject item, string chave) => item[chave]?.ToString() ?? "—";
    public static int Id(JsonObject item, string chave = "id") => item[chave]?.GetValue<int>() ?? 0;
    public static bool Ativo(JsonObject item) => item["ativo"]?.GetValue<bool>() ?? false;
    public static decimal Valor(JsonObject item, string chave) => item[chave]?.GetValue<decimal>() ?? 0;
    public static string Moeda(decimal valor) => valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
    public static DateOnly Hoje() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
    public static string Data(JsonNode? valor)
    {
        var texto = valor?.ToString();
        if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)) return data.ToString("dd/MM/yyyy");
        return DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var evento)
            ? TimeZoneInfo.ConvertTimeBySystemTimeZoneId(evento, "America/Sao_Paulo").ToString("dd/MM/yyyy HH:mm") : "—";
    }
}
