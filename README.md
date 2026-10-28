# Vander Panificações — demonstração acadêmica

Sistema acadêmico com três projetos separados: API em ASP.NET Core e MySQL, aplicação web administrativa em ASP.NET Core MVC e aplicativo do vendedor em .NET MAUI com SQLite local.

## Requisitos

- .NET SDK 10 e MySQL 8.
- Uma conta MySQL com permissão para criar e alterar tabelas.

## Configuração inicial

No PowerShell, na pasta do projeto, configure a conexão com os dados locais. Não inclua a senha em arquivos do projeto:

```powershell
dotnet user-secrets set "ConnectionStrings:MySql" "Server=localhost;Port=3306;Database=vanderpanificacoes;User=SEU_USUARIO;Password=SUA_SENHA" --project VanderPanificacoes.Api
```

Configure também uma chave de desenvolvimento para os tokens. Execute uma vez; alterar essa chave invalida os tokens anteriores:

```powershell
$chave = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "JwtDesenvolvimento:ChaveBase64" $chave --project VanderPanificacoes.Api
$chave = $null
```

Restaure a ferramenta de migração e atualize o banco:

```powershell
dotnet tool restore
dotnet ef database update --project VanderPanificacoes.Api --configuration Migration
```

Se ainda não existir um administrador, crie-o em um terminal interativo:

```powershell
dotnet run --project VanderPanificacoes.Api -- criar-admin
```

## Executar

Na pasta principal, abra dois terminais. No primeiro, execute a API:

```powershell
dotnet watch --project VanderPanificacoes.Api run
```

No segundo, execute a aplicação web:

```powershell
dotnet run --project VanderPanificacoes.Web --launch-profile http
```

Abra **http://localhost:5300** e entre com a conta do administrador. Mantenha a API em http://localhost:5296 e o MySQL em execução. O endereço da API usado pela aplicação web fica em [appsettings.json](VanderPanificacoes.Web/appsettings.json), na configuração `Api:BaseUrl`.

Como administrador, a área web permite cadastrar e editar vendedores, clientes, categorias e produtos, desativar cadastros, registrar entradas e ajustes de estoque, conferir ou cancelar lotes e registrar o recebimento integral de parcelas. A confirmação do lote gera vendas e parcelas e baixa o estoque; a prévia não altera o banco. Receber uma parcela é um registro manual independente da confirmação do lote.

O painel também apresenta indicadores de lotes pendentes, valores a receber, parcelas vencidas e produtos ativos, além de filtros nas tabelas. A aplicação web possui seus próprios Controllers, Views, Models e Services. Ela consome a API com autenticação, sem acessar o MySQL diretamente. A navegação e as operações funcionam com links e formulários HTML, sem depender de JavaScript. O painel antigo foi removido da API; abrir a raiz da API em desenvolvimento encaminha para a aplicação web.

Os testes iniciam a aplicação MVC de verdade, abrem o Microsoft Edge sem interface visível e executam os links e formulários com JavaScript desativado. Usam uma API simulada e não alteram o MySQL. Requerem Node.js e Microsoft Edge instalados neste computador:

```powershell
dotnet build VanderPanificacoes.Web
npm --prefix tests/Web ci
npm --prefix tests/Web test
```

As capturas dos testes ficam em `tests/Web/artifacts` e não são versionadas.

## Funcionalidades da API

- Login de desenvolvimento, contas de vendedores e carteira de clientes.
- Cadastro de clientes, categorias e produtos; entrada, ajustes e histórico de estoque.
- Envio de lotes com validação e proteção contra reenvios duplicados.
- Sugestão, prévia, confirmação e cancelamento de lotes recebidos.
- Consulta de vendas e parcelas e registro manual de recebimento integral.
- Permissões por perfil nas operações protegidas. O administrador gerencia os cadastros; o vendedor consulta sua carteira e o catálogo ativo e envia seus lotes.

Exemplos de requisições estão em [VanderPanificacoes.Api.http](VanderPanificacoes.Api/VanderPanificacoes.Api.http). Substitua os tokens e IDs pelos dados do ambiente; requisições de escrita alteram o banco.

Para verificar os cálculos e a distribuição de estoque:

```powershell
dotnet run --project tests\Calculos
```

## Aplicativo do vendedor

O projeto `VanderPanificacoes.Mobile` é um protótipo .NET MAUI. Ele permite fazer login como vendedor, pesquisar carteira e catálogo, montar um pedido com vários produtos e acompanhar os próprios lotes e seus resultados. A carteira, o catálogo, as condições de pagamento e o rascunho são armazenados no SQLite do dispositivo, por vendedor e endereço da API. O lote em tentativa de envio é salvo separadamente no armazenamento local, para permitir uma nova tentativa com o mesmo identificador quando houver falha. Um envio pendente deve ser reenviado com o mesmo vendedor e endereço da API antes de criar outro pedido.

Para testar no emulador Android, mantenha a API em execução e use no aplicativo o endereço `http://10.0.2.2:5296`, que aponta para o computador onde o emulador está rodando. No Windows, use `http://localhost:5296`. Um telefone físico exige um endereço acessível na rede e configuração apropriada da API. HTTP sem criptografia é usado somente para a demonstração local.

```powershell
dotnet build VanderPanificacoes.Mobile -f net10.0-android
```

É necessário ter o SDK Android e um emulador configurados para executar o aplicativo. O login inicial e o envio exigem conexão. Após entrar e sincronizar, o vendedor pode consultar os dados já carregados e editar seu rascunho sem conexão; uma atualização com falha de conexão usa a última cópia salva. Após fechar o aplicativo, o login precisa ser feito novamente online. O protótipo ainda não oferece ciclos completos de atendimento com múltiplos clientes.

Neste computador, a compilação Android apresentou o erro APT2265. A mensagem indica que caracteres não ASCII no caminho podem ser a causa; essa hipótese ainda precisa ser confirmada. O alvo Windows compilou com sucesso.

## Limites desta entrega

Esta é uma versão simplificada para demonstração local. A aplicação web administrativa está separada da API. A proposta original em [Documentacao.txt](Documentacao.txt) inclui funcionalidades offline além da cópia local e do rascunho implementados. Nem todos os critérios de aceitação da proposta original foram verificados.

A autenticação, o painel e diversos endpoints são destinados ao ambiente de desenvolvimento; esta configuração não está pronta para publicação ou uso em produção. Credenciais ficam nos User Secrets, que são apropriados somente para desenvolvimento.
