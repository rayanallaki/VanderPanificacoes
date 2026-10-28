import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { iniciarAmbiente } from './ambiente.mjs';

test('MVC: links e formulários executam os fluxos administrativos no navegador', { timeout: 180000 }, async () => {
  const ambiente = await iniciarAmbiente();
  let navegador;
  try {
    navegador = await chromium.launch({ channel: 'msedge', headless: true });
    const contexto = await navegador.newContext({ javaScriptEnabled: false, viewport: { width: 1440, height: 1000 } });
    const pagina = await contexto.newPage();
    const falhas = []; pagina.on('pageerror', erro => falhas.push(erro.message));
    await pagina.goto(ambiente.base + '/cadastros/categorias');
    assert.match(pagina.url(), /\/conta\/entrar/);
    await pagina.getByLabel('E-mail', { exact: true }).fill('admin@demo.local');
    await pagina.getByLabel('Senha', { exact: true }).fill('SenhaDemo123');
    await pagina.getByRole('button', { name: 'Entrar no sistema' }).click();
    await pagina.getByRole('heading', { name: 'Visão geral' }).waitFor();
    await mkdir(new URL('./artifacts/', import.meta.url), { recursive: true });
    await pagina.screenshot({ path: fileURLToPath(new URL('./artifacts/painel.png', import.meta.url)), fullPage: true });
    console.log('OK: autenticação e painel, com JavaScript desativado');

    await pagina.getByRole('link', { name: 'Categorias', exact: true }).click();
    await pagina.getByRole('link', { name: '+ Novo cadastro' }).click();
    await pagina.getByLabel('Nome da categoria').fill('Bolos artesanais');
    await pagina.getByRole('button', { name: 'Salvar cadastro' }).click();
    await pagina.getByText('Cadastro salvo com sucesso.').waitFor();
    const categoria = pagina.getByRole('row').filter({ hasText: 'Bolos artesanais' });
    await categoria.getByRole('link', { name: 'Editar' }).click();
    await pagina.getByLabel('Nome da categoria').fill('Bolos especiais');
    await pagina.getByRole('button', { name: 'Salvar cadastro' }).click();
    await pagina.getByRole('row').filter({ hasText: 'Bolos especiais' }).getByRole('button', { name: 'Desativar' }).click();
    assert.equal(ambiente.estado.categorias.find(c => c.nome === 'Bolos especiais').ativo, false);
    console.log('OK: cadastrar, editar e desativar categoria');

    await pagina.getByRole('link', { name: 'Clientes', exact: true }).click();
    await pagina.getByRole('link', { name: '+ Novo cadastro' }).click();
    await pagina.getByLabel('Nome do estabelecimento').fill('Mercado da apresentação');
    await pagina.getByLabel('Documento').fill('98765432100');
    await pagina.getByLabel('Vendedor responsável').selectOption('3');
    await pagina.getByRole('button', { name: 'Salvar cadastro' }).click();
    await pagina.getByText('Cadastro salvo com sucesso.').waitFor();
    assert.equal(ambiente.estado.clientes.find(c => c.nome === 'Mercado da apresentação').vendedorId, 3);
    console.log('OK: cadastrar cliente vinculado ao vendedor');

    await pagina.getByRole('link', { name: 'Produtos', exact: true }).click();
    await pagina.getByRole('link', { name: '+ Novo cadastro' }).click();
    await pagina.getByLabel('Nome do produto').fill('Biscoito de teste');
    await pagina.getByLabel('Categoria').selectOption('1');
    await pagina.getByLabel('Unidade de venda').selectOption('Pacote');
    await pagina.getByLabel('Preço (R$)').fill('12.50');
    await pagina.getByRole('button', { name: 'Salvar cadastro' }).click();
    await pagina.getByText('Cadastro salvo com sucesso.').waitFor();
    assert.equal(ambiente.estado.produtos.find(p => p.nome === 'Biscoito de teste').preco, 12.5);
    console.log('OK: cadastrar produto e enviar preço decimal');

    await pagina.getByRole('link', { name: 'Estoque', exact: true }).click();
    await pagina.getByRole('link', { name: '+ Registrar movimentação' }).click();
    await pagina.getByLabel('Produto', { exact: true }).selectOption('2');
    await pagina.getByLabel('Quantidade', { exact: true }).fill('5');
    await pagina.getByLabel('Motivo', { exact: true }).fill('Entrada para demonstração');
    await pagina.getByRole('button', { name: 'Registrar movimentação', exact: true }).click();
    await pagina.getByText('Movimentação registrada e saldo atualizado.').waitFor();
    assert.equal(ambiente.estado.produtos.find(p => p.id === 2).estoque, 25);
    await pagina.getByRole('link', { name: '+ Registrar movimentação' }).click();
    await pagina.getByLabel('Produto', { exact: true }).selectOption('2');
    await pagina.getByLabel('Tipo', { exact: true }).selectOption('ajustes-saida');
    await pagina.getByLabel('Quantidade', { exact: true }).fill('9999');
    await pagina.getByLabel('Motivo', { exact: true }).fill('Teste de rejeição');
    await pagina.getByRole('button', { name: 'Registrar movimentação', exact: true }).click();
    await pagina.getByText('Quantidade incompatível com o saldo disponível.').waitFor();
    assert.equal(await pagina.getByLabel('Quantidade', { exact: true }).inputValue(), '9999');
    console.log('OK: movimentação de estoque e erro exibido sem perder formulário');

    await pagina.getByRole('link', { name: 'Lotes e pedidos', exact: true }).click();
    await pagina.getByRole('row').filter({ hasText: '#1' }).getByRole('link', { name: 'Conferir lote' }).click();
    await pagina.getByLabel('Quantidade atendida de Pão de forma').fill('1');
    await pagina.getByRole('button', { name: 'Calcular prévia' }).click();
    await pagina.getByRole('heading', { name: 'Confira os valores antes de confirmar' }).waitFor();
    await pagina.getByRole('button', { name: 'Revisar quantidades' }).click();
    await pagina.getByLabel('Quantidade atendida de Pão de forma').fill('2');
    await pagina.getByRole('button', { name: 'Calcular prévia' }).click();
    await pagina.getByRole('button', { name: 'Confirmar lote e gerar parcelas' }).click();
    await pagina.getByText('Lote confirmado. Vendas e parcelas foram geradas e o estoque foi atualizado.').waitFor();
    assert.equal(ambiente.estado.lotes[0].situacao, 'Confirmado');
    assert.deepEqual(ambiente.estado.chamadas.find(c => c.rota.endsWith('/confirmacao')).dados.itens, [{ itemPedidoId: 8, quantidadeAtendida: 2 }]);
    console.log('OK: conferir, revisar e confirmar lote');

    await pagina.getByRole('link', { name: 'Lotes e pedidos', exact: true }).click();
    await pagina.getByRole('row').filter({ hasText: '#2' }).getByRole('link', { name: 'Cancelar', exact: true }).click();
    await pagina.getByLabel('Motivo', { exact: true }).fill('Solicitação do cliente');
    await pagina.getByRole('button', { name: 'Confirmar cancelamento' }).click();
    await pagina.getByText('Lote cancelado sem baixa de estoque.').waitFor();
    assert.equal(ambiente.estado.lotes[1].situacao, 'Cancelado');

    await pagina.getByRole('link', { name: 'Parcelas', exact: true }).click();
    await pagina.getByRole('link', { name: 'Receber parcela' }).click();
    await pagina.getByRole('button', { name: 'Confirmar recebimento integral' }).click();
    await pagina.getByText('Recebimento registrado. A parcela está paga.').waitFor();
    assert.equal(ambiente.estado.parcelas[0].situacao, 'Paga');
    assert.equal(ambiente.estado.parcelas[0].valorRecebido, 19);
    console.log('OK: cancelar lote e receber parcela pelo valor integral');

    // Cada link de navegação deve renderizar uma página real, sem ações fictícias.
    for (const rota of ['/vendas', '/cadastros/vendedores', '/parcelas?situacao=Paga', '/lotes?situacao=Confirmado']) {
      const resposta = await pagina.goto(ambiente.base + rota); assert.equal(resposta.status(), 200);
    }
    await pagina.goto(ambiente.base + '/cadastros/produtos');
    await pagina.setViewportSize({ width: 390, height: 844 });
    await pagina.screenshot({ path: fileURLToPath(new URL('./artifacts/mobile-web.png', import.meta.url)), fullPage: true });
    assert.equal(await pagina.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await pagina.getByRole('button', { name: 'Sair', exact: true }).click();
    await pagina.getByRole('heading', { name: 'Bem-vindo de volta' }).waitFor();
    assert.deepEqual(falhas, []);
    console.log('OK: navegação, layout estreito e saída da conta');
    await contexto.close();
  } catch (erro) {
    const paginaFalha = navegador?.contexts()[0]?.pages()[0];
    if (paginaFalha) { console.log('Página em que o teste parou:', await paginaFalha.locator('main').innerText().catch(() => 'sem conteúdo')); await paginaFalha.screenshot({ path: fileURLToPath(new URL('./artifacts/falha.png', import.meta.url)), fullPage: true }); }
    throw erro;
  } finally { if (navegador) await navegador.close(); await ambiente.fechar(); }
});
