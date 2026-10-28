using System.Globalization;
using System.Text.Json.Nodes;

namespace VanderPanificacoes.Web.Models;

public static class Cadastros
{
    public static readonly Dictionary<string, string> Titulos = new() { ["categorias"] = "Categorias", ["produtos"] = "Produtos", ["clientes"] = "Clientes", ["vendedores"] = "Vendedores" };
    public static List<CampoDef> Campos(string tipo, bool editar) => tipo switch
    {
        "categorias" => [new("nome", "Nome da categoria", Obrigatorio: true, Maximo: 100)],
        "vendedores" => [new("nome", "Nome", Obrigatorio: true, Maximo: 100), new("email", "E-mail", "email", true, 150), ..(editar ? Array.Empty<CampoDef>() : [new CampoDef("senha", "Senha inicial", "password", true, 128)])],
        "produtos" => [new("nome", "Nome do produto", Obrigatorio: true, Maximo: 100), new("descricao", "Descrição", Maximo: 500), new("categoriaId", "Categoria", "select", true), new("unidadeVenda", "Unidade de venda", "select", true), new("preco", "Preço (R$)", "number", true, Min: "0", Step: "0.01"), new("perecivel", "Produto perecível", "checkbox"), new("dataValidade", "Validade", "date")],
        "clientes" => [new("nome", "Nome do estabelecimento", Obrigatorio: true, Maximo: 100), new("documento", "Documento", Obrigatorio: true, Maximo: 30), new("vendedorId", "Vendedor responsável", "select", true), new("prioridadeVisita", "Prioridade de visita", "number", true, Min: "0"), new("telefone", "Telefone", "tel", Maximo: 20), new("email", "E-mail", "email", Maximo: 150), new("rua", "Rua", Maximo: 100), new("numero", "Número", Maximo: 20), new("complemento", "Complemento", Maximo: 100), new("bairro", "Bairro", Maximo: 100), new("cidade", "Cidade", Maximo: 100), new("uf", "UF", Maximo: 2), new("cep", "CEP", Maximo: 9)],
        _ => throw new ArgumentException("Cadastro desconhecido.")
    };

    public static JsonObject Corpo(CadastroForm form)
    {
        var corpo = new JsonObject();
        foreach (var campo in Campos(form.Tipo, form.Id.HasValue))
        {
            var valor = form.Dados.GetValueOrDefault(campo.Nome)?.Trim();
            if (campo.Tipo == "checkbox") corpo[campo.Nome] = valor == "true";
            else if (campo.Tipo == "number" || campo.Nome.EndsWith("Id"))
            {
                if (!decimal.TryParse(valor, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var numero)) throw new ArgumentException($"Informe um valor válido para {campo.Rotulo}.");
                if (campo.Nome != "preco" && (numero != decimal.Truncate(numero) || numero > int.MaxValue || numero < 0)) throw new ArgumentException($"Informe um número inteiro válido para {campo.Rotulo}.");
                corpo[campo.Nome] = campo.Nome == "preco" ? JsonValue.Create(numero) : JsonValue.Create((int)numero);
            }
            else corpo[campo.Nome] = string.IsNullOrEmpty(valor) ? null : valor;
        }
        return corpo;
    }
}
