[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:5206",
    [string]$ContainerName = "sgf_postgres",
    [string]$Database = "sgf_dev",
    [string]$DatabaseUser = "sgf_user"
)

$ErrorActionPreference = "Stop"

if ($ApiBaseUrl -notmatch '^http://(localhost|127\.0\.0\.1)(:\d+)?$') {
    throw "Por seguranca, a carga demo aceita somente uma API HTTP local."
}

function ConvertTo-PlainText([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function Invoke-JsonRequest {
    param([string]$Method, [string]$Path, [object]$Body, [string]$Token)
    $parameters = @{
        Method = $Method
        Uri = "$ApiBaseUrl$Path"
        ContentType = "application/json"
    }
    if ($null -ne $Body) { $parameters.Body = $Body | ConvertTo-Json -Depth 5 }
    if ($Token) { $parameters.Headers = @{ Authorization = "Bearer $Token" } }
    Invoke-RestMethod @parameters
}

Write-Host "SGF - carga controlada de demonstracao" -ForegroundColor Cyan
Write-Host "Esta operacao deve ser executada em um banco local limpo."

try { Invoke-RestMethod "$ApiBaseUrl/api/health/database" | Out-Null }
catch { throw "A API ou o PostgreSQL local nao esta disponivel em $ApiBaseUrl." }

$password = $env:SGF_DEMO_PASSWORD
if (-not $password) {
    $password = ConvertTo-PlainText (Read-Host "Defina a senha local de demo" -AsSecureString)
}
if ($password.Length -lt 8) {
    throw "A senha deve seguir as regras do Identity (8+ caracteres, maiuscula, minuscula, numero e simbolo)."
}

$email = "demo@sgf.local"
$company = "Mercado Exemplo LTDA"
try {
    Invoke-JsonRequest POST "/api/auth/register" @{
        name = "Usuario Demonstracao"
        email = $email
        password = $password
        companyName = $company
    } "" | Out-Null
}
catch {
    throw "Nao foi possivel criar a conta demo. Confirme que o banco esta limpo e que a senha atende a todas as regras do Identity."
}

$login = Invoke-JsonRequest POST "/api/auth/login" @{ email = $email; password = $password } ""
$token = $login.accessToken
if (-not $token) { throw "O login demo nao retornou access token." }

$productDefinitions = @(
    @{ Key="arroz"; Name="Arroz tipo 1 5 kg"; SKU="ARR-5KG"; Cost=24.90; Sale=32.90; Minimum=12; Entry=60; Exit=32; EntryAge=82; ExitAge=9 },
    @{ Key="feijao"; Name="Feijao carioca 1 kg"; SKU="FEI-1KG"; Cost=6.80; Sale=9.90; Minimum=10; Entry=50; Exit=33; EntryAge=80; ExitAge=3 },
    @{ Key="cafe"; Name="Cafe torrado 500 g"; SKU="CAF-500"; Cost=14.50; Sale=21.90; Minimum=18; Entry=50; Exit=35; EntryAge=75; ExitAge=6 },
    @{ Key="leite"; Name="Leite integral 1 L"; SKU="LEI-1L"; Cost=4.20; Sale=6.49; Minimum=15; Entry=80; Exit=44; EntryAge=86; ExitAge=12 },
    @{ Key="acucar"; Name="Acucar refinado 1 kg"; SKU="ACU-1KG"; Cost=3.90; Sale=5.99; Minimum=15; Entry=100; Exit=63; EntryAge=79; ExitAge=8 },
    @{ Key="oleo"; Name="Oleo de soja 900 ml"; SKU="OLE-900"; Cost=6.70; Sale=9.49; Minimum=12; Entry=60; Exit=23; EntryAge=73; ExitAge=11 },
    @{ Key="macarrao"; Name="Macarrao espaguete 500 g"; SKU="MAC-500"; Cost=3.20; Sale=5.49; Minimum=20; Entry=80; Exit=57; EntryAge=84; ExitAge=4 },
    @{ Key="biscoito"; Name="Biscoito integral 200 g"; SKU="BIS-200"; Cost=3.80; Sale=6.90; Minimum=12; Entry=40; Exit=30; EntryAge=68; ExitAge=13 },
    @{ Key="detergente"; Name="Detergente neutro 500 ml"; SKU="DET-500"; Cost=1.90; Sale=3.49; Minimum=8; Entry=50; Exit=26; EntryAge=77; ExitAge=2 },
    @{ Key="sabonete"; Name="Sabonete neutro 90 g"; SKU="SAB-90"; Cost=1.80; Sale=3.50; Minimum=6; Entry=20; Exit=8; EntryAge=60; ExitAge=45 },
    @{ Key="papel-toalha"; Name="Papel toalha com 2 rolos"; SKU="PAP-2RL"; Cost=5.40; Sale=8.90; Minimum=10; Entry=40; Exit=31; EntryAge=70; ExitAge=16 },
    @{ Key="agua"; Name="Agua mineral sem gas 1,5 L"; SKU="AGU-15L"; Cost=2.10; Sale=3.99; Minimum=20; Entry=80; Exit=42; EntryAge=78; ExitAge=14 },
    @{ Key="farinha"; Name="Farinha de trigo 1 kg"; SKU="FAR-1KG"; Cost=4.70; Sale=7.49; Minimum=10; Entry=45; Exit=20; EntryAge=66; ExitAge=25 },
    @{ Key="molho"; Name="Molho de tomate 300 g"; SKU="MOL-300"; Cost=2.40; Sale=4.29; Minimum=5; Entry=30; Exit=30; EntryAge=62; ExitAge=7 },
    @{ Key="higienico"; Name="Papel higienico com 4 rolos"; SKU="HIG-4RL"; Cost=7.90; Sale=12.90; Minimum=8; Entry=36; Exit=36; EntryAge=58; ExitAge=1 },
    @{ Key="inativo"; Name="Refrigerante descontinuado 2 L"; SKU="REF-2L"; Cost=5.20; Sale=8.99; Minimum=0; Entry=0; Exit=0; EntryAge=0; ExitAge=0 }
)

$products = @{}
$movementRecords = [Collections.Generic.List[object]]::new()
foreach ($definition in $productDefinitions) {
    $product = Invoke-JsonRequest POST "/api/products" @{
        name = $definition.Name
        sku = $definition.SKU
        description = "Item preparado para a demonstracao academica do SGF."
        costPrice = $definition.Cost
        salePrice = $definition.Sale
        minimumStock = $definition.Minimum
    } $token
    $products[$definition.Key] = $product
    if ($definition.Entry -gt 0) {
        $entry = Invoke-JsonRequest POST "/api/inventory/entries" @{
            productId = $product.id; quantity = $definition.Entry; notes = "Estoque inicial da demonstracao"
        } $token
        $movementRecords.Add([pscustomobject]@{ Id=$entry.id; Age=$definition.EntryAge })
    }
    if ($definition.Exit -gt 0) {
        $exit = Invoke-JsonRequest POST "/api/inventory/exits" @{
            productId = $product.id; quantity = $definition.Exit; notes = "Saida operacional demonstrativa"
        } $token
        $movementRecords.Add([pscustomobject]@{ Id=$exit.id; Age=$definition.ExitAge })
    }
}

# Movimentos adicionais deixam o historico e os rankings mais representativos
# sem alterar o escopo: continuam sendo apenas entradas e saidas reais da API.
$additionalMovements = @(
    @{ Product="arroz"; Type="Entry"; Quantity=30; Age=51 },
    @{ Product="arroz"; Type="Exit"; Quantity=14; Age=35 },
    @{ Product="feijao"; Type="Entry"; Quantity=20; Age=19 },
    @{ Product="feijao"; Type="Exit"; Quantity=15; Age=10 },
    @{ Product="leite"; Type="Entry"; Quantity=30; Age=50 },
    @{ Product="leite"; Type="Exit"; Quantity=22; Age=29 },
    @{ Product="acucar"; Type="Entry"; Quantity=25; Age=26 },
    @{ Product="acucar"; Type="Exit"; Quantity=20; Age=15 },
    @{ Product="oleo"; Type="Entry"; Quantity=30; Age=33 },
    @{ Product="oleo"; Type="Exit"; Quantity=28; Age=17 },
    @{ Product="macarrao"; Type="Entry"; Quantity=20; Age=21 },
    @{ Product="macarrao"; Type="Exit"; Quantity=22; Age=5 },
    @{ Product="detergente"; Type="Entry"; Quantity=20; Age=18 },
    @{ Product="detergente"; Type="Exit"; Quantity=18; Age=4 },
    @{ Product="agua"; Type="Entry"; Quantity=30; Age=36 },
    @{ Product="farinha"; Type="Exit"; Quantity=5; Age=11 }
)

foreach ($definition in $additionalMovements) {
    $path = if ($definition.Type -eq "Entry") { "/api/inventory/entries" } else { "/api/inventory/exits" }
    $notes = if ($definition.Type -eq "Entry") { "Reposicao de estoque para demonstracao" } else { "Saida operacional demonstrativa" }
    $movement = Invoke-JsonRequest POST $path @{
        productId = $products[$definition.Product].id; quantity = $definition.Quantity; notes = $notes
    } $token
    $movementRecords.Add([pscustomobject]@{ Id=$movement.id; Age=$definition.Age })
}

Invoke-JsonRequest PATCH "/api/products/$($products.inativo.id)/status" @{ isActive = $false } $token | Out-Null

$today = [DateTime]::Today
$financeDefinitions = @(
    @{ Key="income-current-01"; Type="Income"; Description="Recebimento de vendas da semana"; Category="Vendas"; Amount=2400; Due=2; Paid=$true; Age=2 },
    @{ Key="income-current-02"; Type="Income"; Description="Pedidos de entrega confirmados"; Category="Vendas"; Amount=1850; Due=5; Paid=$true; Age=5 },
    @{ Key="income-current-03"; Type="Income"; Description="Encomenda corporativa"; Category="Vendas"; Amount=3200; Due=9; Paid=$true; Age=9 },
    @{ Key="income-current-04"; Type="Income"; Description="Recebimento de vendas no balcao"; Category="Vendas"; Amount=1640; Due=14; Paid=$true; Age=14 },
    @{ Key="income-current-05"; Type="Income"; Description="Servico de entrega"; Category="Servicos"; Amount=1050; Due=20; Paid=$true; Age=20 },
    @{ Key="income-current-06"; Type="Income"; Description="Vendas online recebidas"; Category="Vendas"; Amount=2100; Due=27; Paid=$true; Age=27 },
    @{ Key="expense-current-01"; Type="Expense"; Description="Reposicao de mercadorias"; Category="Compras"; Amount=1800; Due=3; Paid=$true; Age=3 },
    @{ Key="expense-current-02"; Type="Expense"; Description="Energia e internet"; Category="Operacional"; Amount=620; Due=7; Paid=$true; Age=7 },
    @{ Key="expense-current-03"; Type="Expense"; Description="Aluguel do ponto comercial"; Category="Estrutura"; Amount=1400; Due=11; Paid=$true; Age=11 },
    @{ Key="expense-current-04"; Type="Expense"; Description="Campanha de divulgacao local"; Category="Marketing"; Amount=480; Due=17; Paid=$true; Age=17 },
    @{ Key="expense-current-05"; Type="Expense"; Description="Manutencao preventiva"; Category="Manutencao"; Amount=750; Due=24; Paid=$true; Age=24 },
    @{ Key="expense-current-06"; Type="Expense"; Description="Fretes e deslocamentos"; Category="Logistica"; Amount=390; Due=28; Paid=$true; Age=28 },
    @{ Key="income-previous-01"; Type="Income"; Description="Vendas do periodo anterior"; Category="Vendas"; Amount=2800; Due=-33; Paid=$true; Age=33 },
    @{ Key="income-previous-02"; Type="Income"; Description="Pedidos de entrega do periodo anterior"; Category="Vendas"; Amount=1750; Due=-38; Paid=$true; Age=38 },
    @{ Key="income-previous-03"; Type="Income"; Description="Encomenda recorrente"; Category="Vendas"; Amount=2500; Due=-43; Paid=$true; Age=43 },
    @{ Key="income-previous-04"; Type="Income"; Description="Recebimento de vendas no balcao"; Category="Vendas"; Amount=1900; Due=-49; Paid=$true; Age=49 },
    @{ Key="income-previous-05"; Type="Income"; Description="Servico de entrega anterior"; Category="Servicos"; Amount=1100; Due=-56; Paid=$true; Age=56 },
    @{ Key="income-previous-06"; Type="Income"; Description="Vendas online anteriores"; Category="Vendas"; Amount=1650; Due=-59; Paid=$true; Age=59 },
    @{ Key="expense-previous-01"; Type="Expense"; Description="Compras do periodo anterior"; Category="Compras"; Amount=1550; Due=-35; Paid=$true; Age=35 },
    @{ Key="expense-previous-02"; Type="Expense"; Description="Aluguel do periodo anterior"; Category="Estrutura"; Amount=1400; Due=-42; Paid=$true; Age=42 },
    @{ Key="expense-previous-03"; Type="Expense"; Description="Custos operacionais anteriores"; Category="Operacional"; Amount=580; Due=-50; Paid=$true; Age=50 },
    @{ Key="expense-previous-04"; Type="Expense"; Description="Fretes anteriores"; Category="Logistica"; Amount=360; Due=-57; Paid=$true; Age=57 },
    @{ Key="expense-previous-05"; Type="Expense"; Description="Material de apoio"; Category="Operacional"; Amount=410; Due=-60; Paid=$true; Age=60 },
    @{ Key="income-older-01"; Type="Income"; Description="Vendas de dois meses atras"; Category="Vendas"; Amount=2200; Due=-67; Paid=$true; Age=67 },
    @{ Key="income-older-02"; Type="Income"; Description="Encomenda de dois meses atras"; Category="Vendas"; Amount=1900; Due=-76; Paid=$true; Age=76 },
    @{ Key="income-older-03"; Type="Income"; Description="Servico anterior"; Category="Servicos"; Amount=900; Due=-85; Paid=$true; Age=85 },
    @{ Key="expense-older-01"; Type="Expense"; Description="Compras de dois meses atras"; Category="Compras"; Amount=1300; Due=-69; Paid=$true; Age=69 },
    @{ Key="expense-older-02"; Type="Expense"; Description="Despesas operacionais anteriores"; Category="Operacional"; Amount=520; Due=-79; Paid=$true; Age=79 },
    @{ Key="income-future"; Type="Income"; Description="Faturamento a receber"; Category="Vendas"; Amount=3600; Due=12; Paid=$false; Age=0 },
    @{ Key="income-future-02"; Type="Income"; Description="Pedido corporativo a receber"; Category="Vendas"; Amount=2100; Due=18; Paid=$false; Age=0 },
    @{ Key="expense-future"; Type="Expense"; Description="Aluguel do proximo periodo"; Category="Estrutura"; Amount=2200; Due=8; Paid=$false; Age=0 },
    @{ Key="expense-future-02"; Type="Expense"; Description="Compra programada"; Category="Compras"; Amount=980; Due=16; Paid=$false; Age=0 },
    @{ Key="income-overdue"; Type="Income"; Description="Cliente em atraso"; Category="Vendas"; Amount=1750; Due=-9; Paid=$false; Age=0 },
    @{ Key="expense-overdue"; Type="Expense"; Description="Fornecedor em atraso"; Category="Compras"; Amount=1300; Due=-6; Paid=$false; Age=0 }
)

$finance = @{}
foreach ($definition in $financeDefinitions) {
    # Um lancamento ja pago recebe vencimento no mesmo dia historico do pagamento.
    # Pendencias continuam usando o vencimento planejado para a demonstracao.
    $dueDate = if ($definition.Paid) { $today.AddDays(-$definition.Age) } else { $today.AddDays($definition.Due) }
    $entry = Invoke-JsonRequest POST "/api/finance" @{
        type = $definition.Type
        description = $definition.Description
        category = $definition.Category
        amount = $definition.Amount
        dueDate = $dueDate.ToString("yyyy-MM-dd")
        notes = "Cenario controlado para apresentacao do TCC."
    } $token
    if ($definition.Paid) {
        $entry = Invoke-JsonRequest PATCH "/api/finance/$($entry.id)/pay" $null $token
    }
    $finance[$definition.Key] = @{ Id=$entry.id; Age=$definition.Age; Paid=$definition.Paid }
}

# Datas historicas nao fazem parte dos contratos publicos. O ajuste abaixo e
# restrito aos IDs recem-criados e somente ao PostgreSQL local da demonstracao.
$sqlParts = [Collections.Generic.List[string]]::new()
foreach ($item in $finance.Values) {
    if ($item.Age -gt 0) {
        $sqlParts.Add("UPDATE `"FinancialEntries`" SET `"PaidAt`" = NOW() - INTERVAL '$($item.Age) days', `"CreatedAt`" = NOW() - INTERVAL '$($item.Age) days', `"UpdatedAt`" = NOW() - INTERVAL '$($item.Age) days' WHERE `"Id`" = '$($item.Id)';")
    }
}
foreach ($movement in $movementRecords) {
    $sqlParts.Add("UPDATE `"InventoryMovements`" SET `"CreatedAt`" = NOW() - INTERVAL '$($movement.Age) days' WHERE `"Id`" = '$($movement.Id)';")
}
$staleProductId = $products.sabonete.id
$sqlParts.Add("UPDATE `"Products`" SET `"CreatedAt`" = NOW() - INTERVAL '75 days', `"UpdatedAt`" = NOW() - INTERVAL '45 days' WHERE `"Id`" = '$staleProductId';")

& docker exec $ContainerName psql -v ON_ERROR_STOP=1 -U $DatabaseUser -d $Database -c ($sqlParts -join " ")
if ($LASTEXITCODE -ne 0) { throw "Falha ao ajustar datas historicas no PostgreSQL local." }

Write-Host ""
Write-Host "Demonstracao criada com sucesso." -ForegroundColor Green
Write-Host "Empresa: $company"
Write-Host "Usuario: $email"
Write-Host "Senha: a informada nesta execucao (nao foi salva pelo script)."
Write-Host "Dados criados: 16 produtos, $($movementRecords.Count) movimentacoes e $($finance.Count) lancamentos financeiros."
Write-Host "Abra http://localhost:5173/login e use Ultimos 30 dias ou Ultimos 90 dias no dashboard."

$password = $null
Remove-Item Env:SGF_DEMO_PASSWORD -ErrorAction SilentlyContinue
