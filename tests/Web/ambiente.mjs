import http from 'node:http';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { fileURLToPath } from 'node:url';

export async function iniciarAmbiente() {
  const hoje = new Date().toISOString().slice(0, 10);
  const estado = {
    categorias: [{ id: 1, nome: 'Pães', ativo: true }],
    produtos: [{ id: 2, nome: 'Pão de forma', categoriaId: 1, categoria: 'Pães', preco: 10, estoque: 20, unidadeVenda: 'Pacote', perecivel: false, ativo: true }],
    vendedores: [{ id: 3, nome: 'Vendedor da demonstração', email: 'vendedor@demo.local', ativo: true }],
    clientes: [{ id: 1, nome: 'Mercado Exemplo', documento: '12345678901', vendedorId: 3, vendedor: 'Vendedor da demonstração', prioridadeVisita: 1, ativo: true }],
    lotes: [1, 2].map(id => ({ id, vendedorId: 3, vendedor: 'Vendedor da demonstração', situacao: 'Recebido', dataRecebimento: new Date().toISOString() })),
    parcelas: [{ id: 1, vendaId: 1, cliente: 'Mercado Exemplo', numero: 1, vencimento: hoje, valorBase: 19, situacao: 'Aberta', formaPagamentoPrevista: 'Pix' }],
    vendas: [{ id: 1, pedidoId: 20, lotePedidosId: 1, cliente: 'Mercado Exemplo', dataConfirmacao: new Date().toISOString(), totalBruto: 20, desconto: 1, totalLiquido: 19 }],
    movimentos: [], chamadas: []
  };
  const api = http.createServer(async (req, res) => {
    res.setHeader('Content-Type', 'application/json');
    const responder = (corpo, codigo = 200) => { res.statusCode = codigo; res.end(JSON.stringify(corpo)); };
    try {
      const url = new URL(req.url, 'http://localhost'); const rota = url.pathname;
      let texto = ''; for await (const parte of req) texto += parte;
      const dados = texto ? JSON.parse(texto) : null;
      estado.chamadas.push({ metodo: req.method, rota, dados });
      if (rota === '/api/autenticacao/login') return responder({ token: 'token-somente-ambiente-simulado', expiraEm: new Date(Date.now() + 3600000).toISOString(), usuario: { id: dados.email.startsWith('vendedor') ? 3 : 1, nome: 'Administrador Demo', perfil: dados.email.startsWith('vendedor') ? 'Vendedor' : 'Administrador' } });
      if (req.headers.authorization !== 'Bearer token-somente-ambiente-simulado') return responder({ mensagem: 'Sem autenticação' }, 401);
      if (rota === '/api/estoque/movimentacoes') return responder(estado.movimentos);
      if (rota.startsWith('/api/estoque/') && req.method === 'POST') {
        const produto = estado.produtos.find(p => p.id === dados.produtoId);
        const saida = rota.endsWith('ajustes-saida');
        if (!produto || (saida && dados.quantidade > produto.estoque)) return responder({ mensagem: 'Quantidade incompatível com o saldo disponível.' }, 400);
        const anterior = estado.movimentos.find(m => m.identificadorOperacao === dados.identificadorOperacao);
        if (!anterior) { produto.estoque += saida ? -dados.quantidade : dados.quantidade; estado.movimentos.unshift({ ...dados, id: estado.movimentos.length + 1, produto: produto.nome, usuario: 'Administrador Demo', tipo: saida ? 'AjusteSaida' : 'Entrada', dataHora: new Date().toISOString() }); }
        return responder({ mensagem: 'Estoque atualizado' });
      }
      if (rota.startsWith('/api/lotes/')) {
        const [, , , numero, acao] = rota.split('/');
        const lote = estado.lotes.find(l => l.id === Number(numero));
        if (!lote) return responder({ mensagem: 'Lote não encontrado' }, 404);
        if (!acao) return responder({ lote, pedidos: [{ id: 11, cliente: 'Mercado Exemplo', condicaoPagamento: 'À vista', formaPagamentoPrevista: 'Pix', situacao: lote.situacao === 'Confirmado' ? 'AtendidoTotal' : 'Recebido', totalFinal: 19, itens: [{ id: 8, nomeProduto: 'Pão de forma', precoUnitario: 10, quantidadeSolicitada: 2, quantidadeAtendida: lote.situacao === 'Confirmado' ? 2 : 0 }] }] });
        if (acao === 'sugestao') return responder({ pedidos: [{ itens: [{ itemPedidoId: 8, quantidadeSugerida: 2 }] }] });
        if (acao === 'conferencia') return responder({ pedidos: [{ pedidoId: 11, totalLiquido: dados.itens[0].quantidadeAtendida * 9.5, parcelas: [{ numero: 1, prazoDias: 0, valor: dados.itens[0].quantidadeAtendida * 9.5 }] }] });
        if (acao === 'confirmacao') { lote.situacao = 'Confirmado'; return responder({ mensagem: 'Confirmado' }); }
        if (acao === 'cancelamento') { lote.situacao = 'Cancelado'; return responder({ mensagem: 'Cancelado' }); }
      }
      if (/^\/api\/parcelas\/\d+\/recebimento$/.test(rota)) {
        const parcela = estado.parcelas.find(p => p.id === Number(rota.split('/')[3]));
        if (dados.valorRecebido !== parcela.valorBase) return responder({ mensagem: 'Valor inválido' }, 400);
        Object.assign(parcela, { situacao: 'Paga', dataRecebimento: dados.dataRecebimento, valorRecebido: dados.valorRecebido, formaPagamentoRecebida: dados.formaPagamentoRecebida }); return responder({ mensagem: 'Paga' });
      }
      const [, , tipo, numero, acao] = rota.split('/');
      if (Array.isArray(estado[tipo])) {
        const lista = estado[tipo];
        if (req.method === 'GET') return responder(tipo === 'parcelas' && url.searchParams.get('situacao') ? lista.filter(p => p.situacao === url.searchParams.get('situacao')) : lista);
        if (req.method === 'POST') { const item = { ...dados, id: Math.max(0, ...lista.map(x => x.id)) + 1, ativo: true }; lista.push(item); return responder(item, 201); }
        const item = lista.find(x => x.id === Number(numero));
        if (!item) return responder({ mensagem: 'Não encontrado' }, 404);
        if (req.method === 'PUT') Object.assign(item, dados);
        if (req.method === 'PATCH') item.ativo = acao === 'reativar';
        return responder(item);
      }
      responder({ mensagem: 'Rota de teste inexistente: ' + rota }, 404);
    } catch (erro) { responder({ mensagem: erro.message }, 500); }
  });
  api.listen(0, '127.0.0.1'); await once(api, 'listening');
  const reserva = http.createServer(); reserva.listen(0, '127.0.0.1'); await once(reserva, 'listening');
  const portaWeb = reserva.address().port; await new Promise(resolve => reserva.close(resolve));
  const raiz = fileURLToPath(new URL('../../', import.meta.url));
  const app = spawn('dotnet', ['bin/Debug/net10.0/VanderPanificacoes.Web.dll', '--urls', `http://127.0.0.1:${portaWeb}`], {
    cwd: raiz + 'VanderPanificacoes.Web', windowsHide: true, env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', Api__BaseUrl: `http://127.0.0.1:${api.address().port}/`, Logging__LogLevel__Default: 'Warning' }, stdio: ['ignore', 'pipe', 'pipe']
  });
  let logs = ''; app.stdout.on('data', b => logs += b); app.stderr.on('data', b => logs += b);
  const base = `http://127.0.0.1:${portaWeb}`;
  for (let i = 0; i < 150; i++) {
    try { const r = await fetch(base + '/conta/entrar'); if (r.ok) return { base, estado, fechar: async () => { app.kill(); api.closeAllConnections(); await new Promise(resolve => api.close(resolve)); } }; } catch { }
    if (app.exitCode !== null) { api.close(); throw new Error('A aplicação web não iniciou: ' + logs); }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  app.kill(); api.close(); throw new Error('Tempo de inicialização excedido: ' + logs);
}
