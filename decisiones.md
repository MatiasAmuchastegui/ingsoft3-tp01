# Enlaces del TP6 y del TP7

Van acá arriba porque viven afuera del repositorio, o son unas pocas corridas entre muchas: si no
están acá, la corrección no los busca.

**Los entornos, vivos hasta la defensa.** Las del formulario son las del front.

| | Front | API (su chequeo de salud) |
|---|---|---|
| QA | https://joyeria-front-qa.onrender.com | https://ingsoft3-tp01-jx17.onrender.com/health |
| Producción | https://joyeria-front-prod.onrender.com | https://ingsoft3-tp01-1.onrender.com/health |

El plan gratuito duerme los servicios sin tráfico: el primer pedido después de un rato tarda
alrededor de 30 segundos.

**Las dos imágenes, públicas**, se bajan sin credenciales. Cada merge a `main` publica una por
commit, con la etiqueta `sha-<commit>` de 40 caracteres.

| | |
|---|---|
| Backend | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/pkgs/container/ingsoft3-tp01-backend |
| Frontend | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/pkgs/container/ingsoft3-tp01-frontend |

## TP6: la cadena

| | |
|---|---|
| El job de un PR: «Entrar al registry» sale salteado y no se publica nada | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37992931367/job/114031489474 |
| El mismo job en `main`: entra al registry y publica | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37993396090/job/114033099476 |
| La primera corrida de `main`: QA desplegado y producción rechazada, con el motivo | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37993396090 |
| La primera que llegó a producción, aprobada | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37996088210 |
| Qué commit está en cada entorno y desde cuándo | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/deployments |

## TP7: integración verde y e2e roja

**La corrida donde la integración quedó verde y la e2e roja**, y la promoción se frenó:

| | |
|---|---|
| El commit que rompió la app | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/commit/247d8bcf6c27b8a079e50edd1e836263317d5def |
| La corrida | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/39529017346 |
| `playwright-report-integracion` (verde) | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/39529017346/artifacts/12314926096 |
| `playwright-report-e2e` (rojo) | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/39529017346/artifacts/12314991331 |

**La corrida completa en verde después del arreglo, hasta producción:**
https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/39531146035

`v7.0.0` apunta a `24c876a`, el commit que corría en producción al cerrar el TP7. En el TP8
producción no se movió, y en el TP9 avanzó para llevar Sentry. Lo que corre hoy se ve en
*Deployments*.

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

## Qué lógica testeé y por qué

Testeé las cuatro reglas donde un bug de mi app hace más daño: `GeneradorSku.NormalizarCodigo` (el
SKU va impreso en la etiqueta de la pieza, si sale mal hay que reimprimir), `AuthService.ValidarCoherenciaRolLocal`
(un vendedor sin local no puede trabajar), `AlcanceLocales` (que una vendedora no vea ni mueva el
stock de otro local) y `GeneradorTokenJwt` (que el token lleve el local correcto).

Son 53 tests sobre 31 métodos. El TP pedía mínimo 8, pero conté comportamientos y no tests:
`AlcanceLocales` sola tiene diez caminos distintos, y con dos tests habrían quedado ocho sin
verificar. Usé `[Theory]` con varios casos, casos de error con sus bordes exactos y un mock de
`IUsuarioActual`.

No testeé los services que consultan la base (`MovimientoService`, `ProductoService` y los demás),
porque eso ya es integración y el TP pide unit tests que no toquen la base.

En el front la lógica estaba mezclada con los componentes, así que primero la saqué a `src/lib/`:
el cálculo del stock resultante, los destinos de una transferencia y el contador de faltantes.
Así se prueba sin montar React. Una de las funciones recibe el cliente HTTP por parámetro, y el test
le pasa un `vi.fn()` en lugar de una API de verdad.

Los tests: [backend](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/tree/v5.0.0/backend/JoyeriaStock.Tests) ·
[frontend](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/tree/v5.0.0/frontend/src/lib).

## El umbral y por qué ese número

Puse **16% en el backend**, que mide 18,18% de línea y 20,98% de rama, y **90% en el frontend**, que
mide 100%. Los dos miran línea y rama.

El del backend es bajo pero tiene explicación: mis tests cubren toda la lógica que un unit test
puede cubrir. El resto son services que hablan con la base, y para cubrirlos hacen falta tests de
integración, no más unit tests. Lo elegí después de medir, y un poco por debajo para que me frene si
bajo y no por un cambio chico. El del front es alto porque mide poco: `src/lib` son funciones puras
y están todas cubiertas.

El umbral está en el [Dockerfile del backend](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/blob/v5.0.0/backend/Dockerfile#L79-L80)
y en el [`vite.config.ts`](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/blob/v5.0.0/frontend/vite.config.ts#L37).
El resumen de cobertura y los reportes para bajar están en
[esta corrida](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37529263673).

Un compañero tiene 82,7% y le pedí sus números para entender la diferencia. Su app no tiene services,
la lógica está en los routers, y la cubre con tests de integración contra una base SQLite. No
medimos lo mismo: el porcentaje solo no dice nada si no se sabe qué se está midiendo.

## Qué dejé afuera de la cuenta

En el backend saqué el arranque (`Program`), las entidades y los DTOs, que son sólo datos, las
migraciones, que las genera Entity Framework, y la configuración de la base y el seed. Los
controllers los dejé adentro a propósito: sacarlos subía el número de 18,2% a 19,3%, pero son
código mío. En el front hice al revés: con `include: ['src/lib/**']` digo qué entra, que es la
lógica. Están en el [`Exclude` del backend](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/blob/v5.0.0/backend/Dockerfile#L81)
y en el [`include` del front](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/blob/v5.0.0/frontend/vite.config.ts#L25).

## Por qué una cobertura alta no garantiza calidad

Porque la cobertura mide qué código se ejecutó, no qué se comprobó: un test sin ningún `Assert`
suma cobertura igual que uno bueno. Lo vimos con un mutante: cambiamos un `>` por `>=` en
`NormalizarCodigo`, y el test del caso de error siguió pasando. El único que se puso rojo fue el del
borde exacto. Sin ese test tenía 100% de cobertura sobre una regla que nadie verificaba.

## El Pull Request bloqueado

En el [PR #32](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/pull/32) agregué al front una
función sin tests, `urgenciaDeReposicion`. Compilaba y los 22
tests pasaban, pero la cobertura de líneas bajó a 65% y el umbral de 90 puso `build-frontend` en
rojo, así que no se podía mergear. Le agregué un test por cada caso y sus bordes, la cobertura
volvió a 100% y el check pasó a verde. La conversación del PR muestra la secuencia entera: rojo,
tests, verde y merge.

Corridas: [la roja](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37528845178) ·
[la verde](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37529263673). El
[PR #33](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/pull/33) queda abierto y en rojo a
propósito, como evidencia.

## El camino sin cubrir

La [línea 85 de `GeneradorSku.cs`](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/blob/v5.0.0/backend/JoyeriaStock.Api/Application/Services/GeneradorSku.cs#L85)
está en 0%: es la que ignora los SKU viejos cuya parte numérica no
es un número, por ejemplo `RELCT-ABCD`. No le agregué test porque `GenerarAsync` consulta la base,
así que probarlo ya es integración. Queda como deuda.

## Problemas encontrados y cómo los resolví

El primer `dotnet test` tiró un montón de errores `CS0246`: faltaba `using Xunit;` en cada archivo
de tests.

`npm i -D vitest` falló con `ERESOLVE`, porque vitest 5 pide vite 6 y el proyecto usa vite 5. Lo
resolví instalando `vitest@^2`, sin `--force`.

La etapa de tests del Dockerfile no copiaba el proyecto de tests, y `dotnet test` devolvía 0 sin
correr nada: el pipeline habría quedado verde sin testear. Se arregló copiando también el `.csproj`
de tests y el `.sln`.

En mi máquina la etapa de tests del front decía que no había tests, porque Git Bash cambiaba la ruta
`/salida/reporte` por una de Windows. Se arregla con `MSYS_NO_PATHCONV=1`; en el pipeline no pasa.

## Declaración de uso de IA

Usé IA (Claude) para el código del TP5: escribió los tests, sacó la lógica del front a `src/lib` y
armó las etapas de tests de los Dockerfiles y los pasos del `ci.yml`. También midió la cobertura,
corrió el mutante y comprobó que el umbral frena, y redactó este documento. El umbral lo decidí yo a
partir de esa medición: lo comparé con el de un compañero para entender por qué el mío daba 16 y no
75. Todo lo que no era código lo hice yo.

---

# Decisiones — TP6

Los enlaces de este TP están arriba de todo, junto con los del TP7.

## Qué cambió

Hasta el TP5 el pipeline sólo verificaba. Ahora, cuando algo entra a `main`, publica las dos
imágenes, las despliega en QA solo y deja producción esperando a que yo apruebe. Cada paso depende
del anterior con `needs:`, así que si los tests fallan no se publica ni se despliega nada.

## La imagen, etiquetada con el commit

Cada imagen se publica en ghcr.io con la etiqueta `sha-<commit>` y no con `latest`, porque `latest`
cambia todo el tiempo y no dice qué hay adentro. Con el commit en la etiqueta sé exactamente qué
código está corriendo.

Se publica sólo con todo en verde, porque lo que está en el registry es lo que después se despliega.
Si se publicara igual, que una imagen esté ahí ya no querría decir que pasó los tests. Lo aseguran
tres cosas encadenadas: nada entra a `main` sin los checks en verde, sólo `main` publica, y publicar
es el último paso del job que testea, así que si un test falla no se llega. En un PR el paso de
entrar al registry sale salteado.

## La misma imagen en QA y en producción

La dirección del backend estaba escrita adentro del `nginx.conf`, y en QA y en producción es
distinta. La cambié por una plantilla, `default.conf.template`, que nginx completa al arrancar con
la variable `BACKEND_URL`. Así los dos frontends corren la misma imagen y sólo cambia una variable.

## Quién despliega

Apagué el auto-deploy de Render en los cuatro servicios. Si no, Render desplegaría solo en cada
push, sin esperar al CI ni a mi aprobación. El deploy lo dispara el pipeline con un deploy hook, y
le paso `&ref=<commit>` para que despliegue exactamente el commit que pasó los tests.

Una limitación: Render no usaba la imagen que publico, la volvía a construir desde el repo. Era el
mismo commit, pero no el mismo archivo, así que perdía la garantía de que lo que corre es
exactamente lo que pasó los tests. Lo arreglé en el TP7.

## La cadena y los secrets

Los jobs van `build-backend` y `build-frontend` → `deploy-qa` → `deploy-prod`, unidos con `needs:`.
`deploy-qa` tiene un `if` para correr sólo en un push a `main`: un PR verifica, pero no despliega.
Cada deploy declara su `environment`, `qa` o `production`. Desde el TP7, entre los dos están la
integración y la e2e.

Los deploy hooks son secrets de cada environment, no del repo: `RENDER_HOOK_API` y
`RENDER_HOOK_FRONT` existen en `qa` y en `production`, con valores distintos. Un job sólo ve los
secrets del environment que declara, así que el de QA no puede leer los hooks de producción, y el de
producción los recibe recién después de la aprobación. Para publicar en ghcr no uso ningún secret
mío: alcanza con el `GITHUB_TOKEN` que GitHub crea para cada corrida, y sólo los dos jobs de build
tienen permiso para escribir paquetes. La cadena de conexión y la clave de los tokens no pasan por
GitHub: son variables de cada servicio de Render.

## Los entornos están separados

QA y producción tienen cada uno su base en Neon, `app_qa` y `app_prod`, y su propia clave para
firmar los tokens. Lo comprobamos: un token sacado en QA da 401 en producción, y una categoría
creada sólo en producción no aparece en QA.

## La aprobación

QA se despliega solo y producción espera a un revisor, que soy yo. Eso es entrega continua: todo
queda listo para producción, pero apretar el botón es una decisión. Si saco el revisor pasa a ser
despliegue continuo, sin cambiar nada del workflow. Elegí entrega continua porque un error en
producción frena las ventas de los tres locales, y prefiero que alguien mire QA antes de pasarlo.

Antes de aprobar miro que la corrida esté en verde hasta QA, con el smoke pasado (desde el TP7,
también la integración y la e2e), que no haya entrado otro merge mientras tanto, porque QA es uno
solo, y entro a QA a probar el login y el stock. Lo que no puedo ver desde ahí es cómo se va a
portar con los datos y el uso de producción: para eso faltaba observabilidad, que llegó en el TP9.

Rechacé la primera corrida con este motivo: *«Rechazo: es la primera corrida del pipeline de
despliegue y QA recién terminó de levantar. Quiero verificar a mano el login y el listado de stock
en QA antes de tocar producción»*. El job de producción no ejecutó ningún paso, porque la aprobación
es una puerta antes del job.

## La prueba de humo

Después de cada deploy, un script le pide al front y a la api que respondan, y reintenta porque los
servicios gratis tardan en despertar. Prueba que el entorno responde, no que la versión nueva esté
arriba: mientras Render levanta la nueva, la vieja sigue contestando.

## El plan gratuito

Render gratis duerme cada servicio después de 15 minutos sin tráfico, y despertarlo tarda unos 30
segundos. Además, el workspace tiene 750 horas por mes para todos los servicios juntos, y los
minutos de build también tienen un tope. El pipeline lo maneja así: el smoke espera 45 segundos y
reintenta 30 veces cada 20, y nginx espera al backend hasta 75 segundos (`proxy_read_timeout`), para
que el primer pedido no corte mientras la api se despierta. Desde el TP7 Render ya no construye
nada, sólo baja la imagen, así que tampoco gasta minutos de build.

## El patrón de deploy y el rollback

Render hace un reemplazo sin corte: levanta la versión nueva, espera a que esté sana y recién ahí le
pasa el tráfico. Para una producción de verdad elegiría blue-green: dos entornos iguales, y el
tráfico se pasa de uno al otro, así que volver atrás es devolver el tráfico. Canary necesita
repartir el tráfico entre dos versiones y métricas para compararlas, y hoy no tengo ninguna de las
dos.

Mi plan si producción se rompe después de aprobar: en Render, *Deploys* → el deploy anterior →
*Rollback*, y después revertir el commit por PR, para que `main` vuelva a ser lo que corre. Lo
probé: Render tardó unos 11 segundos y la app no dejó de responder en ningún momento, porque deja el
contenedor viejo hasta que el nuevo está listo. El rollback vuelve el código, no la base: si la
versión rota hubiera migrado o escrito datos mal, eso hay que arreglarlo aparte, por ejemplo
restaurando la base desde el historial de Neon.

De aprobar a tener producción verificada pasaron 56 segundos.

## Problemas encontrados y cómo los resolví

Neon da la cadena de conexión en formato `postgresql://…` y Npgsql no la acepta: hay que elegir la
versión ".NET" en el panel de Neon.

Las variables de .NET van con doble guion bajo, `ConnectionStrings__Default` y `Jwt__Key`. Con
`JWT_KEY` la app no la encuentra.

En Render la app corre en Production y no aplicaba las migraciones ni cargaba los datos. Hubo que
agregar `AplicarMigracionesAlArrancar` y `SembrarDatosIniciales` en `true`.

El build del front empezó a fallar con `429 Too Many Requests` de Docker Hub, porque los runners de
GitHub comparten IP. Lo resolví bajando las imágenes de Docker Hub por un espejo.

Once días después del primer deploy, el primer pedido por el front dio 502: las apis estaban
dormidas. Preparando la defensa medí que por el front no se despiertan nunca: 150 segundos de
intentos de login, todos 502. Render limita los despertares que vienen de sus propias IPs, y el
front corre en Render. Un pedido de afuera, en cambio, despierta la api en unos 35 segundos. Lo
resolví haciendo que el navegador, al abrir la app, le pegue directo a su api, y que la pantalla
diga que el servidor se está despertando en lugar de «Error 502».

## Declaración de uso de IA

Usé IA (Claude) para escribir el código del TP6 —los jobs de publicar y desplegar, la plantilla de
nginx y el script de humo— y para redactar este documento. La parte de las cuentas la hice yo
siguiendo sus indicaciones: creé las dos bases en Neon y los cuatro servicios en Render con sus
variables, apagué el auto-deploy, armé los environments de GitHub con el revisor y cargué los
deploy hooks como secrets. También aprobé y rechacé los despliegues, e hice el rollback desde Render
cronometrándolo. Las comprobaciones de que los entornos estaban separados las hizo la IA, y yo miré
los resultados.

---

# Decisiones — TP7

## Build once, deploy many

En el TP6 Render reconstruía la app en cada deploy, así que lo que corría no era la imagen que había
pasado los tests. Mis Dockerfiles parten de imágenes como `node:22-alpine`, que cambian con el
tiempo, así que dos builds del mismo commit podían salir distintos. Ahora la imagen se construye una
sola vez, y QA y producción ejecutan esa misma. Además desplegar desde una imagen ya hecha tarda
segundos en vez de minutos.

## Las etiquetas

Las imágenes llevan `sha-<commit>` y el repo lleva el tag `v7.0.0`. La etiqueta `sha` dice de qué
commit salió cada imagen y es la que se usa para desplegar; el tag de git es el nombre de una
versión que anduvo en producción. No publico `latest` porque no dice qué hay adentro.

A los cuatro servicios de Render les cambié la fuente a imagen sin recrearlos, así que conservan
URL, variables y hooks. Quedaron configurados con la imagen del último merge del TP6, pero ésa no es
la que corre: cada deploy del pipeline le manda su imagen con `imgURL`. Por eso no hay que usar el
botón *Manual Deploy* de Render, que desplegaría la imagen vieja.

Del tag a la imagen hay un paso: `git rev-list -n1 v7.0.0` da el commit, y la imagen es
`sha-<ese commit>`.

## Cómo se ve desde afuera qué imagen corre

En el log del pipeline aparece el `imgURL` y el id del deploy que devuelve Render, y en Render ese
mismo deploy dice *Triggered via Deploy Hook* con la misma imagen. El smoke no alcanza para eso:
sólo pregunta si el entorno responde, y la versión vieja sigue respondiendo mientras arranca la
nueva.

## Las dos suites

La de integración, `api.spec.ts`, le habla a la api de QA sin navegador: crea y borra una categoría,
prueba que un nombre vacío se rechaza sin crear nada, y registra una entrada y una venta para ver
que el stock cambia en la base y vuelve a como estaba. La e2e, `flujos.spec.ts`, usa la app con un
navegador: crear y borrar una categoría, un prefijo repetido que muestra el error, y el mismo
movimiento de stock desde la pantalla.

La tercera de cada una la elegí porque registrar movimientos es lo que se hace todos los días en el
local, y si se rompe me escribe la sucursal. Es la misma en las dos a propósito: si falla la e2e,
miro la integración para saber si también falla la api.

Lo que ya prueban los unit tests del TP5 —los SKU, los permisos, el JWT— no lo repetí arriba. Los
movimientos no se pueden borrar, así que las pruebas de stock los compensan: una entrada y una venta
dejan la cantidad como estaba.

El nombre vacío da 409 y no 400 como dice el TP, porque mi api trata todas las reglas de negocio
como 409. El test fija lo que mi api hace hoy.

## La prueba de que funcionan juntas

Para la evidencia rompí el front a propósito: cambié el campo `nombre` por `name` en lo que manda al
crear una categoría. TypeScript compiló y los unit tests pasaron. En la corrida, el smoke y la
integración quedaron verdes, la e2e quedó roja y producción ni pidió aprobación. La traza del
reporte mostró que el navegador mandaba `name` y la api contestaba 400. Integración verde y e2e roja
quiere decir que la api está bien y el problema es el front. Después revertí el cambio, y la cadena
volvió a verde hasta producción.

## Integración amplia y no estrecha

Mi integración prueba la api ya desplegada en QA, con su base de verdad. La otra forma era levantar
la api dentro del pipeline con una base descartable. Elegí la de QA porque ya existía, y porque
además prueba que el deploy quedó bien. Lo que pierdo es que me entero después del merge y no en el
PR, y que QA lo comparten todas las corridas.

## Cold start y tests flaky

QA se duerme, así que las pruebas tienen tiempos largos —60 segundos por test— y un reintento, sin
`sleep`. Un test flaky es el que a veces pasa y a veces falla sin que cambie nada, y es peor que no
tener test porque te acostumbra a ignorar el rojo. Me pasó: el test de stock leía la tabla mientras
decía "Cargando…". Lo arreglé esperando la respuesta del servidor antes de leer, y lo probé 9 veces
seguidas contra QA sin reintentos.

## La misma imagen del front en QA y en producción

La imagen es la misma en los dos. Adentro quedan el build de React y la plantilla de nginx. Lo que
cambia va en las variables de cada servicio de Render: `BACKEND_URL`, la dirección de su api. Las
otras dos, `DNS_RESOLVER` y `PORT`, son iguales en los dos entornos. Es lo que se armó en el TP6.

## Límite conocido: un solo QA

Si mergeo dos cambios seguidos, el segundo puede redesplegar QA mientras las pruebas del primero
todavía corren, y esas pruebas ya no valen. Por eso hago un merge por vez.

## Problemas encontrados y cómo los resolví

`getByLabel('Local')` no encontraba el filtro de local porque el `select` está adentro del `label`.
Lo busqué por su rol.

`innerText` devolvía los títulos de las columnas en mayúsculas por el CSS. Con `textContent` anduvo.

La primera vez, los secrets de prueba se cargaron vacíos sin que nada avisara: el comando que los
sacaba falló. Lo vi revisando el largo de cada valor antes de seguir.

## Declaración de uso de IA

Usé IA (Claude) para escribir el código del TP7 —las dos suites de pruebas, los jobs nuevos del pipeline, la rotura del front y su arreglo— y para redactar este documento. La parte de las cuentas la hice yo con la ayuda de la IA, a la que le pedí un paso a paso para cada cosa: cambié la fuente de los cuatro servicios de Render a imagen,  cargué los secrets de las pruebas en el repo y armé la release v7.0.0. También aprobé y rechacé cada despliegue a producción mirando antes la corrida en Actions. El diagnóstico con la traza del reporte lo hizo la IA, y yo miré los resultados.

---

# Decisiones — TP8

## Riel y versiones

Usé Terraform 1.16.6 con los mismos proveedores del TP6, Neon y Render. Fijé `kislerdm/neon` 0.18.0,
`render-oss/render` 1.9.1 y `hashicorp/random` 3.9.1, y el `.terraform.lock.hcl` guarda el hash de
cada uno.

Declaré un entorno nuevo, `preprod`: un proyecto de Neon con su base, y la api y el front en Render
con la imagen de `v7.0.0`. QA y producción no se tocaron.

## Problemas y cómo los resolví

Neon no dejaba crear el proyecto sin `org_id`, porque mi cuenta es una organización y el ejemplo de
la guía no lo trae. Lo agregué como variable.

La api necesitaba más que la cadena de conexión: sin `Jwt__Key` no arranca, y en Production no
aplica las migraciones. La clave la genera Terraform con `random_password`, y los dos interruptores
de migraciones y seed van como variables del servicio.

Guardar el plan con `-out` también guarda las contraseñas, así que agregué `*.tfplan` al
`.gitignore` antes del primer apply.

Al destruir y volver a crear preprod, Render devolvió las mismas URLs, pero Neon devolvió otro host
para la base. Por eso la cadena de conexión se arma sola y no se copia.

## Declaración de uso de IA

Usé IA (Claude) para escribir el código del TP8 —los archivos de Terraform de infra/— y para redactar este documento. Lo que estaba por fuera del código lo hice yo con la ayuda de la IA, a la que le pedí un paso a paso: instalé Terraform, creé la API key de Neon, para que me ayudar si se me complicaba en algo, y busqué el org_id de mi organización, corrí el plan y el apply para crear preprod y después lo destruí, e hice las pruebas de drift y de prevent_destroy mirando en Neon y en Render que pasara lo que el plan decía. También rechacé los dos despliegues a producción de este TP porque no cambiaban la app.

---

# Decisiones — TP9

## El PR de Dependabot que mergeé

Mergeé el #62, que subía `Microsoft.AspNetCore.Authentication.JwtBearer` de 8.0.10 a 8.0.32, la
librería que valida los tokens del login. Me animé porque es un parche, que no cambia la versión
mayor ni la menor; las notas eran sólo arreglos y mejoras de seguridad; tocaba un solo archivo; y lo
probó toda la cadena: los unit tests en el PR, y después la integración y la e2e contra QA, que
hacen login. Los otros doce PR eran casi todos cambios de versión mayor que pueden romper cosas,
como vitest 5, que necesita vite 6.

## El monitor y Sentry

El monitor tardó 5 minutos 33 segundos en avisar. Suspendí producción a las 23:19:04 (UTC), el
aviso de DOWN llegó a las 23:24:37 y el de UP a las 23:29:43. Me llegaron los dos mails.

La operación más usada es `GET /api/stock`, con 18 pedidos. La más lenta es `POST /api/auth/login`,
con un P95 de 2,7 segundos, aunque son pocos pedidos. Lo que más me sirvió fue ver que cada consulta
a la base tarda unos 176 ms, porque la api está en Oregon y la base en São Paulo. Con ese dato abrí
la tarjeta #66.

## Problemas encontrados

CodeQL encontró un bug en un test del TP7: la regex de la e2e de stock tenía `\d` con una sola barra
adentro de un template literal, y ahí `\d` es la letra d. El test aceptaba 12 cuando esperaba 1. Lo
corregí. También marcó que cuatro jobs del pipeline no declaraban permisos, y ahora declaran sólo
lectura.

Me preocupaba que reanudar producción en Render volviera a la imagen vieja de la configuración. No
pasó: volvió con el mismo deploy.

## Declaración de uso de IA

Usé IA (Claude) para escribir el código del TP9 —el dependabot.yml, la integración de Sentry en la app y los arreglos de lo que marcó CodeQL— y para redactar este documento. Todo lo que pasaba por las páginas lo hice yo con la ayuda de la IA, a la que le pedí un paso a paso: prendí las protecciones de seguridad en GitHub, revisé los hallazgos de CodeQL, elegí y mergeé el PR de Dependabot, creé las cuentas de UptimeRobot y de Sentry con sus claves, armé el monitor y provoqué la caída suspendiendo producción en Render, creé la tarjeta en el tablero, e hice el token para la demo del secreto bloqueado y después lo borré. También revisé que me llegaran los mails del monitor y aprobé o rechacé cada despliegue.
