using SQLite;

namespace VanderPanificacoes.Mobile;

/// <summary>Guarda sincronização e rascunho separados por servidor e vendedor.</summary>
public sealed class ArmazenamentoLocal : IDisposable
{
    private readonly SQLiteConnection banco;

    public ArmazenamentoLocal(string caminho)
    {
#if IOS || MACCATALYST
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
#else
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
#endif
        banco = new SQLiteConnection(caminho);
        banco.CreateTable<DadosVendedor>();
    }

    public DadosVendedor? Ler(string chave) => banco.Find<DadosVendedor>(chave);

    public void SalvarSincronizacao(string chave, string dados)
    {
        banco.RunInTransaction(() =>
        {
            var registro = Ler(chave) ?? new DadosVendedor { Chave = chave };
            registro.Sincronizacao = dados;
            registro.AtualizadoEm = DateTimeOffset.UtcNow.ToString("O");
            banco.InsertOrReplace(registro);
        });
    }

    public void SalvarRascunho(string chave, string? rascunho)
    {
        banco.RunInTransaction(() =>
        {
            var registro = Ler(chave) ?? new DadosVendedor { Chave = chave };
            registro.Rascunho = rascunho;
            banco.InsertOrReplace(registro);
        });
    }

    public void Dispose() => banco.Dispose();
}

public sealed class DadosVendedor
{
    [PrimaryKey] public string Chave { get; set; } = "";
    public string? Sincronizacao { get; set; }
    public string? AtualizadoEm { get; set; }
    public string? Rascunho { get; set; }
}
