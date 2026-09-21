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
    @{ Key="arroz"; Name="Arroz tipo 1 5 kg"; SKU="ARR-5KG"; Cost=24.90; Sale=32.90; Minimum=10; Entry=30; Exit=26 },
    @{ Key="feijao"; Name="Feijao carioca 1 kg"; SKU="FEI-1KG"; Cost=6.80; Sale=9.90; Minimum=8; Entry=20; Exit=20 },
    @{ Key="cafe"; Name="Cafe torrado 500 g"; SKU="CAF-500"; Cost=14.50; Sale=21.90; Minimum=10; Entry=30; Exit=12 },
    @{ Key="leite"; Name="Leite integral 1 L"; SKU="LEI-1L"; Cost=4.20; Sale=6.49; Minimum=12; Entry=24; Exit=17 },
    @{ Key="acucar"; Name="Acucar refinado 1 kg"; SKU="ACU-1KG"; Cost=3.90; Sale=5.99; Minimum=10; Entry=40; Exit=15 },
    @{ Key="oleo"; Name="Oleo de soja 900 ml"; SKU="OLE-900"; Cost=6.70; Sale=9.49; Minimum=8; Entry=24; Exit=10 },
    @{ Key="macarrao"; Name="Macarrao espaguete 500 g"; SKU="MAC-500"; Cost=3.20; Sale=5.49; Minimum=12; Entry=50; Exit=20 },
    @{ Key="biscoito"; Name="Biscoito integral 200 g"; SKU="BIS-200"; Cost=3.80; Sale=6.90; Minimum=6; Entry=18; Exit=12 },
    @{ Key="detergente"; Name="Detergente neutro 500 ml"; SKU="DET-500"; Cost=1.90; Sale=3.49; Minimum=5; Entry=20; Exit=4 },
    @{ Key="sabonete"; Name="Sabonete neutro 90 g"; SKU="SAB-90"; Cost=1.80; Sale=3.50; Minimum=6; Entry=12; Exit=0 },
    @{ Key="inativo"; Name="Refrigerante descontinuado 2 L"; SKU="REF-2L"; Cost=5.20; Sale=8.99; Minimum=0; Entry=0; Exit=0 }
)

$products = @{}
$movementIds = @()
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
        $movementIds += $entry.id
    }
    if ($definition.Exit -gt 0) {
        $exit = Invoke-JsonRequest POST "/api/inventory/exits" @{
            productId = $product.id; quantity = $definition.Exit; notes = "Saida operacional demonstrativa"
        } $token
        $movementIds += $exit.id
    }
}
Invoke-JsonRequest PATCH "/api/products/$($products.inativo.id)/status" @{ isActive = $false } $token | Out-Null

$today = [DateTime]::Today
$financeDefinitions = @(
    @{ Key="income-current-1"; Type="Income"; Description="Vendas no varejo"; Category="Vendas"; Amount=8500; Due=5; Paid=$true; Age=2 },
    @{ Key="income-current-2"; Type="Income"; Description="Encomendas corporativas"; Category="Vendas"; Amount=4000; Due=12; Paid=$true; Age=10 },
    @{ Key="expense-current-1"; Type="Expense"; Description="Reposicao de mercadorias"; Category="Compras"; Amount=4200; Due=8; Paid=$true; Age=5 },
    @{ Key="expense-current-2"; Type="Expense"; Description="Energia e internet"; Category="Operacional"; Amount=980; Due=10; Paid=$true; Age=13 },
    @{ Key="expense-current-3"; Type="Expense"; Description="Manutencao preventiva"; Category="Manutencao"; Amount=650; Due=15; Paid=$true; Age=21 },
    @{ Key="income-previous"; Type="Income"; Description="Vendas do periodo anterior"; Category="Vendas"; Amount=9800; Due=-35; Paid=$true; Age=35 },
    @{ Key="expense-previous-1"; Type="Expense"; Description="Compras do periodo anterior"; Category="Compras"; Amount=3800; Due=-38; Paid=$true; Age=38 },
    @{ Key="expense-previous-2"; Type="Expense"; Description="Custos operacionais anteriores"; Category="Operacional"; Amount=900; Due=-45; Paid=$true; Age=45 },
    @{ Key="income-future"; Type="Income"; Description="Faturamento a receber"; Category="Vendas"; Amount=3500; Due=12; Paid=$false; Age=0 },
    @{ Key="expense-future"; Type="Expense"; Description="Aluguel do proximo periodo"; Category="Estrutura"; Amount=2200; Due=8; Paid=$false; Age=0 },
    @{ Key="income-overdue"; Type="Income"; Description="Cliente em atraso"; Category="Vendas"; Amount=1800; Due=-9; Paid=$false; Age=0 },
    @{ Key="expense-overdue"; Type="Expense"; Description="Fornecedor em atraso"; Category="Compras"; Amount=1250; Due=-6; Paid=$false; Age=0 }
)

$finance = @{}
foreach ($definition in $financeDefinitions) {
    $entry = Invoke-JsonRequest POST "/api/finance" @{
        type = $definition.Type
        description = $definition.Description
        category = $definition.Category
        amount = $definition.Amount
        dueDate = $today.AddDays($definition.Due).ToString("yyyy-MM-dd")
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
$staleProductId = $products.sabonete.id
$sqlParts.Add("UPDATE `"Products`" SET `"CreatedAt`" = NOW() - INTERVAL '60 days', `"UpdatedAt`" = NOW() - INTERVAL '45 days' WHERE `"Id`" = '$staleProductId';")
$sqlParts.Add("UPDATE `"InventoryMovements`" SET `"CreatedAt`" = NOW() - INTERVAL '45 days' WHERE `"ProductId`" = '$staleProductId';")

& docker exec $ContainerName psql -v ON_ERROR_STOP=1 -U $DatabaseUser -d $Database -c ($sqlParts -join " ")
if ($LASTEXITCODE -ne 0) { throw "Falha ao ajustar datas historicas no PostgreSQL local." }

Write-Host ""
Write-Host "Demonstracao criada com sucesso." -ForegroundColor Green
Write-Host "Empresa: $company"
Write-Host "Usuario: $email"
Write-Host "Senha: a informada nesta execucao (nao foi salva pelo script)."
Write-Host "Abra http://localhost:5173/login e use Ultimos 30 dias no dashboard."

$password = $null
Remove-Item Env:SGF_DEMO_PASSWORD -ErrorAction SilentlyContinue
