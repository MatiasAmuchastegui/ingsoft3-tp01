# Verificación end-to-end de las 5 reglas de negocio contra la API corriendo.
#   .\probar-reglas.ps1                          -> contra el backend local (dotnet run)
#   .\probar-reglas.ps1 http://localhost:8080    -> contra el sistema en compose, via nginx
param([string]$BaseUrl = 'http://localhost:5080')

$ErrorActionPreference = 'Stop'
$base = $BaseUrl
$fallas = 0
Write-Host "Probando contra: $base"

function Afirmar($descripcion, $condicion, $detalle) {
    if ($condicion) {
        Write-Host "  OK   $descripcion"
    } else {
        Write-Host "  FALLA $descripcion  --> $detalle"
        $script:fallas++
    }
}

# Invoca la API y devuelve @{ Status; Body } tanto en exito como en error HTTP.
# PowerShell 5.1 no tiene -SkipHttpErrorCheck: los 4xx/5xx llegan como excepcion
# y hay que leer el cuerpo desde el stream de la respuesta.
function Llamar($metodo, $ruta, $token, $cuerpo) {
    $headers = @{}
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    $parametros = @{
        Uri = "$base$ruta"; Method = $metodo; Headers = $headers
        UseBasicParsing = $true
    }
    if ($null -ne $cuerpo) {
        # El cuerpo va como bytes UTF-8 explicitos. Si se manda como string, PowerShell 5.1
        # no declara la codificacion y los acentos llegan corruptos: el JSON no parsea y la
        # API responde 400. Se nota recien cuando algun texto tiene tildes ("circón").
        $parametros['Body'] = [System.Text.Encoding]::UTF8.GetBytes(($cuerpo | ConvertTo-Json -Compress))
        $parametros['ContentType'] = 'application/json; charset=utf-8'
    }

    $status = 0
    $contenido = $null
    try {
        $r = Invoke-WebRequest @parametros
        $status = [int]$r.StatusCode
        $contenido = $r.Content
    } catch {
        $respuesta = $_.Exception.Response
        if ($null -eq $respuesta) { throw }
        $status = [int]$respuesta.StatusCode
        $stream = $respuesta.GetResponseStream()
        $lector = New-Object System.IO.StreamReader($stream)
        $contenido = $lector.ReadToEnd()
        $lector.Close()
    }

    $parsed = $null
    if ($contenido) { try { $parsed = $contenido | ConvertFrom-Json } catch { } }
    return @{ Status = $status; Body = $parsed }
}

Write-Host "`n=== Login ==="
$loginAdmin = Llamar POST '/api/auth/login' $null @{ email = 'admin@joyeria.local'; password = 'Admin123!' }
Afirmar "Admin puede loguearse" ($loginAdmin.Status -eq 200) "status $($loginAdmin.Status)"
$tokenAdmin = $loginAdmin.Body.token
Afirmar "Admin no tiene local asignado" ($null -eq $loginAdmin.Body.usuario.localId) "localId=$($loginAdmin.Body.usuario.localId)"

$loginVend = Llamar POST '/api/auth/login' $null @{ email = 'vendedor1@joyeria.local'; password = 'Vendedor123!' }
Afirmar "Vendedor puede loguearse" ($loginVend.Status -eq 200) "status $($loginVend.Status)"
$tokenVend = $loginVend.Body.token
$localVend = $loginVend.Body.usuario.localId
Afirmar "Vendedor tiene local asignado" ($null -ne $localVend) "sin localId"

$malPass = Llamar POST '/api/auth/login' $null @{ email = 'admin@joyeria.local'; password = 'incorrecta' }
Afirmar "Password incorrecta devuelve 401" ($malPass.Status -eq 401) "status $($malPass.Status)"

$sinToken = Llamar GET '/api/stock' $null $null
Afirmar "Endpoint protegido sin token devuelve 401" ($sinToken.Status -eq 401) "status $($sinToken.Status)"

Write-Host "`n=== Regla 1: el SKU lo genera el sistema y nunca se repite ==="
# El codigo de linea es unico por corrida (HHmmss), asi el script se puede correr varias
# veces sobre la misma base y cada corrida estrena su propia serie de numeracion.
$corrida = Get-Date -Format 'HHmmss'
$cats = (Llamar GET '/api/categorias' $tokenAdmin $null).Body
$cat = $cats[0]
$catId = $cat.id
$prefijoEsperado = "$($cat.prefijoSku)$corrida"

$nuevo = @{ codigoLinea = $corrida; nombre = "Producto de prueba $corrida"; categoriaId = $catId
            precioBase = 10000; umbralStockBajo = 2 }

# La vista previa tiene que anticipar exactamente el codigo que despues se guarda.
$previa = Llamar GET "/api/productos/proximo-sku?categoriaId=$catId&codigoLinea=$corrida" $tokenAdmin $null
Afirmar "La vista previa del SKU responde" ($previa.Status -eq 200) "status $($previa.Status)"

$creado = Llamar POST '/api/productos' $tokenAdmin $nuevo
Afirmar "Crear producto nuevo devuelve 201" ($creado.Status -eq 201) "status $($creado.Status)"
$productoTestId = $creado.Body.id
$skuPrueba = $creado.Body.sku

Afirmar "El sistema asigno un SKU" (-not [string]::IsNullOrWhiteSpace($skuPrueba)) "vino vacio"
Afirmar "Arranca con el prefijo de la categoria mas el codigo de linea" `
    ($skuPrueba -like "$prefijoEsperado-*") "sku=$skuPrueba esperaba $prefijoEsperado-…"
Afirmar "Numera desde 0001 con 4 digitos" ($skuPrueba -eq "$prefijoEsperado-0001") "sku=$skuPrueba"
Afirmar "La vista previa habia anticipado ese mismo codigo" ($previa.Body.sku -eq $skuPrueba) `
    "previa=$($previa.Body.sku) real=$skuPrueba"

# Crear otro igual NO choca: el sistema le da el numero siguiente.
$segundo = Llamar POST '/api/productos' $tokenAdmin $nuevo
Afirmar "Un segundo producto de la misma serie se crea sin conflicto" ($segundo.Status -eq 201) `
    "status $($segundo.Status)"
Afirmar "Y recibe el numero siguiente" ($segundo.Body.sku -eq "$prefijoEsperado-0002") `
    "sku=$($segundo.Body.sku)"
Afirmar "Los dos SKU son distintos" ($segundo.Body.sku -ne $skuPrueba) "se repitieron"

# El SKU que mande el cliente se ignora: el codigo lo pone el sistema.
$conSkuInventado = $nuevo.Clone(); $conSkuInventado.sku = 'INVENTADO-9999'
$tercero = Llamar POST '/api/productos' $tokenAdmin $conSkuInventado
Afirmar "Un SKU mandado por el cliente se ignora" `
    ($tercero.Status -eq 201 -and $tercero.Body.sku -eq "$prefijoEsperado-0003") `
    "sku=$($tercero.Body.sku)"

# El SKU es inmutable: no cambia al editar. Lo que se edita para verificar es el nombre.
$edicion = @{ nombre = "Renombrado $corrida"; categoriaId = $catId; codigoLinea = $corrida
            precioBase = 10000; umbralStockBajo = 2 }
$editado = Llamar PUT "/api/productos/$productoTestId" $tokenAdmin $edicion
Afirmar "Editar el producto funciona" ($editado.Status -eq 200) "status $($editado.Status)"
Afirmar "El cambio se guardo" ($editado.Body.nombre -eq "Renombrado $corrida") `
    "nombre=$($editado.Body.nombre)"
Afirmar "Y el SKU NO cambia" ($editado.Body.sku -eq $skuPrueba) "sku=$($editado.Body.sku)"

# Cambiar de categoria haria que el codigo mienta sobre su categoria.
$otraCat = $cats | Where-Object { $_.id -ne $catId } | Select-Object -First 1
if ($otraCat) {
    $cambioCat = $edicion.Clone(); $cambioCat.categoriaId = $otraCat.id
    $rechazo = Llamar PUT "/api/productos/$productoTestId" $tokenAdmin $cambioCat
    Afirmar "No se puede cambiar la categoria de un producto ya creado" ($rechazo.Status -eq 409) `
        "status $($rechazo.Status)"
}

Write-Host "`n=== El prefijo de la categoria es unico y no se puede reutilizar ==="
$prefijoRepetido = Llamar POST '/api/categorias' $tokenAdmin @{
    nombre = "Duplicada $corrida"; prefijoSku = $cat.prefijoSku }
Afirmar "Otra categoria no puede usar un prefijo ya tomado" ($prefijoRepetido.Status -eq 409) `
    "status $($prefijoRepetido.Status)"

$prefijoInvalido = Llamar POST '/api/categorias' $tokenAdmin @{
    nombre = "Invalida $corrida"; prefijoSku = 'A B-!' }
Afirmar "El prefijo solo acepta letras y numeros" ($prefijoInvalido.Status -eq 409) `
    "status $($prefijoInvalido.Status)"

$cambiarPrefijoConProductos = Llamar PUT "/api/categorias/$catId" $tokenAdmin @{
    nombre = $cat.nombre; prefijoSku = "XX$corrida".Substring(0,6) }
Afirmar "No se puede cambiar el prefijo de una categoria con productos" `
    ($cambiarPrefijoConProductos.Status -eq 409) "status $($cambiarPrefijoConProductos.Status)"

Write-Host "`n=== Un producto nuevo aparece en stock, en los 3 locales, con cantidad 0 ==="
# Regresion: antes la consulta de stock partia de las filas de la tabla stocks, que sólo
# nacen con el primer movimiento. Un producto recien creado no aparecia nunca, y como los
# movimientos se registran desde una fila de esa pantalla, quedaba imposible de cargar.
$localesTodos = @((Llamar GET '/api/locales' $tokenAdmin $null).Body)
$stockNuevo = @((Llamar GET "/api/stock?busqueda=$skuPrueba" $tokenAdmin $null).Body)
Afirmar "El producto recien creado aparece una vez por local" `
    ($stockNuevo.Count -eq $localesTodos.Count) `
    "esperaba $($localesTodos.Count) filas y hay $($stockNuevo.Count)"
Afirmar "Y arranca con cantidad 0 en todos" `
    (@($stockNuevo | Where-Object { $_.cantidad -ne 0 }).Count -eq 0) `
    "alguna fila no arranca en 0"

# Y se le puede dar entrada en el local elegido, que era lo imposible antes.
$localElegido = $stockNuevo[0].localId
$primeraEntrada = Llamar POST '/api/movimientos' $tokenAdmin @{
    tipo = 'Entrada'; productoId = $productoTestId; localId = $localElegido
    cantidad = 7; observacion = 'carga inicial' }
Afirmar "Se le puede dar la primera entrada" ($primeraEntrada.Status -eq 200) `
    "status $($primeraEntrada.Status)"
Afirmar "La cantidad queda en 7" ($primeraEntrada.Body.cantidadResultante -eq 7) `
    "resultante=$($primeraEntrada.Body.cantidadResultante)"

$stockTrasEntrada = @((Llamar GET "/api/stock?busqueda=$skuPrueba" $tokenAdmin $null).Body)
$enEseLocal = $stockTrasEntrada | Where-Object { $_.localId -eq $localElegido }
Afirmar "El local elegido refleja las 7 unidades" ($enEseLocal.cantidad -eq 7) `
    "cantidad=$($enEseLocal.cantidad)"
Afirmar "Los otros locales siguen en 0" `
    (@($stockTrasEntrada | Where-Object { $_.localId -ne $localElegido -and $_.cantidad -ne 0 }).Count -eq 0) `
    "algun otro local cambio"

Write-Host "`n=== Regla 3: no eliminar categoria con productos ==="
$catConProductos = $cats | Where-Object { $_.cantidadProductos -gt 0 } | Select-Object -First 1
$borrarOcupada = Llamar DELETE "/api/categorias/$($catConProductos.id)" $tokenAdmin $null
Afirmar "Eliminar categoria con productos devuelve 409" ($borrarOcupada.Status -eq 409) "status $($borrarOcupada.Status)"

$catVacia = Llamar POST '/api/categorias' $tokenAdmin @{ nombre = "Categoria vacia $corrida"; prefijoSku = "TV" + $corrida.Substring(4) }
Afirmar "Crear categoria devuelve 201" ($catVacia.Status -eq 201) "status $($catVacia.Status)"
$borrarVacia = Llamar DELETE "/api/categorias/$($catVacia.Body.id)" $tokenAdmin $null
Afirmar "Eliminar categoria sin productos devuelve 204" ($borrarVacia.Status -eq 204) "status $($borrarVacia.Status)"

Write-Host "`n=== Regla 2: el stock nunca queda negativo ==="
$stockVend = (Llamar GET '/api/stock' $tokenVend $null).Body
$item = $stockVend | Where-Object { $_.cantidad -gt 0 } | Select-Object -First 1
Afirmar "Vendedor ve stock de su local" ($null -ne $item) "lista vacia"

$exceso = Llamar POST '/api/movimientos' $tokenVend @{
    tipo = 'Venta'; productoId = $item.productoId; localId = $item.localId
    cantidad = ($item.cantidad + 1); observacion = 'debe fallar' }
Afirmar "Venta mayor al stock devuelve 409" ($exceso.Status -eq 409) "status $($exceso.Status)"

$stockDespues = (Llamar GET '/api/stock' $tokenVend $null).Body |
    Where-Object { $_.productoId -eq $item.productoId }
Afirmar "El stock no cambio tras el rechazo" ($stockDespues.cantidad -eq $item.cantidad) `
    "antes=$($item.cantidad) despues=$($stockDespues.cantidad)"

$exacto = Llamar POST '/api/movimientos' $tokenVend @{
    tipo = 'Venta'; productoId = $item.productoId; localId = $item.localId
    cantidad = $item.cantidad; observacion = 'vaciar exacto' }
Afirmar "Vender exactamente todo el stock funciona" ($exacto.Status -eq 200) "status $($exacto.Status)"
Afirmar "El stock queda en cero" ($exacto.Body.cantidadResultante -eq 0) "resultante=$($exacto.Body.cantidadResultante)"

$deCero = Llamar POST '/api/movimientos' $tokenVend @{
    tipo = 'Salida'; productoId = $item.productoId; localId = $item.localId
    cantidad = 1; observacion = 'debe fallar' }
Afirmar "Salida con stock en cero devuelve 409" ($deCero.Status -eq 409) "status $($deCero.Status)"

$cero = Llamar POST '/api/movimientos' $tokenVend @{
    tipo = 'Entrada'; productoId = $item.productoId; localId = $item.localId
    cantidad = 0; observacion = 'cantidad invalida' }
Afirmar "Cantidad cero se rechaza" ($cero.Status -eq 409) "status $($cero.Status)"

$negativa = Llamar POST '/api/movimientos' $tokenVend @{
    tipo = 'Entrada'; productoId = $item.productoId; localId = $item.localId
    cantidad = -5; observacion = 'cantidad invalida' }
Afirmar "Cantidad negativa se rechaza" ($negativa.Status -eq 409) "status $($negativa.Status)"

$entrada = Llamar POST '/api/movimientos' $tokenVend @{
    tipo = 'Entrada'; productoId = $item.productoId; localId = $item.localId
    cantidad = 20; observacion = 'reposicion' }
Afirmar "Entrada suma stock" ($entrada.Body.cantidadResultante -eq 20) "resultante=$($entrada.Body.cantidadResultante)"

Write-Host "`n=== La venta congela el precio cobrado ==="
$productoVendido = (Llamar GET "/api/productos/$($item.productoId)" $tokenAdmin $null).Body
$precioAlVender = $productoVendido.precioBase

$venta = Llamar POST '/api/movimientos' $tokenAdmin @{
    tipo = 'Venta'; productoId = $item.productoId; localId = $item.localId
    cantidad = 6; observacion = 'venta mostrador' }
Afirmar "Venta registra precio unitario" ($null -ne $venta.Body.precioUnitarioAplicado) "nulo"
Afirmar "Y es el precio del producto" ($venta.Body.precioUnitarioAplicado -eq $precioAlVender) `
    "aplicado=$($venta.Body.precioUnitarioAplicado) base=$precioAlVender"
Afirmar "El total es precio x cantidad" ($venta.Body.total -eq ($precioAlVender * 6)) `
    "total=$($venta.Body.total) esperaba $($precioAlVender * 6)"

# Se cambia el precio del producto: la venta ya registrada NO se puede alterar.
$nuevoPrecio = $precioAlVender + 5000
$actualizarPrecio = Llamar PUT "/api/productos/$($item.productoId)" $tokenAdmin @{
    nombre = $productoVendido.nombre; categoriaId = $productoVendido.categoriaId
    codigoLinea = $productoVendido.codigoLinea; precioBase = $nuevoPrecio
    umbralStockBajo = $productoVendido.umbralStockBajo }
Afirmar "Se puede cambiar el precio del producto" ($actualizarPrecio.Status -eq 200) `
    "status $($actualizarPrecio.Status)"

$historial = @((Llamar GET "/api/movimientos?productoId=$($item.productoId)&limite=50" $tokenAdmin $null).Body)
$ventaEnHistorial = $historial | Where-Object { $_.id -eq $venta.Body.id }
Afirmar "La venta vieja sigue diciendo el precio que se cobro" `
    ($ventaEnHistorial.precioUnitarioAplicado -eq $precioAlVender) `
    "quedo en $($ventaEnHistorial.precioUnitarioAplicado), esperaba $precioAlVender"

$entradaSinPrecio = Llamar POST '/api/movimientos' $tokenAdmin @{
    tipo = 'Entrada'; productoId = $item.productoId; localId = $item.localId
    cantidad = 3; observacion = 'sin plata' }
Afirmar "Entrada NO registra total" ($null -eq $entradaSinPrecio.Body.total) `
    "total=$($entradaSinPrecio.Body.total)"

Write-Host "`n=== Regla 5: alcance por rol ==="
$stockAdminTodos = (Llamar GET '/api/stock' $tokenAdmin $null).Body
$localesEnRespuesta = @($stockAdminTodos | Select-Object -ExpandProperty localId -Unique).Count
Afirmar "Admin ve los 3 locales" ($localesEnRespuesta -eq 3) "locales distintos=$localesEnRespuesta"

$localesVend = @($stockVend | Select-Object -ExpandProperty localId -Unique)
Afirmar "Vendedor ve solo 1 local" ($localesVend.Count -eq 1) "locales=$($localesVend.Count)"
Afirmar "Y es el suyo" ($localesVend[0] -eq $localVend) "ve=$($localesVend[0]) propio=$localVend"

$otroLocal = ($stockAdminTodos | Where-Object { $_.localId -ne $localVend } |
    Select-Object -First 1).localId
$leerAjeno = Llamar GET "/api/stock?localId=$otroLocal" $tokenVend $null
Afirmar "Vendedor leyendo local ajeno devuelve 403" ($leerAjeno.Status -eq 403) "status $($leerAjeno.Status)"

$escribirAjeno = Llamar POST '/api/movimientos' $tokenVend @{
    tipo = 'Entrada'; productoId = $item.productoId; localId = $otroLocal; cantidad = 1 }
Afirmar "Vendedor operando local ajeno devuelve 403" ($escribirAjeno.Status -eq 403) `
    "status $($escribirAjeno.Status)"

$localesVisibles = @((Llamar GET '/api/locales' $tokenVend $null).Body)
Afirmar "Vendedor solo lista su propio local" ($localesVisibles.Count -eq 1) `
    "cantidad=$($localesVisibles.Count)"
$localesAdmin = @((Llamar GET '/api/locales' $tokenAdmin $null).Body)
Afirmar "Admin lista los 3 locales" ($localesAdmin.Count -eq 3) "cantidad=$($localesAdmin.Count)"

$vendedorCreaProducto = Llamar POST '/api/productos' $tokenVend @{
    codigoLinea = 'ZY'; nombre = 'No permitido'; categoriaId = $catId
    precioBase = 1000; umbralStockBajo = 1 }
Afirmar "Vendedor no puede crear productos (403)" ($vendedorCreaProducto.Status -eq 403) `
    "status $($vendedorCreaProducto.Status)"

$vendedorBorraCategoria = Llamar DELETE "/api/categorias/$catId" $tokenVend $null
Afirmar "Vendedor no puede borrar categorias (403)" ($vendedorBorraCategoria.Status -eq 403) `
    "status $($vendedorBorraCategoria.Status)"

Write-Host "`n=== Regla 6: transferencia entre locales, atomica ==="
# El producto de prueba tiene 7 unidades en $localElegido (cargadas mas arriba).
$otroDestino = ($localesTodos | Where-Object { $_.id -ne $localElegido } | Select-Object -First 1).id

function StockDe($productoId, $localId) {
    $filas = @((Llamar GET "/api/stock?busqueda=$skuPrueba" $tokenAdmin $null).Body)
    return ($filas | Where-Object { $_.localId -eq $localId }).cantidad
}

$origenAntes  = StockDe $productoTestId $localElegido
$destinoAntes = StockDe $productoTestId $otroDestino
$totalAntes   = $origenAntes + $destinoAntes

$transf = Llamar POST '/api/movimientos/transferencia' $tokenAdmin @{
    productoId = $productoTestId; localOrigenId = $localElegido
    localDestinoId = $otroDestino; cantidad = 3; observacion = 'prueba' }
Afirmar "La transferencia se registra" ($transf.Status -eq 200) "status $($transf.Status)"

$origenDespues  = StockDe $productoTestId $localElegido
$destinoDespues = StockDe $productoTestId $otroDestino
Afirmar "El origen queda con 3 menos" ($origenDespues -eq $origenAntes - 3) `
    "antes=$origenAntes despues=$origenDespues"
Afirmar "El destino queda con 3 mas" ($destinoDespues -eq $destinoAntes + 3) `
    "antes=$destinoAntes despues=$destinoDespues"
Afirmar "La mercaderia se conserva: el total no cambia" `
    (($origenDespues + $destinoDespues) -eq $totalAntes) `
    "antes=$totalAntes despues=$($origenDespues + $destinoDespues)"
Afirmar "Los dos asientos comparten el mismo id de transferencia" `
    ($null -ne $transf.Body.transferenciaId) "sin transferenciaId"

# --- casos que tienen que fallar ---

$excesoTransf = Llamar POST '/api/movimientos/transferencia' $tokenAdmin @{
    productoId = $productoTestId; localOrigenId = $localElegido
    localDestinoId = $otroDestino; cantidad = 9999 }
Afirmar "Transferir mas de lo que hay devuelve 409" ($excesoTransf.Status -eq 409) `
    "status $($excesoTransf.Status)"
Afirmar "Y NO movio nada (atomicidad)" `
    ((StockDe $productoTestId $localElegido) -eq $origenDespues -and
     (StockDe $productoTestId $otroDestino) -eq $destinoDespues) `
    "el stock cambio pese al rechazo"

$mismoLocal = Llamar POST '/api/movimientos/transferencia' $tokenAdmin @{
    productoId = $productoTestId; localOrigenId = $localElegido
    localDestinoId = $localElegido; cantidad = 1 }
Afirmar "Origen igual a destino devuelve 409" ($mismoLocal.Status -eq 409) "status $($mismoLocal.Status)"

$cantidadCero = Llamar POST '/api/movimientos/transferencia' $tokenAdmin @{
    productoId = $productoTestId; localOrigenId = $localElegido
    localDestinoId = $otroDestino; cantidad = 0 }
Afirmar "Cantidad cero se rechaza" ($cantidadCero.Status -eq 409) "status $($cantidadCero.Status)"

$vendedorTransf = Llamar POST '/api/movimientos/transferencia' $tokenVend @{
    productoId = $productoTestId; localOrigenId = $localVend
    localDestinoId = $otroDestino; cantidad = 1 }
Afirmar "Un vendedor no puede transferir (403)" ($vendedorTransf.Status -eq 403) `
    "status $($vendedorTransf.Status)"

# Un asiento de transferencia suelto dejaria mercaderia saliendo sin entrar a ningun lado.
$asientoSuelto = Llamar POST '/api/movimientos' $tokenAdmin @{
    tipo = 'TransferenciaSalida'; productoId = $productoTestId
    localId = $localElegido; cantidad = 1 }
Afirmar "No se puede registrar un asiento de transferencia suelto" ($asientoSuelto.Status -eq 409) `
    "status $($asientoSuelto.Status)"

$localInexistente = Llamar POST '/api/movimientos/transferencia' $tokenAdmin @{
    productoId = $productoTestId; localOrigenId = $localElegido
    localDestinoId = 999999; cantidad = 1 }
Afirmar "Local de destino inexistente devuelve 404" ($localInexistente.Status -eq 404) `
    "status $($localInexistente.Status)"

Write-Host "`n=== Baja logica y auditoria ==="
$baja = Llamar DELETE "/api/productos/$productoTestId" $tokenAdmin $null
Afirmar "Dar de baja devuelve 204" ($baja.Status -eq 204) "status $($baja.Status)"
# El @() es obligatorio: en PS 5.1, .Count sobre un resultado escalar de Where-Object
# devuelve vacio en lugar de 1.
$activos = (Llamar GET '/api/productos' $tokenAdmin $null).Body
Afirmar "El producto dado de baja no aparece por defecto" `
    (@($activos | Where-Object { $_.id -eq $productoTestId }).Count -eq 0) "sigue apareciendo"
$conInactivos = (Llamar GET '/api/productos?incluirInactivos=true' $tokenAdmin $null).Body
Afirmar "Aparece al pedir inactivos" `
    (@($conInactivos | Where-Object { $_.id -eq $productoTestId }).Count -eq 1) "no aparece"

$movInactivo = Llamar POST '/api/movimientos' $tokenAdmin @{
    tipo = 'Entrada'; productoId = $productoTestId; localId = $localVend; cantidad = 1 }
Afirmar "Producto inactivo no admite movimientos (409)" ($movInactivo.Status -eq 409) `
    "status $($movInactivo.Status)"

$movs = (Llamar GET '/api/movimientos' $tokenAdmin $null).Body
Afirmar "El historial registra movimientos" (@($movs).Count -gt 0) "cantidad=$(@($movs).Count)"
Afirmar "Cada movimiento sabe quien lo hizo" `
    (@($movs | Where-Object { [string]::IsNullOrEmpty($_.usuarioNombre) }).Count -eq 0) "hay usuarios vacios"

Write-Host "`n=== Inexistentes y salud ==="
$noExiste = Llamar GET '/api/productos/999999' $tokenAdmin $null
Afirmar "Producto inexistente devuelve 404" ($noExiste.Status -eq 404) "status $($noExiste.Status)"
$salud = Llamar GET '/health' $null $null
Afirmar "El endpoint /health responde 200" ($salud.Status -eq 200) "status $($salud.Status)"

Write-Host ""
if ($fallas -eq 0) {
    Write-Host "TODAS LAS VERIFICACIONES PASARON"
} else {
    Write-Host "$fallas VERIFICACION(ES) FALLARON"
}
exit $fallas
