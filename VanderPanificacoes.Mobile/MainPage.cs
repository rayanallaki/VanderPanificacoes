using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Layouts;

namespace VanderPanificacoes.Mobile;

public sealed partial class MainPage : ContentPage
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private readonly Entry endereco = new() { Placeholder = "Endereço da API", Text = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5296" : "http://localhost:5296", Keyboard = Keyboard.Url };
    private readonly Entry email = new() { Placeholder = "E-mail", Keyboard = Keyboard.Email };
    private readonly Entry senha = new() { Placeholder = "Senha", IsPassword = true };
    private readonly Label aviso = new() { TextColor = Color.FromArgb("#9B4338"), FontSize = 13, Margin = new Thickness(4, 0) };
    private readonly ActivityIndicator carregando = new() { IsVisible = false, Color = Color.FromArgb("#246348") };
    private readonly VerticalStackLayout conteudo = new() { Spacing = 14 };
    private readonly Picker clientes = new() { Title = "Escolha um cliente", ItemDisplayBinding = new Binding(nameof(Cliente.Nome)) };
    private readonly Picker produtos = new() { Title = "Escolha um produto", ItemDisplayBinding = new Binding(nameof(Produto.DescricaoExibicao)) };
    private readonly Picker condicoes = new() { Title = "Condição de pagamento", ItemDisplayBinding = new Binding(nameof(Condicao.Nome)) };
    private readonly Picker pagamento = new() { Title = "Forma de pagamento", ItemsSource = new[] { "Pix", "Dinheiro", "Cartao", "Boleto" }, SelectedIndex = 0 };
    private readonly Entry quantidade = new() { Placeholder = "Quantidade", Keyboard = Keyboard.Numeric, Text = "1" };
    private readonly ArmazenamentoLocal armazenamento = new(Path.Combine(FileSystem.AppDataDirectory, "vander-local.db3"));
    private readonly VerticalStackLayout conteudoPagina = new() { Spacing = 12 };
    private readonly List<ItemCarrinho> carrinho = [];
    private List<Cliente> carteira = [];
    private List<Produto> catalogo = [];
    private List<Condicao> planos = [];
    private string nomeVendedor = "";
    private int vendedorId;
    private bool ocupado;
    private bool restaurando;
    private bool dadosLocais;
    private string baseApi = "";
    private readonly string arquivo = Path.Combine(FileSystem.AppDataDirectory, "envio-pendente.json");

    public MainPage()
    {
        Title = "Vander Panificações";
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#F5F3EE");
        clientes.SelectedIndexChanged += (_, _) => SalvarRascunho();
        condicoes.SelectedIndexChanged += (_, _) => SalvarRascunho();
        pagamento.SelectedIndexChanged += (_, _) => SalvarRascunho();
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = new Thickness(20, 28), Spacing = 18, MaximumWidthRequest = 760, HorizontalOptions = LayoutOptions.Center, Children = { conteudo, carregando, aviso } } };
        MostrarLogin();
    }

    private void MostrarLogin()
    {
        http.DefaultRequestHeaders.Authorization = null; senha.Text = ""; conteudo.Children.Clear(); aviso.Text = "";
        conteudo.Children.Add(CabecalhoMarca("Sua rota. Seus clientes.", "Tudo pronto para uma boa venda."));
        var formulario = new VerticalStackLayout { Spacing = 16 };
        formulario.Children.Add(Titulo("Bem-vindo de volta"));
        formulario.Children.Add(Texto("Entre com sua conta de vendedor para organizar os pedidos do dia."));
        formulario.Children.Add(Campo("E-MAIL", email)); formulario.Children.Add(Campo("SENHA", senha));
        var entrar = new Button { Text = "Entrar na minha rota" };
        entrar.Clicked += async (_, _) => await Executar(entrar, LoginAsync);
        formulario.Children.Add(entrar);
        formulario.Children.Add(Campo("CONEXÃO COM A LOJA", endereco));
        conteudo.Children.Add(Cartao(formulario));
        conteudo.Children.Add(Texto("Vander Panificações · Portal do vendedor", 12));
    }

    private async Task LoginAsync()
    {
        if (!Uri.TryCreate(endereco.Text?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new InvalidOperationException("Informe um endereço válido para a API.");
        baseApi = uri.ToString().TrimEnd('/');
        using var resposta = await http.PostAsJsonAsync(baseApi + "/api/autenticacao/login", new { email = email.Text?.Trim(), senha = senha.Text });
        await Verificar(resposta);
        var login = await resposta.Content.ReadFromJsonAsync<Login>(json) ?? throw new InvalidOperationException("Resposta de login inválida.");
        if (login.Usuario.Perfil != "Vendedor") throw new InvalidOperationException("Entre com uma conta de vendedor.");
        vendedorId = login.Usuario.Id; nomeVendedor = login.Usuario.Nome; http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        senha.Text = "";
        await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        var chave = ChaveLocal();
        var anterior = armazenamento.Ler(chave);
        Sincronizacao dados;
        try
        {
            var carteiraNova = await Obter<List<Cliente>>("/api/carteira/clientes");
            var catalogoNovo = await Obter<List<Produto>>("/api/produtos");
            var planosNovos = await Obter<List<Condicao>>("/api/condicoes-pagamento");
            dados = new Sincronizacao(carteiraNova, catalogoNovo, planosNovos);
            armazenamento.SalvarSincronizacao(chave, JsonSerializer.Serialize(dados, json));
            dadosLocais = false;
        }
        catch (Exception erro) when ((erro is HttpRequestException or TaskCanceledException) && anterior?.Sincronizacao is not null)
        {
            dados = JsonSerializer.Deserialize<Sincronizacao>(anterior.Sincronizacao, json) ?? throw new InvalidOperationException("A cópia local está inválida.");
            dadosLocais = true;
        }
        carteira = dados.Carteira; catalogo = dados.Catalogo; planos = dados.Planos;
        restaurando = true;
        try
        {
            clientes.ItemsSource = carteira; produtos.ItemsSource = catalogo; condicoes.ItemsSource = planos;
            carrinho.Clear();
            var rascunhoJson = armazenamento.Ler(chave)?.Rascunho;
            var rascunho = rascunhoJson is null ? null : JsonSerializer.Deserialize<Rascunho>(rascunhoJson, json);
            if (rascunho is not null)
            {
                clientes.SelectedItem = carteira.Find(c => c.Id == rascunho.ClienteId);
                condicoes.SelectedItem = planos.Find(c => c.Id == rascunho.CondicaoId);
                pagamento.SelectedItem = rascunho.FormaPagamento;
                foreach (var item in rascunho.Itens) carrinho.Add(new ItemCarrinho(item.Produto, item.Quantidade));
            }
        }
        finally { restaurando = false; }
        conteudo.Children.Clear(); aviso.Text = "";
        conteudo.Children.Add(CabecalhoMarca($"Olá, {nomeVendedor.Split(' ')[0]}", "Vamos fazer bons negócios hoje?"));
        var resumo = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) } };
        resumo.Add(Cartao(new VerticalStackLayout { Spacing = 2, Children = { Titulo(carteira.Count.ToString()), Texto("Clientes na carteira", 12) } }), 0);
        resumo.Add(Cartao(new VerticalStackLayout { Spacing = 2, Children = { Titulo(catalogo.Count.ToString()), Texto("Produtos no catálogo", 12) } }), 1);
        conteudo.Children.Add(resumo);
        conteudo.Children.Add(Selo(dadosLocais ? "Modo local · dados salvos no aparelho" : "Dados sincronizados com a loja", dadosLocais));
        var menu = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row };
        void Aba(string texto, Action acao) { var b = BotaoDiscreto(texto); b.Margin = new Thickness(0, 0, 6, 6); b.Clicked += (_, _) => { if (!ocupado) { aviso.Text = ""; acao(); } }; menu.Children.Add(b); }
        Aba("Pedido", MostrarPedido); Aba("Clientes", MostrarClientes); Aba("Produtos", MostrarProdutos);
        var lotesBotao = BotaoDiscreto("Meus lotes"); lotesBotao.Margin = new Thickness(0, 0, 6, 6);
        lotesBotao.Clicked += async (_, _) => await Executar(lotesBotao, MostrarLotesAsync); menu.Children.Add(lotesBotao);
        Aba("Sair", () => { carrinho.Clear(); MostrarLogin(); });
        conteudo.Children.Add(menu); conteudo.Children.Add(conteudoPagina);
        MostrarPedido();
        if (File.Exists(arquivo)) aviso.Text = "Há um envio pendente neste dispositivo. Entre com o mesmo vendedor e reenvie antes de criar outro pedido.";
    }

    private static Label Titulo(string texto) => new() { Text = texto, FontSize = 24, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#173D2E") };
    private void MostrarPedido()
    {
        conteudoPagina.Children.Clear(); conteudoPagina.Children.Add(Titulo("Novo pedido"));
        conteudoPagina.Children.Add(Texto("Monte o pedido em três passos. A loja confirmará as quantidades disponíveis."));
        conteudoPagina.Children.Add(Cartao(new VerticalStackLayout { Spacing = 12, Children = { Etapa("01", "Quem vamos atender?"), Campo("CLIENTE", clientes) } }));
        var itensCard = new VerticalStackLayout { Spacing = 12 };
        itensCard.Children.Add(Etapa("02", "Escolha os produtos"));
        itensCard.Children.Add(Campo("PRODUTO", produtos)); itensCard.Children.Add(Campo("QUANTIDADE", quantidade));
        var adicionar = BotaoDiscreto("+ Adicionar ao pedido");
        adicionar.Clicked += (_, _) => {
            if (ocupado) return;
            if (produtos.SelectedItem is not Produto produto || !int.TryParse(quantidade.Text, out var qtd) || qtd <= 0) { aviso.Text = "Escolha um produto e uma quantidade inteira positiva."; return; }
            var item = carrinho.Find(x => x.Produto.Id == produto.Id);
            if (item is null) carrinho.Add(new ItemCarrinho(produto, qtd));
            else if ((long)item.Quantidade + qtd <= int.MaxValue) item.Quantidade += qtd;
            else { aviso.Text = "Quantidade muito grande."; return; }
            SalvarRascunho();
            aviso.Text = ""; MostrarPedido();
        };
        itensCard.Children.Add(adicionar);
        if (carrinho.Count == 0) itensCard.Children.Add(Texto("Seu pedido está vazio. Escolha um produto para começar."));
        foreach (var item in carrinho.ToArray())
        {
            var linha = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            linha.Add(new VerticalStackLayout { Spacing = 3, Children = { new Label { Text = item.Produto.Nome, FontFamily = "OpenSansSemibold", LineBreakMode = LineBreakMode.WordWrap }, Texto($"{item.Quantidade} × {Dinheiro(item.Produto.Preco)} · {Dinheiro(item.Quantidade * item.Produto.Preco)}", 12) } }, 0);
            var remover = BotaoDiscreto("Remover"); remover.TextColor = Color.FromArgb("#9B4338"); remover.FontSize = 12; remover.Clicked += (_, _) => { if (!ocupado) { carrinho.Remove(item); SalvarRascunho(); MostrarPedido(); } }; linha.Add(remover, 1); itensCard.Children.Add(linha);
        }
        conteudoPagina.Children.Add(Cartao(itensCard));
        var fechamento = new VerticalStackLayout { Spacing = 14, Children = { Etapa("03", "Pagamento e envio"), Campo("CONDIÇÃO", condicoes), Campo("FORMA DE PAGAMENTO", pagamento), Texto("SUBTOTAL PREVISTO", 11), Titulo(Dinheiro(carrinho.Sum(i => i.Quantidade * i.Produto.Preco))), Texto("Descontos e valores finais serão calculados na conferência da loja.", 12) } };
        var enviar = new Button { Text = "Enviar pedido para a loja", IsEnabled = carrinho.Count > 0 }; enviar.Clicked += async (_, _) => await Executar(enviar, EnviarNovoAsync); fechamento.Children.Add(enviar);
        conteudoPagina.Children.Add(Cartao(fechamento));
        var reenviar = BotaoDiscreto("Retomar envio pendente"); reenviar.Clicked += async (_, _) => await Executar(reenviar, ReenviarAsync); conteudoPagina.Children.Add(reenviar);
        var atualizar = BotaoDiscreto("Atualizar carteira e catálogo"); atualizar.Clicked += async (_, _) => await Executar(atualizar, CarregarAsync); conteudoPagina.Children.Add(atualizar);
    }

    private void MostrarClientes()
    {
        conteudoPagina.Children.Clear(); conteudoPagina.Children.Add(Titulo("Minha carteira"));
        conteudoPagina.Children.Add(Texto("Seus clientes, organizados pela prioridade de atendimento."));
        var pesquisa = new Entry { Placeholder = "Nome ou documento do cliente" }; conteudoPagina.Children.Add(Campo("PESQUISAR NA CARTEIRA", pesquisa));
        var lista = new VerticalStackLayout { Spacing = 10 }; conteudoPagina.Children.Add(lista);
        void Atualizar() { lista.Children.Clear(); var termo = pesquisa.Text?.Trim() ?? ""; var encontrados = carteira.Where(x => x.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase) || (x.Documento?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            if (encontrados.Count == 0) lista.Children.Add(Cartao(Texto("Nenhum cliente encontrado. Tente outro nome ou documento.")));
            foreach (var cliente in encontrados)
            {
                var escolher = BotaoDiscreto("Iniciar pedido");
                escolher.Clicked += (_, _) => { if (!ocupado) { clientes.SelectedItem = cliente; MostrarPedido(); } };
                lista.Children.Add(Cartao(new VerticalStackLayout { Spacing = 10, Children = {
                    Selo($"Prioridade {cliente.PrioridadeVisita}"), new Label { Text = cliente.Nome, FontSize = 18, FontFamily = "OpenSansSemibold" }, Texto($"Documento: {cliente.Documento}", 12), escolher
                } }));
            } }
        pesquisa.TextChanged += (_, _) => Atualizar(); Atualizar();
    }

    private void MostrarProdutos()
    {
        conteudoPagina.Children.Clear(); conteudoPagina.Children.Add(Titulo("Catálogo"));
        conteudoPagina.Children.Add(Texto("Consulte o catálogo e escolha o próximo item do pedido."));
        var pesquisa = new Entry { Placeholder = "Nome do produto" }; conteudoPagina.Children.Add(Campo("PESQUISAR NO CATÁLOGO", pesquisa));
        var lista = new VerticalStackLayout { Spacing = 10 }; conteudoPagina.Children.Add(lista);
        void Atualizar() { lista.Children.Clear(); var termo = pesquisa.Text?.Trim() ?? ""; var encontrados = catalogo.Where(x => x.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)).ToList();
            if (encontrados.Count == 0) lista.Children.Add(Cartao(Texto("Nenhum produto encontrado. Tente outro nome.")));
            foreach (var produto in encontrados)
            {
                var escolher = BotaoDiscreto("Escolher para o pedido");
                escolher.Clicked += (_, _) => { if (!ocupado) { produtos.SelectedItem = produto; MostrarPedido(); } };
                lista.Children.Add(Cartao(new VerticalStackLayout { Spacing = 10, Children = {
                    Texto(produto.Categoria.ToUpperInvariant(), 11), new Label { Text = produto.Nome, FontSize = 18, FontFamily = "OpenSansSemibold" },
                    Titulo(Dinheiro(produto.Preco)), Texto($"Por {produto.UnidadeVenda.ToLowerInvariant()}", 12), escolher
                } }));
            } }
        pesquisa.TextChanged += (_, _) => Atualizar(); Atualizar();
    }

    private async Task MostrarLotesAsync()
    {
        var lotes = await Obter<List<Lote>>("/api/lotes/meus");
        conteudoPagina.Children.Clear(); conteudoPagina.Children.Add(Titulo("Meus lotes"));
        conteudoPagina.Children.Add(Texto("Acompanhe o recebimento e a conferência dos seus pedidos."));
        if (lotes.Count == 0) conteudoPagina.Children.Add(Cartao(Texto("Nenhum lote enviado ainda. Seu primeiro pedido aparecerá aqui.")));
        foreach (var lote in lotes)
        {
            var botao = BotaoDiscreto("Ver detalhes do lote");
            botao.Clicked += async (_, _) => await Executar(botao, () => MostrarDetalhesAsync(lote.Id));
            conteudoPagina.Children.Add(Cartao(new VerticalStackLayout { Spacing = 12, Children = {
                Selo(lote.Situacao, lote.Situacao == "Cancelado"), Titulo($"Lote #{lote.Id}"), Texto($"Enviado em {lote.DataRecebimento.LocalDateTime:dd/MM/yyyy 'às' HH:mm}", 12), botao
            } }));
        }
    }

    private async Task MostrarDetalhesAsync(int id)
    {
        var detalhes = await Obter<Detalhes>("/api/lotes/" + id);
        conteudoPagina.Children.Clear(); conteudoPagina.Children.Add(Titulo($"Lote #{id}"));
        conteudoPagina.Children.Add(Selo(detalhes.Lote.Situacao, detalhes.Lote.Situacao == "Cancelado"));
        foreach (var pedido in detalhes.Pedidos)
        {
            var cartao = new VerticalStackLayout { Spacing = 12, Children = { new Label { Text = pedido.Cliente, FontSize = 20, FontFamily = "OpenSansSemibold" }, Texto(pedido.Situacao, 12) } };
            foreach (var item in pedido.Itens) cartao.Children.Add(new VerticalStackLayout { Spacing = 4, Children = {
                new Label { Text = item.NomeProduto, FontFamily = "OpenSansSemibold" }, Texto($"Solicitado: {item.QuantidadeSolicitada} · Atendido: {item.QuantidadeAtendida}", 12)
            } });
            cartao.Children.Add(Texto($"Valor previsto: {Dinheiro(pedido.TotalPrevisto)}", 12));
            cartao.Children.Add(Titulo($"Confirmado: {Dinheiro(pedido.TotalFinal)}"));
            conteudoPagina.Children.Add(Cartao(cartao));
        }
        var voltar = BotaoDiscreto("Voltar aos meus lotes"); voltar.Clicked += async (_, _) => await Executar(voltar, MostrarLotesAsync); conteudoPagina.Children.Add(voltar);
    }

    private async Task EnviarNovoAsync()
    {
        if (File.Exists(arquivo)) throw new InvalidOperationException("Existe um envio pendente. Use Reenviar pedido pendente.");
        if (clientes.SelectedItem is not Cliente cliente || carrinho.Count == 0 || condicoes.SelectedItem is not Condicao condicao || pagamento.SelectedItem is not string forma)
            throw new InvalidOperationException("Selecione o cliente, adicione produtos e escolha a condição e forma de pagamento.");
        var agora = DateTimeOffset.Now;
        var lote = new { identificadorEnvio = Guid.NewGuid(), dataCriacaoLocal = agora,
            visitas = new[] { new { clienteId = cliente.Id, prioridadeOriginal = cliente.PrioridadeVisita, ordemAtendimento = 1, situacao = "PedidoRegistrado", dataHoraAtendimento = agora } },
            pedidos = new[] { new { clienteId = cliente.Id, condicaoPagamentoId = condicao.Id, formaPagamentoPrevista = forma,
                itens = carrinho.Select(item => new { produtoId = item.Produto.Id, quantidadeSolicitada = item.Quantidade, precoUnitario = item.Produto.Preco }).ToArray() } } };
        var pendente = new Pendente(vendedorId, baseApi, JsonSerializer.Serialize(lote, json));
        await File.WriteAllTextAsync(arquivo, JsonSerializer.Serialize(pendente, json));
        await EnviarPendenteAsync(pendente);
    }

    private async Task ReenviarAsync()
    {
        if (!File.Exists(arquivo)) throw new InvalidOperationException("Não há envio pendente neste dispositivo.");
        var pendente = JsonSerializer.Deserialize<Pendente>(await File.ReadAllTextAsync(arquivo), json) ?? throw new InvalidOperationException("Não foi possível ler o envio pendente.");
        if (pendente.VendedorId != vendedorId || pendente.BaseApi != baseApi) throw new InvalidOperationException("Entre com o vendedor e o endereço usados no envio pendente.");
        await EnviarPendenteAsync(pendente);
    }

    private async Task EnviarPendenteAsync(Pendente pendente)
    {
        using var corpo = new StringContent(pendente.Conteudo, Encoding.UTF8, "application/json");
        using var resposta = await http.PostAsync(baseApi + "/api/lotes", corpo);
        if (resposta.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var erro = await resposta.Content.ReadAsStringAsync();
            File.Delete(arquivo);
            throw new InvalidOperationException($"Pedido rejeitado pela API. Atualize os dados e crie outro pedido. {erro}");
        }
        await Verificar(resposta);
        var recebido = await resposta.Content.ReadFromJsonAsync<Recebido>(json);
        armazenamento.SalvarRascunho(ChaveLocal(), null);
        File.Delete(arquivo);
        carrinho.Clear();
        MostrarPedido();
        aviso.Text = $"Pedido enviado. Lote {recebido?.Id}: {recebido?.Mensagem}";
    }

    private async Task<T> Obter<T>(string rota) => await http.GetFromJsonAsync<T>(baseApi + rota, json) ?? throw new InvalidOperationException("Resposta vazia da API.");
    private string ChaveLocal() => $"{baseApi}|{vendedorId}";
    private void SalvarRascunho()
    {
        if (restaurando || vendedorId <= 0 || string.IsNullOrEmpty(baseApi)) return;
        try
        {
            var rascunho = new Rascunho((clientes.SelectedItem as Cliente)?.Id, (condicoes.SelectedItem as Condicao)?.Id,
                pagamento.SelectedItem as string, carrinho.Select(i => new ItemRascunho(i.Produto, i.Quantidade)).ToList());
            armazenamento.SalvarRascunho(ChaveLocal(), JsonSerializer.Serialize(rascunho, json));
        }
        catch (Exception) { aviso.Text = "Não foi possível salvar o rascunho no dispositivo."; }
    }
    private static async Task Verificar(HttpResponseMessage resposta)
    {
        if (resposta.IsSuccessStatusCode) return;
        var corpo = await resposta.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"API retornou {(int)resposta.StatusCode}. {corpo}");
    }
    private async Task Executar(Button botao, Func<Task> acao)
    {
        if (ocupado) return;
        ocupado = true;
        carregando.IsVisible = true; carregando.IsRunning = true;
        botao.IsEnabled = false; aviso.Text = "";
        try { await acao(); }
        catch (HttpRequestException) { aviso.Text = "Não foi possível conectar à API. Confira o endereço e a conexão."; }
        catch (TaskCanceledException) { aviso.Text = "A conexão demorou demais. Se enviou um pedido, tente reenviar o pendente."; }
        catch (Exception erro) { aviso.Text = erro.Message; }
        finally { botao.IsEnabled = true; ocupado = false; carregando.IsVisible = false; carregando.IsRunning = false; }
    }

    private sealed record Login(string Token, Usuario Usuario);
    private sealed record Usuario(int Id, string Nome, string Perfil);
    private sealed record Cliente(int Id, string Nome, string Documento, int PrioridadeVisita);
    private sealed record Produto(int Id, string Nome, decimal Preco, string Categoria, string UnidadeVenda) { public string DescricaoExibicao => $"{Nome} — {Preco:C}"; }
    private sealed record Condicao(int Id, string Nome);
    private sealed record Pendente(int VendedorId, string BaseApi, string Conteudo);
    private sealed record Recebido(int Id, string Mensagem);
    private sealed class ItemCarrinho(Produto produto, int quantidade) { public Produto Produto { get; } = produto; public int Quantidade { get; set; } = quantidade; }
    private sealed record Lote(int Id, string Situacao, DateTimeOffset DataRecebimento);
    private sealed record Detalhes(Lote Lote, List<PedidoDetalhe> Pedidos);
    private sealed record PedidoDetalhe(string Cliente, string Situacao, decimal TotalPrevisto, decimal TotalFinal, List<ItemDetalhe> Itens);
    private sealed record ItemDetalhe(string NomeProduto, int QuantidadeSolicitada, int QuantidadeAtendida);
    private sealed record Sincronizacao(List<Cliente> Carteira, List<Produto> Catalogo, List<Condicao> Planos);
    private sealed record Rascunho(int? ClienteId, int? CondicaoId, string? FormaPagamento, List<ItemRascunho> Itens);
    private sealed record ItemRascunho(Produto Produto, int Quantidade);
}
