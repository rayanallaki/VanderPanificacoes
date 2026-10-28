param(
    [string]$BaseUrl = "http://localhost:5296",
    [int]$VendedorId = 3,
    [int]$ClienteId = 1,
    [int]$CategoriaId = 2,
    [int]$AdministradorId = 1,
    [Parameter(Mandatory = $true)][string]$TokenVendedor,
    [Parameter(Mandatory = $true)][string]$TokenAdministrador
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$http = [System.Net.Http.HttpClient]::new()
$cabecalhoVendedor = @{ Authorization = "Bearer $TokenVendedor" }
$cabecalhoAdministrador = @{ Authorization = "Bearer $TokenAdministrador" }

function EnviarJson([string]$rota, $objeto) {
    $token = if ($rota -eq '/api/lotes') { $TokenVendedor } else { $TokenAdministrador }
    $http.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)
    $json = ConvertTo-Json -InputObject $objeto -Depth 20 -Compress
    $conteudo = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, 'application/json')
    try {
        $resposta = $http.PostAsync("$BaseUrl$rota", $conteudo).GetAwaiter().GetResult()
        $texto = $resposta.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return @{ Codigo = [int]$resposta.StatusCode; Corpo = $texto }
    } finally { $conteudo.Dispose() }
}

function Verificar([bool]$condicao, [string]$mensagem) {
    if (-not $condicao) { throw $mensagem }
    Write-Host "OK: $mensagem"
}

try {
    $identidadeVendedor = Invoke-RestMethod -Uri "$BaseUrl/api/autenticacao/eu" -Headers $cabecalhoVendedor
    $identidadeAdmin = Invoke-RestMethod -Uri "$BaseUrl/api/autenticacao/eu" -Headers $cabecalhoAdministrador
    if ($identidadeVendedor.perfil -ne 'Vendedor' -or [int]$identidadeVendedor.id -ne $VendedorId -or
        $identidadeAdmin.perfil -ne 'Administrador' -or [int]$identidadeAdmin.id -ne $AdministradorId) {
        throw 'Os tokens nao correspondem aos IDs e perfis de teste.'
    }
    $sufixo = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $produto = EnviarJson '/api/produtos' @{
        nome = "Teste integracao $sufixo"; categoriaId = $CategoriaId
        unidadeVenda = 'Unidade'; preco = 1.00; perecivel = $false
    }
    Verificar ($produto.Codigo -eq 201) "Produto exclusivo criado: $($produto.Corpo)"
    $produtoId = ($produto.Corpo | ConvertFrom-Json).id

    $estoque = EnviarJson '/api/estoque/entradas' @{
        identificadorOperacao = [guid]::NewGuid().ToString(); produtoId = $produtoId
        usuarioId = $AdministradorId; quantidade = 1; motivo = "Estoque teste $sufixo"
    }
    Verificar ($estoque.Codigo -eq 201) "Uma unidade adicionada ao produto de teste: $($estoque.Corpo)"

    function NovoLote {
        $data = [DateTimeOffset]::Now.ToString('o')
        return @{
            identificadorEnvio = [guid]::NewGuid().ToString(); vendedorId = $VendedorId; dataCriacaoLocal = $data
            visitas = @(@{ clienteId = $ClienteId; prioridadeOriginal = 1; ordemAtendimento = 1
                situacao = 'PedidoRegistrado'; dataHoraAtendimento = $data })
            pedidos = @(@{ clienteId = $ClienteId; condicaoPagamentoId = 1; formaPagamentoPrevista = 'Pix'
                observacao = "Teste integracao $sufixo"; itens = @(@{ produtoId = $produtoId; quantidadeSolicitada = 1; precoUnitario = 1.00 }) })
        }
    }

    $primeiro = NovoLote
    $envio1 = EnviarJson '/api/lotes' $primeiro
    Verificar ($envio1.Codigo -eq 201) "Primeiro lote recebido: $($envio1.Corpo)"
    $id1 = ($envio1.Corpo | ConvertFrom-Json).id
    $reenvio = EnviarJson '/api/lotes' $primeiro
    Verificar ($reenvio.Codigo -eq 200 -and ($reenvio.Corpo | ConvertFrom-Json).id -eq $id1) "Reenvio retornou o mesmo lote: $($reenvio.Corpo)"
    $primeiro.pedidos[0].itens[0].quantidadeSolicitada = 2
    $conflito = EnviarJson '/api/lotes' $primeiro
    Verificar ($conflito.Codigo -eq 409) "Mesmo identificador com conteúdo alterado foi rejeitado"

    $segundo = NovoLote
    $envio2 = EnviarJson '/api/lotes' $segundo
    Verificar ($envio2.Codigo -eq 201) "Segundo lote recebido: $($envio2.Corpo)"
    $id2 = ($envio2.Corpo | ConvertFrom-Json).id
    $detalhe1 = Invoke-RestMethod -Uri "$BaseUrl/api/lotes/$id1" -Headers $cabecalhoVendedor
    $detalhe2 = Invoke-RestMethod -Uri "$BaseUrl/api/lotes/$id2" -Headers $cabecalhoVendedor
    $item1 = $detalhe1.pedidos[0].itens[0].id
    $item2 = $detalhe2.pedidos[0].itens[0].id

    $json1 = ConvertTo-Json -InputObject @{ usuarioId = $AdministradorId; itens = @(@{ itemPedidoId = $item1; quantidadeAtendida = 1 }) } -Depth 10 -Compress
    $json2 = ConvertTo-Json -InputObject @{ usuarioId = $AdministradorId; itens = @(@{ itemPedidoId = $item2; quantidadeAtendida = 1 }) } -Depth 10 -Compress
    $content1 = [System.Net.Http.StringContent]::new($json1, [System.Text.Encoding]::UTF8, 'application/json')
    $content2 = [System.Net.Http.StringContent]::new($json2, [System.Text.Encoding]::UTF8, 'application/json')
    try {
        $http.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $TokenAdministrador)
        $tarefa1 = $http.PostAsync("$BaseUrl/api/lotes/$id1/confirmacao", $content1)
        $tarefa2 = $http.PostAsync("$BaseUrl/api/lotes/$id2/confirmacao", $content2)
        $resp1 = $tarefa1.GetAwaiter().GetResult()
        $resp2 = $tarefa2.GetAwaiter().GetResult()
        $codigo1 = [int]$resp1.StatusCode; $codigo2 = [int]$resp2.StatusCode
        $corpo1 = $resp1.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $corpo2 = $resp2.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $codigos = @($codigo1, $codigo2) | Sort-Object
        Verificar (($codigos -join ',') -eq '200,409') "Disputa pelo estoque: lote $id1 HTTP $codigo1 ($corpo1); lote $id2 HTTP $codigo2 ($corpo2)"
    } finally { $content1.Dispose(); $content2.Dispose() }

    $estado1 = Invoke-RestMethod -Uri "$BaseUrl/api/lotes/$id1" -Headers $cabecalhoVendedor
    $estado2 = Invoke-RestMethod -Uri "$BaseUrl/api/lotes/$id2" -Headers $cabecalhoVendedor
    $produtos = Invoke-RestMethod -Uri "$BaseUrl/api/produtos" -Headers $cabecalhoAdministrador
    $produtoFinal = @($produtos | Where-Object { $_.id -eq $produtoId })[0]
    $movimentos = @(Invoke-RestMethod -Uri "$BaseUrl/api/estoque/movimentacoes?produtoId=$produtoId" -Headers $cabecalhoAdministrador)
    Verificar ($produtoFinal.estoque -eq 0) 'Estoque de teste permaneceu em zero'
    $situacoes = @($estado1.lote.situacao, $estado2.lote.situacao)
    Verificar (@($situacoes | Where-Object { $_ -eq 'Confirmado' }).Count -eq 1) 'Somente um lote foi confirmado'
    Verificar (@($movimentos | Where-Object { $_.tipo -eq 'Venda' }).Count -eq 1) 'Somente uma movimentação de venda foi criada'
    $confirmadoId = if ($codigo1 -eq 200) { $id1 } else { $id2 }
    $confirmadoItem = if ($codigo1 -eq 200) { $item1 } else { $item2 }
    $confirmacao = @{ usuarioId = $AdministradorId; itens = @(@{ itemPedidoId = $confirmadoItem; quantidadeAtendida = 1 }) }
    $repeticao = EnviarJson "/api/lotes/$confirmadoId/confirmacao" $confirmacao
    Verificar ($repeticao.Codigo -eq 200 -and $repeticao.Corpo -match 'confirmado') 'Repetição da confirmação não gerou nova baixa'
    $confirmacao.itens[0].quantidadeAtendida = 0
    $diferente = EnviarJson "/api/lotes/$confirmadoId/confirmacao" $confirmacao
    Verificar ($diferente.Codigo -eq 409) 'Confirmação com conteúdo diferente foi rejeitada'
    $movimentosDepois = @(Invoke-RestMethod -Uri "$BaseUrl/api/estoque/movimentacoes?produtoId=$produtoId" -Headers $cabecalhoAdministrador)
    Verificar (@($movimentosDepois | Where-Object { $_.tipo -eq 'Venda' }).Count -eq 1) 'Repetições preservaram uma única movimentação de venda'
    Write-Host "Teste concluído. Produto de teste ID $produtoId; lotes IDs $id1 e $id2. Os registros foram preservados."
} finally { $http.Dispose() }
