namespace VanderPanificacoes.Api.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Perfil { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}

public class Cliente
{
    public int Id { get; set; }
    public int VendedorId { get; set; }
    public Usuario Vendedor { get; set; } = null!;
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Rua { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? UF { get; set; }
    public string? CEP { get; set; }
    public int PrioridadeVisita { get; set; }
    public bool Ativo { get; set; } = true;
}

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}

public class Produto
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string UnidadeVenda { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Estoque { get; set; }
    public bool Perecivel { get; set; }
    public DateOnly? DataValidade { get; set; }
    public bool Ativo { get; set; } = true;
}
