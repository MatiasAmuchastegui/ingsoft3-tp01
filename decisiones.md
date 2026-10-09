# Decisiones — TP1

## 1. Por qué Git no pudo resolver el conflicto solo

Por que se freno: las dos ramas salieron del mismo ancestro y las 2 cambiaron la misma linea pero con contenido distinto. Entonces git no puede decidir cual de las 2 versiones es la correcta por lo que da un error y te da la opcion de arreglarlo vos.
Para que esto no pasara primero tendriamos que haber mergeado A y despues crear B para que B ya nazca con el main actualizado, entonces cuando modifcaras B, git no tendria que decidir.

Git solo puede resolver cuando una sola rama toco la linea, el conflicto aparece cuando mas de una rama tocan la linea. 

## 2. Problemas que encontré y cómo los solucioné

Require approvals viene ya tildado en 1 por defecto. Como el trabajo lo hago yo solo y github no te deja aprobar tu propio PR, lo tuve que poner en 0.

La primera vez que hice la prueba para que me rechace el push, la evidencia no era la que decia la guia que me iba a salir. Me decia fetch first, que era porque tenia la rama desactualizada y pasa aunque no este la proteccion, por lo que tuve que repetir con el main actualizado y ahi si funciono.

No sabia como subir las imagenes al git, por lo que me ayude de claude para hacerlo.

## 3. Declaración de uso de IA


Use IA (Claude) más que nada para entender conceptos, después para hacer el TP no la necesité. Sólo
para armar la parte de evidencias y subir las imágenes.

---

# Decisiones — TP2

## 1. Qué app elegí y por qué

Elegí un **sistema de gestión de stock para una joyería con tres locales**: catálogo de productos,
existencias separadas por sucursal, movimientos de entrada, venta y salida, transferencias entre
locales, y usuarios con dos roles distintos.


Contra los criterios de la guía:

| Criterio | Cómo lo cumple |
|---|---|
| **Frontend + backend + base de datos** | React 18 + Vite + TypeScript, ASP.NET Core 8 con Entity Framework, y PostgreSQL 16. Tres piezas separadas de verdad, cada una en su contenedor |
| **La entiendo y puedo explicarla** | Definí yo las reglas de negocio y el modelo, y probé cada funcionalidad a mano. Puedo explicar por qué cada regla está donde está |
| **Da para las capas que siguen** | Sirve tal cual para CI, tests, entrega continua, infraestructura como código y observabilidad. No hay que cambiarla para que el TP5 o el TP8 tengan sobre qué trabajar |
| **Acotada** | Tres pantallas —Login, Stock y Catálogo— y ninguna dependencia exótica. Todo lo que usa es estándar de cada stack |


## 2. Decisiones de contenerización

### Imágenes base elegidas

| Servicio | Etapa | Imagen | Por qué ésa |
|---|---|---|---|
| **Backend** | Compila | `mcr.microsoft.com/dotnet/sdk:8.0` | Es la imagen oficial de Microsoft con el compilador de C# y NuGet. Pesa ~1,2 GB porque trae todo lo necesario para **construir**, no para ejecutar |
| **Backend** | Ejecuta | `mcr.microsoft.com/dotnet/aspnet:8.0` | Sólo el runtime: sabe ejecutar una aplicación .NET pero no compilarla. Además ya trae definido el usuario sin privilegios `APP_UID` y el puerto 8080 por defecto |
| **Frontend** | Compila | `node:22-alpine` | Node hace falta para `npm ci` y `vite build`. La variante `alpine` es la misma imagen sobre una distribución mínima, así que la etapa de build baja más rápido |
| **Frontend** | Sirve | `nginx:1.27-alpine` | El resultado del build son archivos estáticos, y servirlos no necesita Node: necesita un servidor web. nginx además hace de proxy hacia el backend |
| **Base** | — | `postgres:16-alpine` | Imagen oficial, versión fijada. `alpine` porque la base no necesita nada del sistema operativo más allá del motor |


### Estructura multi-stage

**¿Por qué multi-stage?** Sin multi-stage, la imagen del backend cargaría el SDK completo de .NET
(~1,2 GB) sólo para ejecutar una aplicación que ya está compilada. Con multi-stage la imagen final
pesa **344 MB** y contiene únicamente el resultado publicado: 33 archivos, 11 MB de aplicación. Lo
mismo en el frontend: sin multi-stage viajarían Node, Vite y `node_modules`; con multi-stage viajan
sólo los HTML, JS y CSS del `dist/`, y la imagen queda en **73,9 MB**.

Y hay una segunda razón, que para mí pesa más que el tamaño: **la imagen final no tiene compilador**.
Si alguien lograra ejecutar algo dentro del contenedor, con el SDK adentro podría compilar y correr
código nuevo ahí mismo. Sin SDK, no puede. Lo que no está en la imagen no se puede explotar. Por el
mismo criterio el proceso corre como `USER $APP_UID` y no como root.

**Orden de instrucciones para aprovechar el cache:** copio primero los manifiestos de dependencias
(`.csproj` en el backend, `package.json` y `package-lock.json` en el frontend), instalo, y recién
después copio el código fuente. Así Docker no vuelve a bajar todos los paquetes cada vez que cambia
una línea de código, sino sólo cuando cambian las dependencias. Esa decisión, tomada acá, es la que
después hizo que el cache del pipeline del TP4 funcionara: la segunda corrida bajó de 1m09s a 20s
reutilizando 15 capas.

### Qué persiste y qué no

En el compose defino un volumen para que los datos de la base sobrevivan al contenedor. Un contenedor
es descartable —lo que escribe adentro se pierde cuando se borra— y una base de datos justamente no
puede darse ese lujo.

El volumen se llama **`db-data`** y está montado en `/var/lib/postgresql/data`, que es donde
PostgreSQL guarda todo. No alcanza con declararlo al final del archivo: hay que usarlo en el servicio.

- `docker compose down` → borra los contenedores, **el volumen queda**: al volver a levantar está todo
- `docker compose down -v` → borra también el volumen: la base se rehace de cero desde el seed

Lo comprobé destruyéndolo: cargué un producto que no viene de los datos de ejemplo y lo seguí a través
de los dos casos.

## 3. Problemas encontrados y cómo los resolví


**El bug que ningún test encontró.** Cargué un reloj nuevo desde la interfaz y no aparecía en la
pantalla de Stock, así que no había forma de asignarlo a ningún local. La consulta partía de la tabla
de stock, y un producto recién creado todavía no tiene fila ahí. Lo resolví armando la consulta desde
productos × locales con un LEFT JOIN. Apareció usando la aplicación como usuario, no corriendo
pruebas. Lo solucione con la ayuda de la IA.

## 4. Declaración de uso de IA

La app la hizo completamente la IA (Claude). Yo le di las indicaciones previas de lo que quería y como queria que fuera la app. Despues fui haciendo pruebas a mano de todas las funcionalidades de la app donde encontre alguno errores o algunas cosas que faltaban, que despues las solucione con IA.



# Decisiones — TP3

## 1. Duración del sprint y por qué

**Elegí una semana.**

 Hay una clase por semana, y entre clase y clase es cuando entiendo si lo que planifiqué
tenía sentido. Un sprint más largo que ese ciclo significa enterarme de que planifiqué mal cuando
ya no me queda margen para modificarlo.

Los tres factores que pesaron:

| Factor | Cómo empuja la decisión |
|---|---|
| Cadencia de la materia | Una clase por semana. El sprint alineado con ella cierra justo cuando llega la corrección |
| Tamaño del equipo | Uno solo. Con una persona, la ceremonia de un sprint largo no compra nada: no hay que sincronizar a nadie |
| Tamaño del trabajo | Cada TP entra cómodo en una semana. Dos semanas me obligarían a partir un TP al medio o a mezclar dos |


## 2. Límite de trabajo en progreso y por qué

**Puse 2 en la columna *In Progress*.**
 Cuando tengo cuatro tarjetas abiertas, ninguna avanza: cada vez que cambio de
una a otra pago el costo de recordar dónde estaba, y el trabajo a medio hacer no le sirve a nadie
hasta que se termina. 
Elegí 2 y no 1 porque con 1 me quedo bloqueado cada vez que algo depende de esperar —un run de CI,
una revisión— y quedarme sin hacer nada por respetar el tablero sería el tablero mandando sobre el
trabajo. Y no 3 porque un límite que nunca se toca no informa nada.

## 3. Diagnóstico de la historia mal escrita

Está mal escrita porque no es una historia, es una tarea técnica con formato de historia. El rol
es quien implementa la solución y no quien recibe el valor; el "quiero" pide directamente una
solución técnica —una tabla— en vez de describir una necesidad, así que ya viene con el cómo
decidido; y el "para" es una obviedad de implementación, no un beneficio: guardar los datos es
algo que el sistema tiene que hacer sí o sí para cumplir cualquier otra cosa. Escrita así no se
puede priorizar, porque nadie fuera del equipo puede decidir si vale más que otra historia, ni se
puede dar por terminada sin que sea una opinión.

**Como administrador del sistema, quiero que cada vendedor entre con su
propio usuario, para saber quién registró cada movimiento y que cada uno vea únicamente el stock
de su local.**



## 4. Problemas que encontré y cómo los resolví

### El campo Sprint no se puede crear desde la línea de comandos

Toda la estructura del TP3 la creé con `gh` —etiquetas, épica, historia, tareas, bug y las
relaciones padre-hijo— y me faltaba el campo de sprint. `gh project field-create` sólo acepta
`TEXT`, `SINGLE_SELECT`, `DATE` y `NUMBER`: el tipo *Iteration* no está. Y un campo de texto
llamado "Sprint" no es lo mismo, porque el tipo Iteration es el que tiene fechas de inicio y
duración. Ese campo lo creé desde la web, con duración de 1 semana.

## 5. Declaración de uso de IA

En el TP3 el trabajo estuvo repartido. La IA redactó los textos de la épica, la historia, las
tareas y el bug a partir de lo que yo le indiqué que quería, y armó el script que los creó en
GitHub con `gh` junto con la jerarquía de sub-issues. La duración del sprint y el límite de trabajo
en progreso los discutimos: le pedí que me diera el fundamento de cada número en vez del
número solo, y los adopté una vez que el razonamiento me cerró.

# Decisiones — TP4

## Estructura elegida del pipeline

El workflow tiene dos jobs, `build-backend` y `build-frontend`, uno por cada Dockerfile de la app.

Corren en paralelo, cada uno en su propio runner limpio, porque no dependen entre sí: ninguno
necesita nada que produzca el otro. Así el tiempo total es el del job más lento y no la suma de los
dos. Y tiene una segunda ventaja que se nota cuando algo falla: sé cuál de las dos mitades se
rompió sin tener que leer un log mezclado. En la demostración del gate se ve exacto —
`build-backend` en rojo y `build-frontend` en verde al mismo tiempo.

## Qué cachea el pipeline y qué pasa si el cache desaparece

Lo que se cachea son las capas de las imágenes. Con `setup-buildx-action` se prepara un constructor
que sabe exportarlas al almacén de GitHub Actions (`type=gha`), y cada job usa su propio `scope`
para no pisarse con el otro.

Gracias al orden del Dockerfile —primero los archivos de dependencias, después el código— las capas
del `dotnet restore` y del `npm ci` se reutilizan y aparecen como `CACHED`, mientras que las del
código fuente se rehacen en cada commit. Lo medí en el PR del workflow: la primera corrida tardó
1m09s sin ninguna capa guardada, y la segunda 20s reutilizando 15 capas, 8 del backend y 7 del
frontend.

Si el cache desapareciera —GitHub lo desaloja cuando quiere y tiene límite de tamaño— el pipeline
funcionaría exactamente igual, sólo que más lento: volvería a tardar 1m09s y daría verde igual. Es
una optimización, no una dependencia. Si fallara sin cache no tendría un cache, tendría una
dependencia escondida.

## Por qué construye con mi Dockerfile en vez de compilar por su cuenta

Porque si el workflow compilara por su cuenta con `dotnet` y `npm` tendría dos definiciones
distintas del mismo build, la del workflow y la del Dockerfile, y tarde o temprano se separan. El
día que cambie una versión en un lado y no en el otro estaría verificando una compilación que no es
la que después despliego, y un verde que no significa nada es peor que no tener pipeline.

Construyendo con el Dockerfile hay una sola definición, versionada en el repo, y es la misma que
produjo las imágenes que publiqué en el TP2. Un efecto lateral que confirma que la decisión es
buena: el workflow no tiene una sola línea de .NET ni de Node, así que le serviría igual a alguien
con otro stack.

## Problemas encontrados y cómo los resolví

El error que usé para romper el build a propósito no fue el que esperaba. Agregué `using NoExiste;`
al final de `Program.cs` pensando que iba a fallar por un namespace inexistente, y falló con
`CS1529: A using clause must precede all other elements` — porque `Program.cs` usa top-level
statements y un `using` no puede ir después de código. Rompió igual, que era el objetivo, pero por
otro motivo. Me enteré leyendo el log del job en Actions, que decía exactamente el archivo y la
línea.

## Declaración de uso de IA

Usé IA para escribir el ci.yml y para redactar este documento. Lo que hice yo fue correr el pipeline y verificar que hiciera lo que decía: comparé las dos corridas para ver el cache reutilizado, rompí el build a propósito para comprobar que el gate bloqueara el merge, y configuré la protección de main desde la web después de leer cómo estaba, para no perder lo del TP1.

---

# Decisiones — TP5

## Qué lógica elegí testear y por qué ésa

Pregunté dónde duele un bug en mi app, y la respuesta son cuatro lugares. Esos son los que
testeé:

| Regla | Qué pasa si se rompe |
|---|---|
| `GeneradorSku.NormalizarCodigo` | El SKU va impreso en la etiqueta de una pieza física. Un código mal formado no se arregla con un deploy: hay que reimprimir |
| `AuthService.ValidarCoherenciaRolLocal` | Un vendedor sin local no puede ver ni registrar nada, y un admin con local parece limitado cuando opera los tres |
| `AlcanceLocales` | Una vendedora viendo o moviendo el stock de otro local. Es el agujero de permisos de la app |
| `GeneradorTokenJwt` | Un token sin el claim de local deja a un vendedor sin alcance, o peor, con el de todos |

Son 53 tests sobre 31 métodos, con las tres técnicas: `[Theory]` parametrizado, casos de error
con sus bordes exactos, y un mock sobre `IUsuarioActual`.

El enunciado pide ocho métodos y escribí treinta y uno, y no fue una decisión de cantidad: **no
conté tests, conté comportamientos.** El mínimo de ocho sobre cuatro reglas da dos por regla —uno
que anda y uno que falla—, y ninguna de las mías entra en dos. `AlcanceLocales` sola tiene diez
caminos distintos: un vendedor consultando otro local, consultando el suyo, sin filtrar; un admin
sin filtro y con filtro; las tres variantes de escritura; y un vendedor sin local asignado, que no
puede ni leer ni escribir. Escribir dos habría dejado ocho sin verificar.

Igual la cantidad no es el criterio: treinta y un tests triviales valen menos que ocho bien
elegidos. El criterio es si cada uno protege algo, y eso se comprueba rompiendo la regla a ver si
algo se pone rojo — está más abajo, en el mutante.

Lo que **no** testeé son los services asincrónicos —`MovimientoService`, `ProductoService`,
`CategoriaService`, `StockService`—, y es una decisión y no un olvido: consultan la base, así que
probarlos es integración y no unitario. El enunciado define unit test como el que corre «sin tocar
red, disco ni base de datos», y por la pirámide ese nivel llega después.

En el frontend la lógica estaba **adentro de los componentes**: el cálculo del stock resultante en
`ModalMovimiento`, el filtro de destinos en `ModalTransferencia`, el contador de faltantes en
`StockPage`. La saqué a `src/lib/` para poder probarla sin montar React — está contado más abajo.

## El umbral, y por qué ese número

| | Umbral | Mide hoy |
|---|---|---|
| Backend | **16**, línea y rama | **18,18%** línea · **20,98%** rama |
| Frontend | **90**, línea y rama | **100%** línea · **100%** rama |

El del backend es bajo y tiene una razón que puedo defender: **mis unit tests cubren el 100% de la
lógica que un unit test puede cubrir**. El 18% sale de que el resto de mi lógica vive en services
contra Entity Framework, que es otro nivel de la pirámide. Puse 16 para que me frene si bajo, no
para que sea inalcanzable — y para subirlo de verdad tendría que escribir tests de integración, no
más unit tests.

Elegí el número **después** de medir, no antes. Medí primero con todo el ensamblado (3,1%), después
con los filtros, y recién ahí puse el umbral.

El del frontend es alto porque mide poco: `src/lib` son 25 líneas de funciones puras y las cubrí
todas. 90 deja diez puntos de aire para que un refactor menor no rompa el build, pero frena cuando
entra lógica sin tests — que es exactamente lo que pasó en la demostración.

### Por qué un 18% y un 82% pueden ser los dos correctos

Comparé mi umbral con el de un compañero que tiene **82,7%**, y conseguí los números de su proyecto
para entender la diferencia. No testeamos distinta cantidad: **medimos cosas distintas.**

| | Él | Yo |
|---|---|---|
| Arquitectura | Sin capa de services: la lógica vive adentro de los routers, mezclada con las queries | Services separados de los controllers |
| Qué cubre su porcentaje | Los routers — 374 de 580 statements | Lógica pura — 96 de 1.430 líneas |
| Con qué los cubre | **14 tests de integración** contra una base SQLite real, vía `TestClient` | Unit tests puros, sin IO |
| Tests estrictamente unitarios | 9 funciones | 31 métodos |

Su 82,7% viene en su mayor parte de **tests de integración**, no unitarios. El mío sale de no tocar
la base en ningún test. Si yo levantara mi API con una base de prueba y le pegara a los endpoints,
mis 830 líneas de services se cubrirían y mi número saltaría parecido: **puedo hacerlo, elegí no
hacerlo**, porque el enunciado define unit test como el que corre sin tocar red, disco ni base.

Lo confirma su propia medición: le pregunté qué pasaría si su proyecto tuviera 800 líneas de
services sin cubrir, y la respuesta fue que caería a ~38%. O sea que el 82,7% no es un estándar de
la materia: es una consecuencia de cómo está hecha su app.

La conclusión que me llevo es que **el porcentaje solo no dice nada si no se sabe qué hay en el
denominador**. Por eso el enunciado pide justificarlo y no acertarle.

Los dos umbrales miran **línea y rama**. Con sólo líneas el freno es más débil: una condición
ejecutada por un solo camino da 100% de línea y 50% de rama.

## Qué dejé afuera de la cuenta, y por qué

**Backend** — `/p:Exclude` en el `ENTRYPOINT` de la etapa de tests:

| Qué | Por qué |
|---|---|
| `Program*` | Es el arranque. Si está mal, la app no levanta y me entero sin ningún test |
| `Domain.Entities.*` · `Application.Dtos.*` | Clases de datos: sólo propiedades, ninguna regla. Testearlas es testear que una propiedad guarda un valor |
| `Infrastructure.Migrations.*` | Las escribió Entity Framework, no yo. Testearlas es testear al generador |
| `Infrastructure.Configurations.*` · `AppDbContext` · `DbSeeder*` | Configuración declarativa de EF y carga inicial de datos. No tienen comportamiento |

**Los controllers quedan adentro a propósito.** Sacarlos subiría el número de 18,2% a 19,3%, y no
lo hice: son código mío y un punto de cobertura no vale que la medición diga menos. Excluir el
arranque y lo generado es medir lo que importa; excluir código propio porque no lo testeé es otra
cosa.

**Frontend** — al revés, con `include: ['src/lib/**']` digo qué **sí** entra: la lógica de negocio.
Los componentes, el ruteo y el cliente HTTP quedan afuera porque un unit test no puede custodiar una
pantalla — eso se verifica de punta a punta, y llega en el TP7.

Las dos listas del backend —la del umbral y la de los `-classfilters` del reporte— dicen lo mismo a
propósito. Si no coincidieran, el resumen de la corrida mostraría un número y el umbral exigiría
otro, y eso se lee como trampa.

## Por qué una cobertura alta no garantiza calidad

Porque la cobertura mide **ejecución**, no **verificación**. Este test deja
`GeneradorSku.NormalizarCodigo` ejecutada y no comprueba absolutamente nada:

```csharp
[Fact]
public void CoberturaSinVerdad()
{
    GeneradorSku.NormalizarCodigo("ct", "Código", 10);   // se ejecutó… y no hay ningún Assert
}
```

Suma cobertura igual que un test de verdad. Por eso la cobertura baja sí es señal confiable —hay
código que nadie ejercita— pero la alta no lo es.

Lo comprobé en mi propio código con un **mutante**: cambié `limpio.Length > maximo` por `>=` y corrí
la suite. Un solo test se puso rojo, `NormalizarCodigo_ExactamenteElMaximo_EsAceptado`. El del caso
de error —el que usa un código más largo que el máximo— **siguió pasando**, porque con `>` y con
`>=` lo rechaza igual.

O sea: sin el test del borde exacto tenía 100% de cobertura sobre una regla que nadie estaba
verificando. Es la mejor demostración de que el porcentaje puede mentir, y de por qué los casos de
borde importan más que la cantidad de tests.

## El Pull Request bloqueado

La secuencia completa está en el **PR #32**:
<https://github.com/MatiasAmuchastegui/ingsoft3-tp01/pull/32>

Agregué `urgenciaDeReposicion` a `src/lib/stock.ts`, una función con cinco caminos y sin un solo
test. El resultado:

```
Tests  22 passed (22)

File         | % Stmts | % Branch | % Funcs | % Lines
All files    |      65 |      100 |   83.33 |      65
 stock.ts    |   36.36 |      100 |   66.66 |   36.36

ERROR: Coverage for lines (65%) does not meet global threshold (90%)
```

**Qué check se puso en rojo**: `build-frontend`. `build-backend` quedó en verde — alcanza con uno
para bloquear el merge.

**En qué métrica**: líneas, de 100% a 65%. Las ramas se quedaron en 100%, y tiene explicación: en
vitest 2.x, una función que ningún test llama **no suma ramas**, sólo líneas sin cubrir. Por eso el
umbral va sobre las dos: con uno solo de ramas, esta demostración no se habría puesto roja.

**Por qué**: el código compilaba perfecto y los 22 tests pasaban. Lo que lo frenó fue un número de
calidad que elegí yo. Es la primera vez en la materia que lo que bloquea un merge no es que algo
esté roto.

**Qué escribí para arreglarlo**: un test por cada camino que la función declara —sin stock, crítica,
baja, normal, holgada— más los bordes exactos de cada franja: la mitad del umbral, el umbral y el
doble. Son los que distinguen un `<=` de un `<`. La cobertura volvió a 100% y el check pasó a verde.

Corridas: [la roja](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37528845178) ·
[la verde](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37529263673)

Y el **PR #33** queda abierto y en rojo a propósito, con el botón de merge deshabilitado:
<https://github.com/MatiasAmuchastegui/ingsoft3-tp01/pull/33>

## El refactor que hizo falta para poder testear

En el frontend no había nada que probar unitariamente: la lógica estaba mezclada con el JSX.

```tsx
// ANTES, adentro de ModalMovimiento.tsx:
const cantidadResultante = item.cantidad + (tipo === 'Entrada' ? cantidad : -cantidad)
const excedeStock = esEgreso && cantidad > item.cantidad

// ANTES, adentro de ModalTransferencia.tsx:
const destinosPosibles = locales.filter((l) => l.id !== item.localId)
```

Para probar eso había que montar React, que es mucha maquinaria para verificar una resta. Lo saqué a
`src/lib/movimientos.ts` y `src/lib/stock.ts`, y los componentes ahora las llaman. **El segundo paso
no te lo reclaman los tests**: si extraés las funciones y no actualizás los componentes, la suite
queda verde sobre código que la app no ejecuta.

Para el mock hice algo más: `faltantesDe` recibe el cliente HTTP **por parámetro** en lugar de
llamar a `api.stock.listar` adentro. Así el test le pasa un `vi.fn()` y no hace falta una API
levantada. Es la misma lección que ya tenía resuelta en el backend sin saberlo —`IUsuarioActual` es
una interfaz desde el TP1, y el comentario de esa interfaz dice textual que está así «para que la
regla se testee con un doble de prueba, sin levantar un servidor HTTP ni fabricar tokens JWT»—.

## El camino sin cubrir

Elegí la línea 85 de `GeneradorSku.cs`, dentro de `GenerarAsync`:

```csharp
// Tolera series viejas con otra cantidad de dígitos (REL-001 y REL-0001 conviven).
if (int.TryParse(parteNumerica, NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
    && numero > ultimo)
```

El reporte la marca en **`0% (0/4)`**: abre dos decisiones y ningún test recorre ninguna.

**Qué entrada la recorrería**: un producto cuyo SKU tenga una parte numérica que no sea un número —
`RELCT-ABCD`, o un código viejo cargado a mano antes de que el sistema los generara. `TryParse`
devuelve `false` y ese SKU se saltea en lugar de romper el cálculo del correlativo.

**Qué decidí**: no agregarlo. `GenerarAsync` consulta la base para traer los SKU existentes, así que
probarlo exige un DbContext y pasa a ser integración. Y es irónico: esa rama existe justamente para
tolerar datos viejos, que es el tipo de cosa que un test con base de datos verificaría bien y uno
con un doble no. Queda anotado como deuda para cuando la materia llegue a ese nivel.

## Problemas encontrados y cómo los resolví

**El primer `dotnet test` fue una pared de `CS0246`.** El proyecto tiene `ImplicitUsings` activado,
pero eso no incluye xUnit: faltaba `using Xunit;` en cada archivo. El error no dice «falta un using»,
dice que no encuentra `Fact` ni `InlineData`, que es lo mismo pero no se lee igual.

**`npm i -D vitest` falló con `ERESOLVE`.** La versión 5 de vitest pide vite 6 o más y el proyecto
usa vite 5.4. La salida no es `--force` —eso instala una resolución rota a propósito— sino fijar la
línea compatible: `vitest@^2`. Las dos quedaron en 2.1.9.

**El pipeline habría quedado verde mintiendo.** Mi etapa `build` del Dockerfile copiaba sólo el
proyecto de la API, así que al agregar `FROM build AS test` el proyecto de tests no estaba adentro de
la imagen. Lo peor es que **no falla**: `dotnet test` sobre una solución sin proyecto de tests
devuelve 0 y no dice nada. Lo encontré construyendo la etapa a mano antes de subirla. Se arregla
copiando también el `.csproj` de tests y el `.sln` antes del `restore`.

**El frontend falló en mi máquina y no en el pipeline.** Al correr la etapa de tests en Docker me dio
`No test files found` y un `filter: Files/Git/salida/reporte` que no tenía sentido. Era Git Bash en
Windows convirtiendo `/salida/reporte` a una ruta de Windows: el espacio de «Program Files» partió
el argumento en dos y vitest tomó la segunda mitad como un filtro de nombres de test. En el runner de
Linux no pasa, y de hecho el pipeline salió verde a la primera. Para probarlo local hay que prefijar
`MSYS_NO_PATHCONV=1`. El síntoma —no hay tests— apunta al lugar equivocado.

**Un PR quedó en conflicto y sin checks.** Armé la rama del umbral encima de la del pipeline antes de
mergear la primera; al mergearla con *squash*, sus commits dejaron de ser ancestros de `main` y
GitHub no corre checks sobre un PR en conflicto. Se resuelve rehaciendo la rama desde el `main`
actual con `cherry-pick`. La lección es ramificar desde `main` después de cada merge, no encadenar
ramas.

## Declaración de uso de IA

Usé IA (Claude) para escribir los tests, las etapas de tests de los Dockerfiles, los pasos del
`ci.yml` y para redactar este documento. Lo que decidí y verifiqué yo:

1. **El umbral.** Medí primero —3,1% con todo, 14,3% con los filtros básicos, 18,18% con los
   definitivos— y recién después elegí el número. No copié el 70 del ejemplo de la cátedra, que
   corresponde a una app cuya lógica es un validador de títulos y no un sistema de stock con services
   asincrónicos.
2. **Qué queda afuera de la cuenta**, incluida la decisión de **no** excluir los controllers aunque
   habría subido el número.
3. **Comprobé que el umbral frena, no sólo que mide.** Forcé el del backend a 30 y el contenedor
   salió con error 1 con los 53 tests en verde; agregué un archivo sin tests al frontend y cayó a
   80,64% con los 22 en verde. Un umbral que no se probó rompiendo es un umbral que no se sabe si
   existe.
4. **Corrí un mutante sobre mi propio código** —cambiar `>` por `>=`— para ver qué test lo mataba.
   Descubrí que el caso de error no lo detectaba y que hacía falta el del borde exacto.
5. **Construí y corrí las dos etapas de tests a mano** antes de subirlas al pipeline. Ahí aparecieron
   los dos problemas del Dockerfile y del path de Windows.
6. **Leí cada test para saber qué protege y qué no.** El ejercicio del camino sin cubrir sale de
   abrir el reporte y mirar el código, no el número.

Puedo explicar qué verifica cada assert de mi suite y qué casos quedaron afuera. Lo que no puedo
decir es que escribí los tests a mano.
