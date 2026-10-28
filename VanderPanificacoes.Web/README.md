# Aplicação web administrativa

Projeto ASP.NET Core MVC separado da API e do aplicativo MAUI.

## Abrir

Na pasta principal da solução:

```powershell
# Terminal 1: API
dotnet run --project VanderPanificacoes.Api --launch-profile http
```

Em outro terminal:

```powershell
# Terminal 2: Web
dotnet run --project VanderPanificacoes.Web --launch-profile http
```

Abra **http://localhost:5300** e entre com o administrador cadastrado na API. O MySQL também precisa estar em execução. A aplicação web não possui credenciais do MySQL; apenas o endereço da API em [appsettings.json](appsettings.json).

## Organização

- [Controllers](Controllers): navegação e envio dos formulários administrativos.
- [Views](Views): páginas Razor, formulários e layout compartilhado.
- [Models](Models): dados das telas e definições dos cadastros.
- [Services](Services): cliente HTTP autenticado da API e tratamento de falhas.
- [wwwroot/css/admin.css](wwwroot/css/admin.css): estilos da interface.

## Fluxo para apresentação

1. Consulte ou cadastre categoria, produto, vendedor e cliente.
2. Registre uma entrada de estoque.
3. Envie um pedido pelo aplicativo do vendedor.
4. Em **Lotes e pedidos**, abra o lote e calcule a prévia.
5. Revise as quantidades, se necessário, e confirme o lote.
6. Consulte a venda em **Vendas**.
7. Em **Parcelas**, abra **Receber parcela** e confirme o recebimento integral.

As operações principais usam links e formulários HTML e funcionam sem JavaScript. A sessão do administrador fica em cookie protegido e o token é enviado à API somente pelo servidor web. Os formulários incluem proteção contra requisições forjadas.

Os testes automatizados do navegador ficam em [tests/Web](../tests/Web). Eles executam a aplicação web com uma API simulada, sem alterar o banco real.
