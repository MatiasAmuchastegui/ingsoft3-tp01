# Verifica lo que el TP2 pide demostrar: persistencia del volumen, aislamiento de red,
# ruteo de la SPA y que EXPOSE no publica nada.
#
# Requiere el sistema levantado:  docker compose up -d
#
#   .\scripts\probar-compose.ps1
#
# OJO: este script hace "docker compose down -v", asi que BORRA los datos de la base.
# Es a proposito: probar la persistencia exige destruirla.

param([string]$BaseUrl = 'http://localhost:8080')

$ErrorActionPreference = 'Continue'

# La raiz del repo se deriva de donde esta el script (scripts/), para que funcione
# en cualquier maquina que clone el repositorio.
$raiz = Split-Path -Parent $PSScriptRoot
$base = $BaseUrl
$fallas = 0
Write-Host "Repo:     $raiz"
Write-Host "Probando: $base"

function Afirmar($descripcion, $condicion, $detalle) {
    if ($condicion) { Write-Host "  OK   $descripcion" }
    else { Write-Host "  FALLA $descripcion  --> $detalle"; $script:fallas++ }
}

function TokenAdmin {
    $r = Invoke-RestMethod -Uri "$base/api/auth/login" -Method POST -UseBasicParsing `
        -ContentType 'application/json' `
        -Body (@{email='admin@joyeria.local';password='Admin123!'} | ConvertTo-Json)
    return $r.token
}

function ContarProductos($token) {
    $p = Invoke-RestMethod -Uri "$base/api/productos?incluirInactivos=true" `
        -Headers @{Authorization="Bearer $token"} -UseBasicParsing
    return @($p).Count
}

function EsperarSano {
    # El healthcheck del frontend tarda unos segundos tras un up.
    for ($i = 0; $i -lt 60; $i++) {
        try {
            $r = Invoke-WebRequest -Uri "$base/health" -UseBasicParsing -TimeoutSec 3
            if ($r.StatusCode -eq 200) { return $true }
        } catch { }
        Start-Sleep -Seconds 2
    }
    return $false
}

Write-Host "`n=== Ruteo de la SPA y proxy ==="
$raizHtml = Invoke-WebRequest -Uri "$base/" -UseBasicParsing
Afirmar "La raiz sirve el index.html" ($raizHtml.Content -match 'id="root"') "no tiene #root"

# /stock es una ruta de react-router: NO existe como archivo. Sin try_files daria 404.
$ruta = Invoke-WebRequest -Uri "$base/stock" -UseBasicParsing
Afirmar "Una ruta del cliente (/stock) devuelve el index.html" `
    ($ruta.StatusCode -eq 200 -and $ruta.Content -match 'id="root"') "status $($ruta.StatusCode)"

$salud = Invoke-WebRequest -Uri "$base/health" -UseBasicParsing
Afirmar "nginx proxea /health al backend" ($salud.StatusCode -eq 200) "status $($salud.StatusCode)"

Write-Host "`n=== EXPOSE no publica: backend y base NO alcanzables desde el host ==="
$backendDirecto = Test-NetConnection -ComputerName localhost -Port 5080 -InformationLevel Quiet -WarningAction SilentlyContinue
Afirmar "El backend NO responde en el host (5080)" (-not $backendDirecto) "responde: hay un ports: de mas"

$dbDirecta = Test-NetConnection -ComputerName localhost -Port 5432 -InformationLevel Quiet -WarningAction SilentlyContinue
Afirmar "La base NO responde en el host (5432)" (-not $dbDirecta) "responde: hay un ports: de mas"

Write-Host "`n=== Aislamiento de red: el frontend no llega a la base ==="
# El frontend esta solo en la red 'publica'; la base solo en 'interna'.
docker compose --project-directory $raiz exec -T frontend sh -c "nslookup db >/dev/null 2>&1" | Out-Null
$frontendVeDb = ($LASTEXITCODE -eq 0)
Afirmar "El frontend NO resuelve el nombre 'db'" (-not $frontendVeDb) "lo resuelve: estan en la misma red"

docker compose --project-directory $raiz exec -T backend sh -c "curl -fsS http://localhost:8080/health >/dev/null" | Out-Null
Afirmar "El backend se ve sano desde adentro" ($LASTEXITCODE -eq 0) "curl fallo"

Write-Host "`n=== Persistencia: down / up conserva los datos ==="
$token = TokenAdmin
$antes = ContarProductos $token
# Se crea un producto nuevo para tener un dato que NO viene del seed.
$sku = "PERSIST-01"
try {
    Invoke-RestMethod -Uri "$base/api/productos" -Method POST -UseBasicParsing `
        -Headers @{Authorization="Bearer $token"} -ContentType 'application/json' `
        -Body (@{sku=$sku;nombre='Producto de prueba de persistencia';categoriaId=1;
                 precioBase=1234;cantidadMinimaMayorista=0;
                 porcentajeDescuentoMayorista=0;umbralStockBajo=1} | ConvertTo-Json) | Out-Null
} catch { }
$conNuevo = ContarProductos $token
Afirmar "Se creo el producto de prueba" ($conNuevo -eq $antes + 1) "antes=$antes ahora=$conNuevo"

Write-Host "  ... docker compose down (SIN -v)"
docker compose --project-directory $raiz down 2>&1 | Out-Null
Write-Host "  ... docker compose up -d"
docker compose --project-directory $raiz up -d 2>&1 | Out-Null
Afirmar "El sistema volvio a estar sano" (EsperarSano) "no respondio /health"

$token = TokenAdmin
$despuesDown = ContarProductos $token
Afirmar "Los datos SOBREVIVEN a down/up" ($despuesDown -eq $conNuevo) `
    "antes del down=$conNuevo despues=$despuesDown"

$productos = Invoke-RestMethod -Uri "$base/api/productos?incluirInactivos=true" `
    -Headers @{Authorization="Bearer $token"} -UseBasicParsing
Afirmar "El producto de prueba sigue ahi" (@($productos | Where-Object { $_.sku -eq $sku }).Count -eq 1) `
    "no esta"

Write-Host "`n=== docker compose down -v BORRA los datos ==="
Write-Host "  ... docker compose down -v"
docker compose --project-directory $raiz down -v 2>&1 | Out-Null

$volumen = docker volume ls --format "{{.Name}}" | Select-String -Pattern 'joyeria_db-data'
Afirmar "El volumen fue eliminado" ($null -eq $volumen) "el volumen todavia existe"

Write-Host "  ... docker compose up -d"
docker compose --project-directory $raiz up -d 2>&1 | Out-Null
Afirmar "El sistema volvio a estar sano" (EsperarSano) "no respondio /health"

# Se compara contra la cantidad fija que carga el seed, NO contra lo que habia antes:
# si la base traia datos de una corrida anterior, $antes no es el estado limpio.
$productosDelSeed = 6
$token = TokenAdmin
$despuesDownV = ContarProductos $token
Afirmar "Los datos se PERDIERON y el seed volvio a correr desde cero" `
    ($despuesDownV -eq $productosDelSeed) `
    "esperaba $productosDelSeed (lo que carga el seed) y hay $despuesDownV"

$productos = Invoke-RestMethod -Uri "$base/api/productos?incluirInactivos=true" `
    -Headers @{Authorization="Bearer $token"} -UseBasicParsing
Afirmar "El producto de prueba desaparecio" (@($productos | Where-Object { $_.sku -eq $sku }).Count -eq 0) `
    "todavia esta"

Write-Host ""
if ($fallas -eq 0) { Write-Host "TODAS LAS VERIFICACIONES PASARON" }
else { Write-Host "$fallas VERIFICACION(ES) FALLARON" }
exit $fallas
