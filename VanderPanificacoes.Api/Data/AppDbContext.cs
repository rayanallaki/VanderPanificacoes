using Microsoft.EntityFrameworkCore;
using VanderPanificacoes.Api.Models;

namespace VanderPanificacoes.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<CondicaoPagamento> CondicoesPagamento => Set<CondicaoPagamento>();
    public DbSet<LotePedidos> LotesPedidos => Set<LotePedidos>();
    public DbSet<Visita> Visitas => Set<Visita>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<Parcela> Parcelas => Set<Parcela>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SenhaHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Perfil).HasMaxLength(30).IsRequired();
            entity.ToTable(t => t.HasCheckConstraint("CK_Usuarios_Perfil", "`Perfil` IN ('Administrador', 'Vendedor')"));
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasIndex(x => x.Documento).IsUnique();
            entity.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Documento).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.Telefone).HasMaxLength(20);
            entity.Property(x => x.Rua).HasMaxLength(100);
            entity.Property(x => x.Numero).HasMaxLength(20);
            entity.Property(x => x.Complemento).HasMaxLength(100);
            entity.Property(x => x.Bairro).HasMaxLength(100);
            entity.Property(x => x.Cidade).HasMaxLength(100);
            entity.Property(x => x.UF).HasMaxLength(2);
            entity.Property(x => x.CEP).HasMaxLength(9);
        });

        modelBuilder.Entity<Venda>(entity =>
        {
            entity.HasIndex(x => x.PedidoId).IsUnique();
            entity.HasOne(x => x.Pedido).WithMany().HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.DataConfirmacao).HasColumnType("datetime(6)");
            entity.Property(x => x.TotalBruto).HasPrecision(12, 2);
            entity.Property(x => x.Desconto).HasPrecision(12, 2);
            entity.Property(x => x.TotalLiquido).HasPrecision(12, 2);
            entity.ToTable(t => t.HasCheckConstraint("CK_Vendas_Valores", "`TotalBruto` >= 0 AND `Desconto` >= 0 AND `TotalLiquido` >= 0 AND `Desconto` <= `TotalBruto` AND `TotalLiquido` = `TotalBruto` - `Desconto`"));
        });

        modelBuilder.Entity<Parcela>(entity =>
        {
            entity.HasIndex(x => new { x.VendaId, x.Numero }).IsUnique();
            entity.HasIndex(x => x.IdentificadorRecebimento).IsUnique();
            entity.HasOne(x => x.Venda).WithMany().HasForeignKey(x => x.VendaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UsuarioRecebimento).WithMany().HasForeignKey(x => x.UsuarioRecebimentoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.ValorBase).HasPrecision(12, 2);
            entity.Property(x => x.ValorRecebido).HasPrecision(12, 2);
            entity.Property(x => x.Situacao).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FormaPagamentoPrevista).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FormaPagamentoRecebida).HasMaxLength(30);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Parcelas_Valores", "`Numero` > 0 AND `ValorBase` >= 0 AND (`ValorRecebido` IS NULL OR `ValorRecebido` >= 0)");
                t.HasCheckConstraint("CK_Parcelas_Situacao", "`Situacao` IN ('Aberta', 'Paga')");
                t.HasCheckConstraint("CK_Parcelas_FormaPrevista", "`FormaPagamentoPrevista` IN ('Pix', 'Dinheiro', 'Cartao', 'Boleto')");
                t.HasCheckConstraint("CK_Parcelas_FormaRecebida", "`FormaPagamentoRecebida` IS NULL OR `FormaPagamentoRecebida` IN ('Pix', 'Dinheiro', 'Cartao', 'Boleto')");
                t.HasCheckConstraint("CK_Parcelas_Recebimento", "(`Situacao` = 'Aberta' AND `DataRecebimento` IS NULL AND `ValorRecebido` IS NULL AND `UsuarioRecebimentoId` IS NULL AND `FormaPagamentoRecebida` IS NULL) OR (`Situacao` = 'Paga' AND `DataRecebimento` IS NOT NULL AND `ValorRecebido` IS NOT NULL AND `ValorRecebido` = `ValorBase` AND `UsuarioRecebimentoId` IS NOT NULL AND `FormaPagamentoRecebida` IS NOT NULL)");
            });
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasIndex(x => new { x.LotePedidosId, x.ClienteId }).IsUnique();
            entity.HasIndex(x => new { x.LotePedidosId, x.SequenciaPedido }).IsUnique();
            entity.HasOne(x => x.LotePedidos).WithMany().HasForeignKey(x => x.LotePedidosId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CondicaoPagamento).WithMany().HasForeignKey(x => x.CondicaoPagamentoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.NomeCondicaoSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PrazosDiasSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PercentualDescontoSnapshot).HasPrecision(5, 2);
            entity.Property(x => x.FormaPagamentoPrevista).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Observacao).HasMaxLength(500);
            entity.Property(x => x.Situacao).HasMaxLength(30).IsRequired();
            entity.Property(x => x.TotalPrevisto).HasPrecision(12, 2);
            entity.Property(x => x.TotalAtendido).HasPrecision(12, 2);
            entity.Property(x => x.Desconto).HasPrecision(12, 2);
            entity.Property(x => x.TotalFinal).HasPrecision(12, 2);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Pedidos_Sequencia", "`SequenciaPedido` > 0");
                t.HasCheckConstraint("CK_Pedidos_Valores", "`TotalPrevisto` >= 0 AND `TotalAtendido` >= 0 AND `Desconto` >= 0 AND `TotalFinal` >= 0 AND `PercentualDescontoSnapshot` >= 0 AND `PercentualDescontoSnapshot` <= 100");
                t.HasCheckConstraint("CK_Pedidos_Situacao", "`Situacao` IN ('Recebido', 'AtendidoTotal', 'AtendidoParcial', 'NaoAtendido', 'Cancelado')");
                t.HasCheckConstraint("CK_Pedidos_FormaPagamento", "`FormaPagamentoPrevista` IN ('Pix', 'Dinheiro', 'Cartao', 'Boleto')");
            });
        });

        modelBuilder.Entity<ItemPedido>(entity =>
        {
            entity.HasIndex(x => new { x.PedidoId, x.ProdutoId }).IsUnique();
            entity.HasOne(x => x.Pedido).WithMany().HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.NomeProdutoSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UnidadeVendaSnapshot).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PrecoUnitario).HasPrecision(10, 2);
            entity.Property(x => x.SubtotalAtendido).HasPrecision(12, 2);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Itens_Quantidades", "`QuantidadeSolicitada` > 0 AND `QuantidadeAtendida` >= 0 AND `QuantidadeAtendida` <= `QuantidadeSolicitada`");
                t.HasCheckConstraint("CK_Itens_Valores", "`PrecoUnitario` >= 0 AND `SubtotalAtendido` >= 0");
            });
        });

        modelBuilder.Entity<Visita>(entity =>
        {
            entity.HasIndex(x => new { x.LotePedidosId, x.ClienteId }).IsUnique();
            entity.HasOne(x => x.LotePedidos).WithMany().HasForeignKey(x => x.LotePedidosId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Situacao).HasMaxLength(30).IsRequired();
            entity.Property(x => x.MotivoSemPedido).HasMaxLength(30);
            entity.Property(x => x.Observacao).HasMaxLength(500);
            entity.Property(x => x.DataHoraAtendimento).HasColumnType("datetime(6)");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Visitas_Situacao", "`Situacao` IN ('PedidoRegistrado', 'SemPedido')");
                t.HasCheckConstraint("CK_Visitas_Ordem", "`OrdemAtendimento` > 0");
            });
        });

        modelBuilder.Entity<LotePedidos>(entity =>
        {
            entity.HasIndex(x => x.IdentificadorEnvio).IsUnique();
            entity.HasIndex(x => x.IdentificadorCancelamento).IsUnique();
            entity.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UsuarioConfirmacao).WithMany().HasForeignKey(x => x.UsuarioConfirmacaoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UsuarioCancelamento).WithMany().HasForeignKey(x => x.UsuarioCancelamentoId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.HashConteudo).HasMaxLength(64).IsRequired();
            entity.Property(x => x.HashConfirmacao).HasMaxLength(64);
            entity.Property(x => x.Situacao).HasMaxLength(30).IsRequired();
            entity.Property(x => x.MotivoCancelamento).HasMaxLength(500);
            entity.Property(x => x.DataCriacaoLocal).HasColumnType("datetime(6)");
            entity.Property(x => x.DataRecebimento).HasColumnType("datetime(6)");
            entity.Property(x => x.DataConfirmacao).HasColumnType("datetime(6)");
            entity.ToTable(t => t.HasCheckConstraint("CK_Lotes_Situacao", "`Situacao` IN ('Recebido', 'Confirmado', 'Cancelado')"));
        });

        modelBuilder.Entity<CondicaoPagamento>(entity =>
        {
            entity.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PrazosDias).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PercentualDesconto).HasPrecision(5, 2);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Condicoes_Parcelas", "`QuantidadeParcelas` > 0");
                t.HasCheckConstraint("CK_Condicoes_Desconto", "`PercentualDesconto` >= 0 AND `PercentualDesconto` <= 100");
            });
            entity.HasData(
                new CondicaoPagamento { Id = 1, Nome = "À vista", QuantidadeParcelas = 1, PrazosDias = "0", PercentualDesconto = 5m },
                new CondicaoPagamento { Id = 2, Nome = "7/14 dias", QuantidadeParcelas = 2, PrazosDias = "7,14", PercentualDesconto = 0m },
                new CondicaoPagamento { Id = 3, Nome = "14/28/42 dias", QuantidadeParcelas = 3, PrazosDias = "14,28,42", PercentualDesconto = 0m });
        });

        modelBuilder.Entity<Categoria>().Property(x => x.Nome).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<MovimentacaoEstoque>(entity =>
        {
            entity.HasIndex(x => x.IdentificadorOperacao).IsUnique();
            entity.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Venda).WithMany().HasForeignKey(x => x.VendaId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Tipo).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Motivo).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DataHora).HasColumnType("datetime(6)");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Movimentacoes_Quantidade", "`Quantidade` > 0");
                t.HasCheckConstraint("CK_Movimentacoes_Tipo", "`Tipo` IN ('Entrada', 'AjusteEntrada', 'AjusteSaida', 'Venda')");
            });
        });
        modelBuilder.Entity<Produto>(entity =>
        {
            entity.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Descricao).HasMaxLength(500);
            entity.Property(x => x.UnidadeVenda).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Preco).HasPrecision(10, 2);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Produtos_Preco", "`Preco` >= 0");
                t.HasCheckConstraint("CK_Produtos_Estoque", "`Estoque` >= 0");
                t.HasCheckConstraint("CK_Produtos_Validade", "NOT `Perecivel` OR `DataValidade` IS NOT NULL");
                t.HasCheckConstraint("CK_Produtos_Unidade", "`UnidadeVenda` IN ('Unidade', 'Pacote', 'Caixa')");
            });
        });
    }
}
