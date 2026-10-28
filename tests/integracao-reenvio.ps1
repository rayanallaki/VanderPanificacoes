param(
    [string]$BaseUrl = "http://localhost:5296",
    [int]$VendedorId = 3,
    [int]$ClienteId = 1,
    [int]$ProdutoId = 4,
    [decimal]$Preco = 1.00,
    [Parameter(Mandatory = $true)][string]$TokenVendedor,
    [Parameter(Mandatory = $true)][string]$TokenAdministrador
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$http = [System.Net.Http.HttpClient]::new()
$http.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $TokenVendedor)
$cabecalhoVendedor = @{ Authorization = "Bearer $TokenVendedor" }
$cabecalhoAdministrador = @{ Authorization = "Bearer $TokenAdministrador" }

function CorpoJson($objeto) {
    $json = ConvertTo-Json -InputObject $objeto -Depth 20 -Compress
    return [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, 'application/json')
}

function NovaRequisicao($lote) {
    $corpo = CorpoJson $lote
    try {
        $resposta = $http.PostAsync("$BaseUrl/api/lotes", $corpo).GetAwaiter().GetResult()
        return @{ Codigo = [int]$resposta.StatusCode; Corpo = $resposta.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
    } finally { $corpo.Dispose() }
}

function Verificar([bool]$condicao, [string]$mensagem) {
    if (-not $condicao) { throw $mensagem }
    Write-Host "OK: $mensagem"
}

function NovoLote {
    $agora = [DateTimeOffset]::Now.ToString('o')
    return @{
        identificadorEnvio = [guid]::NewGuid().ToString(); vendedorId = $VendedorId; dataCriacaoLocal = $agora
        visitas = @(@{ clienteId = $ClienteId; prioridadeOriginal = 1; ordemAtendimento = 1
            situacao = 'PedidoRegistrado'; dataHoraAtendimento = $agora })
        pedidos = @(@{ clienteId = $ClienteId; condicaoPagamentoId = 1; formaPagamentoPrevista = 'Pix'
            observacao = 'Teste integrado de reenvio'; itens = @(@{ produtoId = $ProdutoId; quantidadeSolicitada = 1; precoUnitario = $Preco }) })
    }
}

try {
    $identidadeVendedor = Invoke-RestMethod -Uri "$BaseUrl/api/autenticacao/eu" -Headers $cabecalhoVendedor
    $identidadeAdmin = Invoke-RestMethod -Uri "$BaseUrl/api/autenticacao/eu" -Headers $cabecalhoAdministrador
    if ($identidadeVendedor.perfil -ne 'Vendedor' -or [int]$identidadeVendedor.id -ne $VendedorId -or
        $identidadeAdmin.perfil -ne 'Administrador') {
        throw 'Os tokens nao correspondem aos perfis de teste.'
    }
    $invalido = NovoLote
    $invalido.pedidos[0].itens[0].precoUnitario = $Preco + 0.01
    $resultado = NovaRequisicao $invalido
    Verificar ($resultado.Codigo -eq 400 -and $resultado.Corpo -match 'divergente') "Preco divergente rejeitado: $($resultado.Corpo)"
    $consulta = $http.GetAsync("$BaseUrl/api/lotes/envios/$($invalido.identificadorEnvio)").GetAwaiter().GetResult()
    Verificar ([int]$consulta.StatusCode -eq 404) 'Lote rejeitado nao foi gravado'

    $lote = NovoLote
    $conteudo1 = CorpoJson $lote
    $conteudo2 = CorpoJson $lote
    try {
        $tarefa1 = $http.PostAsync("$BaseUrl/api/lotes", $conteudo1)
        $tarefa2 = $http.PostAsync("$BaseUrl/api/lotes", $conteudo2)
        $resposta1 = $tarefa1.GetAwaiter().GetResult()
        $resposta2 = $tarefa2.GetAwaiter().GetResult()
        $codigo1 = [int]$resposta1.StatusCode; $codigo2 = [int]$resposta2.StatusCode
        $texto1 = $resposta1.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $texto2 = $resposta2.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $codigos = @($codigo1, $codigo2) | Sort-Object
        Verificar (($codigos -join ',') -eq '200,201') "Envios simultaneos: HTTP $codigo1 ($texto1) e HTTP $codigo2 ($texto2)"
        Verificar (($texto1 | ConvertFrom-Json).id -eq ($texto2 | ConvertFrom-Json).id) 'Ambos os envios retornaram o mesmo lote'
        $id = ($texto1 | ConvertFrom-Json).id
        $lotes = @(Invoke-RestMethod -Uri "$BaseUrl/api/lotes" -Headers $cabecalhoAdministrador)
        Verificar (@($lotes | Where-Object { $_.identificadorEnvio -eq $lote.identificadorEnvio }).Count -eq 1) 'Somente um lote foi gravado'
        Write-Host "Lote de teste ID $id preservado para verificacao."
    } finally { $conteudo1.Dispose(); $conteudo2.Dispose() }
} finally { $http.Dispose() }
