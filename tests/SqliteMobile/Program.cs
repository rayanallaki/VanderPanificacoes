using VanderPanificacoes.Mobile;

var caminho = Path.Combine(Path.GetTempPath(), $"vander-sqlite-teste-{Guid.NewGuid():N}.db3");
try
{
    using (var banco = new ArmazenamentoLocal(caminho))
    {
        banco.SalvarSincronizacao("servidor|1", "dados-1");
        banco.SalvarRascunho("servidor|1", "pedido-1");
        banco.SalvarSincronizacao("servidor|2", "dados-2");
        banco.SalvarRascunho("servidor|2", "pedido-2");
        banco.SalvarSincronizacao("servidor|1", "dados-atualizados");
    }
    using (var banco = new ArmazenamentoLocal(caminho))
    {
        var primeiro = banco.Ler("servidor|1") ?? throw new Exception("Vendedor 1 não encontrado");
        var segundo = banco.Ler("servidor|2") ?? throw new Exception("Vendedor 2 não encontrado");
        if (primeiro.Sincronizacao != "dados-atualizados" || primeiro.Rascunho != "pedido-1" ||
            segundo.Sincronizacao != "dados-2" || segundo.Rascunho != "pedido-2")
            throw new Exception("A persistência ou o isolamento dos vendedores falhou.");
        banco.SalvarRascunho("servidor|1", null);
        if (banco.Ler("servidor|1")?.Rascunho is not null || banco.Ler("servidor|2")?.Rascunho != "pedido-2")
            throw new Exception("A limpeza do rascunho afetou outro vendedor.");
    }
    Console.WriteLine("SQLite: persistência, atualização e isolamento passaram.");
}
finally
{
    if (File.Exists(caminho)) File.Delete(caminho);
}
