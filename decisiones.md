# Enlaces del TP7

Van acá arriba porque viven afuera del repositorio y la corrección no los busca. Los del TP6
siguen en su sección, más abajo.

**Las dos imágenes, públicas.** Cada merge a `main` publica una por commit, con la etiqueta
`sha-<commit>` de 40 caracteres. La que corre en producción es la del commit al que apunta
`v7.0.0`.

| | |
|---|---|
| Backend | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/pkgs/container/ingsoft3-tp01-backend |
| Frontend | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/pkgs/container/ingsoft3-tp01-frontend |

**La corrida donde la integración quedó verde y la e2e roja**, y la promoción se frenó:

| | |
|---|---|
| El commit que rompió la app | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/commit/247d8bcf6c27b8a079e50edd1e836263317d5def |
| La corrida | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/39529017346 |
| `playwright-report-integracion` (verde) | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/39529017346/artifacts/12314926096 |
| `playwright-report-e2e` (rojo) | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/39529017346/artifacts/12314991331 |

**La corrida completa en verde después del arreglo, hasta producción:**
https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/39531146035

**Los entornos, vivos hasta la defensa:**

| | Front | API |
|---|---|---|
| QA | https://joyeria-front-qa.onrender.com | https://ingsoft3-tp01-jx17.onrender.com |
| Producción | https://joyeria-front-prod.onrender.com | https://ingsoft3-tp01-1.onrender.com |

El plan gratuito duerme los servicios sin tráfico: el primer pedido después de un rato tarda
alrededor de 30 segundos.

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

# Decisiones — TP6

## Enlaces de este TP

| | |
|---|---|
| Imagen del backend | `ghcr.io/matiasamuchastegui/ingsoft3-tp01-backend` |
| Imagen del frontend | `ghcr.io/matiasamuchastegui/ingsoft3-tp01-frontend` |
| QA, en vivo | https://joyeria-front-qa.onrender.com |
| Producción, en vivo | https://joyeria-front-prod.onrender.com |
| La cadena entera, corriendo | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/runs/37993396090 |
| Historial de despliegues | https://github.com/MatiasAmuchastegui/ingsoft3-tp01/deployments |

Las dos imágenes son públicas: se bajan sin credenciales, lo verifiqué con `docker logout` y
`docker manifest inspect`.

## Qué cambió respecto del TP5

Hasta el TP5 el pipeline verificaba y ahí se terminaba. Ahora sigue:

```
Pull Request  →  merge a main  →  QA automático  →  aprobación  →  producción
```

Ningún paso se saltea, y no porque yo lo pida sino porque cada job depende del anterior con
`needs:`. Si los tests fallan no hay imagen; sin imagen no hay QA; sin QA verde no hay producción.
Lo mismo que aprendí en el TP5 con los pasos de un job —el orden es la condición— vale acá entre
jobs.

## El artefacto: una imagen etiquetada con el commit

Cada push a `main` publica las dos imágenes al registry con la etiqueta `sha-<commit>`. No uso
`latest`: `latest` es un nombre que apunta a cosas distintas según el día, y entonces la pregunta
«qué está corriendo en producción» no tiene respuesta. Con el commit en la etiqueta, la respuesta
es un `git show` de distancia.

Las imágenes se publican **al final** de cada job de build, después de los tests. No hay ningún `if`
que diga «si los tests pasaron» porque no hace falta: los steps se cortan al primer error, así que
si los tests salieron rojos el job murió antes. Y el `push:` lleva dos condiciones —evento `push` **y**
rama `main`— en vez de una, para que el día que se agregue otra rama al disparador no se publique
algo que nunca pasó por un Pull Request.

Lo comprobé al revés, que es como se comprueba: en un PR el paso «Entrar al registry» figura como
**skipped** con todo lo demás en verde.

## La misma imagen en los dos entornos

Este fue el cambio de código más grande del TP, y es el que hace posible todo lo demás.

Hasta el TP5 la dirección del backend estaba escrita adentro del `nginx.conf` del frontend:
`http://backend:8080`, el nombre del servicio en el compose. Mientras el único destino fuera Docker
Compose alcanzaba. En la nube ese nombre no existe, y además es **distinto en QA y en producción**.
Con la dirección horneada en la imagen hacen falta dos imágenes, y entonces «se despliega lo mismo
que se probó» es imposible de sostener.

La solución fue convertir el archivo en una plantilla —`default.conf.template`, en
`/etc/nginx/templates/`— con `${BACKEND_URL}` y `${DNS_RESOLVER}`. El entrypoint de la imagen oficial
de nginx reemplaza esas variables al arrancar. Resultado: **los dos frontends corren la misma imagen
y lo único que los diferencia es una variable de entorno.**

Tres cosas que aprendí haciéndolo:

- Si el archivo va a `conf.d/` en vez de `templates/`, nginx lo lee tal cual, con los `${...}` sin
  reemplazar, y no levanta.
- Cuando `proxy_pass` recibe una **variable**, nginx necesita un `resolver` declarado, porque resuelve
  el nombre mientras corre y no al arrancar. Sin esa línea no arranca. La contracara buena es que
  arranca aunque el backend esté caído.
- Con una variable, nginx **no** recorta la parte de la URI que coincidió con el `location`: pasa la
  ruta completa. Por eso hubo que sacar el `/api/` del final que llevaba la versión vieja.

También acoté con `NGINX_ENVSUBST_FILTER` qué variables se reemplazan. Sin eso sustituye todas las
del entorno, y si alguna se llamara como una de nginx —`$host`, `$uri`— la configuración saldría
rota de una forma muy difícil de leer.

## Quién dispara el deploy

**El auto-deploy de Render está apagado en los cuatro servicios.** Render sabe desplegarse solo en
cada push, y es lo que viene activado por defecto. Lo apagué a propósito: con el auto-deploy puesto,
el despliegue no pasa por el pipeline —no espera al CI en verde ni a ninguna aprobación— y toda la
cadena de arriba deja de significar algo.

El deploy lo dispara el workflow, con un POST al *deploy hook* de cada servicio. Y los hooks llevan
**`&ref=$GITHUB_SHA`**, que no es un detalle: sin eso Render despliega la punta de `main`, que para
cuando el hook llega puede tener commits que este pipeline nunca verificó.

### Limitación conocida

Render construye la imagen **desde el repositorio**, no se baja la que el pipeline publicó en el
registry. O sea que lo que corre en QA y en producción no son los mismos bytes que se verificaron,
sino una reconstrucción del **mismo commit**, fijado con `&ref`. Mismo código y mismo Dockerfile, pero
no el mismo artefacto.

Lo dejo anotado porque es la diferencia entre «promover el artefacto» y «reconstruir el commit», y la
segunda es más débil. Render permite crear un servicio que despliegue una imagen de un registry en
lugar de un repo; cerrarlo así implica rehacer los cuatro servicios.

## Los dos entornos están separados de verdad

Cuatro servicios en Render —api y front por entorno— y **dos bases distintas en Neon**, `app_qa` y
`app_prod`. También cambia la `Jwt__Key`.

Eso último no es decoración. En QA la contraseña de la administradora es la del seed, `Admin123!`,
y es pública: está en el código. Si la clave de firma fuera la misma en los dos entornos, cualquiera
podría pedir un token en QA y entrar a producción con él.

Lo verifiqué en vez de suponerlo. Pedí un token por el front de QA y lo presenté en los dos lados:

```
token emitido en QA  →  contra QA     HTTP 200
token emitido en QA  →  contra PROD   HTTP 401
```

## La aprobación, y por qué esto es entrega continua y no despliegue continuo

`deploy-qa` corre solo. `deploy-prod` declara `environment: production`, que en el repo tiene un
revisor obligatorio, y se queda esperando.

Ahí está la diferencia de los dos términos. El pipeline **deja todo listo** para desplegar en
producción con cada cambio verificado: eso es entrega continua. Apretar el botón es una decisión de
negocio, no técnica. Sacando el revisor, esto mismo pasa a ser despliegue continuo **sin tocar una
línea del workflow** — la diferencia no está en el código, está en una casilla.

El dato que mejor lo muestra: en la corrida que rechacé, el job `deploy-prod` ejecutó **cero steps**.
No corrió y lo cancelaron a mitad: nunca arrancó. La aprobación no es un paso adentro del job, es
una puerta antes del job.

### El rechazo

Rechacé la primera corrida con este motivo:

> Es la primera corrida del pipeline de despliegue y QA recién terminó de levantar. Quiero verificar
> a mano el login y el listado de stock en QA antes de tocar producción.

Producción no se tocó. El entregable de ese ejercicio es que **no pasó nada**, y que quedó registrado
quién lo decidió y por qué.

## La prueba de humo

Un deploy puede terminar «exitoso» y dejar la app sin base, sin configuración o sirviendo una página
en blanco. Los tests del CI ya corrieron sobre el código y probaron la lógica; el humo prueba otra
cosa: que lo que quedó arriba, arriba y conectado, responde.

Son cuatro comprobaciones en orden, y el orden importa porque aísla el problema:

| | Qué descarta si pasa |
|---|---|
| `GET /` en el front | nginx levantó y el bundle está |
| `GET /nginx-health` | nginx responde por sí mismo, sin el backend |
| `GET /health` en la api | el backend vive **y** ve la base — el healthcheck incluye `AddDbContextCheck` |
| `POST /api/auth/login` por el front | el camino real: nginx → proxy → backend → base |

El último es el que vale, y es el único endpoint que toca datos sin pedir un token antes, así que es
el único que se puede probar desde afuera.

**Es el mismo script para los dos entornos**, con otras URLs. Que sea el mismo archivo no es ahorro de
tipeo: es la garantía de que producción se valida con el mismo criterio con el que se validó QA.

### Limitación conocida

En la primera corrida los cuatro chequeos pasaron en el intento 1, a los 45 segundos. Render tarda
varios minutos en construir una imagen, así que lo más probable es que esas respuestas las haya dado
el contenedor **viejo**, que sigue sirviendo mientras el nuevo se construye.

O sea que hoy el humo prueba que el entorno está sano, pero no que la versión nueva esté arriba. El
cierre es preguntarle a la API de Render por el `deploy id` que devuelve el hook hasta que diga
`live`, y recién ahí correr las pruebas. Queda pendiente.

## Dos detalles del workflow que no se ven

**`concurrency: deploy-prod`.** Dos merges seguidos largan dos despliegues. Sin encolarlos se pisan, y
producción puede terminar con la versión **más vieja** de las dos — la que arrancó primero pero
terminó después.

**Los secrets se llaman igual en los dos entornos.** `RENDER_HOOK_API` existe en `qa` y en
`production` con valores distintos. Por eso los dos jobs son idénticos salvo por las URLs: el job no
elige el entorno, lo declara con `environment:`, y GitHub le entrega los secrets de ése y sólo de
ése. La consecuencia que importa es la de seguridad: **el hook de producción no se puede leer desde
el job de QA.** Los cargué con `gh secret set ... --env <entorno>` sin `--body`, que los pide por
teclado, para que no queden en el historial de la terminal.

## Lo que salió mal

**El pipeline se cayó por algo que no era mío.** El build del frontend empezó a morir a los 20
segundos con `429 Too Many Requests` al bajar `node:22-alpine`. Docker Hub limita las descargas
anónimas por IP y los runners de GitHub comparten IP entre miles de proyectos: no era cuota mía, así
que reintentar no servía — lo intenté dos veces con minutos de por medio. Lo resolví configurando
buildx para bajar las imágenes de Docker Hub por un espejo público. La configuración va en el
workflow y **no** en los Dockerfiles: el problema es de ese entorno, no de la definición de la imagen,
que tiene que seguir funcionando igual en cualquier máquina. El frontend pasó de morir en 20s a
construir en 45s.

**La cadena de conexión de Neon no entra como viene.** Neon la da en formato URI
—`postgresql://usuario:clave@host/base`— y Npgsql no la acepta: quiere `clave=valor`. El error,
`Format of the initialization string does not conform to specification starting at index 0`, no dice
nada de formatos de cadena y mandó a buscar al lado equivocado. Neon la ofrece ya convertida eligiendo
«.NET» en el desplegable del panel de conexión. Dato al pasar: en ese fragmento Neon la llama
`DefaultConnection` y mi app la busca como `Default`.

**Las variables de .NET van con doble guión bajo.** `ConnectionStrings__Default` y `Jwt__Key`, no
`JWT_KEY`. El doble guión bajo es cómo .NET escribe los dos puntos de `ConnectionStrings:Default` en
una variable de entorno. Con un guión solo la app no la encuentra y arranca como si no existiera.

**Las migraciones no corrían en Render.** El código decide si migrar y sembrar con
`app.Environment.IsDevelopment()` como valor por defecto, y Render corre en Production: por defecto
quedaba en `false` y la base se quedaba vacía, sin tablas y sin usuaria administradora. Hay que poner
`AplicarMigracionesAlArrancar=true` y `SembrarDatosIniciales=true` explícitamente en cada servicio.

## Lo que medí

La corrida completa, de merge a producción:

| | |
|---|---|
| `build-backend` | 1m 27s |
| `build-frontend` | 0m 31s |
| `deploy-qa` | 0m 56s |
| *(esperando mi aprobación)* | 1m 47s |
| `deploy-prod` | **0m 56s** |

Los 56 segundos de `deploy-prod` son el número que importa: el reloj del job arranca **en el momento
en que apruebo**, porque mientras espera permiso el job no está corriendo. Así que esa duración es
exactamente el tiempo entre que aprieto *Approve* y que producción queda verificada.

De esos 56 segundos, 45 son la pausa que puse a mano en el script de humo. El resto es disparar los
dos hooks y las cuatro comprobaciones. Lo cual, dicho sea de paso, confirma la limitación que anoté
más arriba: producción contestó antes de que Render pudiera haber terminado de construir.

El minuto y 47 de espera es tiempo mío, no del sistema. En un equipo de verdad eso puede ser un día.
Es la parte que el pipeline no puede acelerar, y es a propósito.

## Comprobé que QA y producción no comparten base

Las dos listas de categorías daban igual —`Anillos, Aros, Collares, Pulseras`— pero eso no prueba
nada: las dos bases arrancan del mismo seed, así que coincidirían igual si fueran la misma. Hace
falta un dato que exista en una sola.

Creé una categoría sólo en producción y volví a listar las dos:

```
POST /api/categorias en PROD → 201  (MARCA-SOLO-PROD)

QA    → Anillos, Aros, Collares, Pulseras
PROD  → Anillos, Aros, Collares, MARCA-SOLO-PROD, Pulseras
```

Dos bases, confirmado. Lo dejo escrito porque la primera comparación era la trampa: parecía una
verificación y no verificaba nada.

## El plan gratuito apaga los servicios

Once días después del despliegue, el primer pedido por el frontend dio **502**. Los dos backends
estaban dormidos —Render apaga los servicios sin tráfico en el plan gratuito— y tardaron **33
segundos** en despertar. Después de eso, todo normal.

No es un error de la app, pero cambia dos cosas prácticas:

- El `proxy_read_timeout` de nginx está en 75s justamente por esto. Con los 30s que tenía en el TP2,
  el primer pedido después de un rato cortaba siempre.
- El script de humo arranca con una pausa y reintenta 30 veces cada 20 segundos. Un smoke test sin
  reintentos daría rojo en un entorno que está perfecto, sólo porque estaba dormido.

Vale la pena saberlo antes de abrir la app en una defensa: conviene despertarla unos minutos antes.

## El rollback, y los dos números que no son el mismo

Volví producción a la versión anterior desde Render —*Deploys* → el deploy previo → *Rollback to
this version*— y medí las dos cosas a la vez: lo que tardó Render, y lo que estuvo caída la app
vista desde afuera.

| | |
|---|---|
| Lo que tardó el rollback, según Render | **~11 s** |
| Lo que estuvo sin responder, consultándola desde afuera | **0 s** |

La segunda la medí con una sonda que pedía la página cada 2 segundos y anotaba cada cambio de
estado. No anotó ninguno: devolvió `200` de principio a fin.

No son el mismo número y la diferencia es el punto. Render no apaga el contenedor viejo para prender
el nuevo: lo deja sirviendo hasta que el nuevo está sano, y recién ahí mueve el tráfico. Los 11
segundos son trabajo suyo; quien estaba usando la app no vio nada.

Dos aclaraciones que prefiero dejar escritas:

- La sonda muestrea cada 2 segundos, así que un corte más breve se me habría escapado. Lo que puedo
  afirmar es que no hubo caída de 2 segundos o más.
- Entre las dos versiones sólo cambiaba este documento, así que el rollback no tenía nada visible
  que revertir. Mide la **capacidad** de volver atrás y su costo en tiempo, que es lo mismo sea cual
  sea el cambio, pero no demuestra un comportamiento distinto antes y después.

### Por qué el rollback es rápido y el despliegue no

Un despliegue normal tarda minutos y este rollback tardó segundos, y la razón es la misma limitación
que anoté más arriba: **Render construye desde el repositorio.** Desplegar implica compilar la imagen
de cero. Volver atrás, no: esa imagen ya existe, así que es apuntar a ella.

O sea que el camino rápido para volver atrás existe precisamente porque Render guarda sus builds
anteriores. Si el rollback se hiciera por el pipeline —relanzando el `deploy-prod` de una corrida
vieja— tardaría lo mismo que un despliegue, porque volvería a construir. La contrapartida es que ese
camino sí pasa por la aprobación y queda registrado en el repo, y el botón de Render no.

## Declaración de uso de IA

Usé IA (Claude) para escribir la plantilla de nginx, los jobs de deploy, el script de humo y para
redactar este documento. Lo que hice y decidí yo:

1. **Armé los cuatro servicios y las dos bases** — Neon, los cuatro servicios en Render, sus variables
   de entorno, y apagar el auto-deploy en cada uno.
2. **Decidí qué entorno lleva revisor y cuál no.** QA automático y producción con aprobación: esa
   asimetría es la que hace que esto sea entrega continua.
3. **Rechacé un despliegue con un motivo mío** y aprobé otro, y miré qué queda registrado de cada uno.
4. **Cargué los cuatro hooks como secrets de entorno**, no del repositorio, para que el de producción
   no sea legible desde el job de QA.
5. **Verifiqué el aislamiento de los entornos** con el cruce de tokens, en vez de darlo por hecho
   porque las URLs son distintas.
6. **Hice el rollback y lo medí por los dos lados**, el de Render y el del usuario. Los números no
   coincidieron, y ésa terminó siendo la parte interesante del ejercicio.

Las dos limitaciones de arriba —que Render reconstruye en vez de bajar la imagen publicada, y que el
humo puede estar midiendo el contenedor viejo— las dejo escritas a propósito. Las encontré mirando
los tiempos de la primera corrida, y prefiero tenerlas anotadas antes que sostener que la cadena es
más fuerte de lo que es.

# Decisiones — TP7

## Qué cambió respecto del TP6

En el TP6 el pipeline ya publicaba una imagen por commit, pero Render no la usaba: con el
`&ref=` del hook, **reconstruía** la app desde el repositorio en cada deploy. Lo dejé anotado
como limitación, y este TP la cierra. La cadena quedó así:

```
PR → merge → build + tests → imagen sha-<commit>
   → QA ejecuta ESA imagen → smoke
   → integración contra la api de QA → e2e contra el front de QA
   → aprobación → producción ejecuta LA MISMA imagen → smoke
```

## Build once, deploy many

En el TP6 cada commit se construía tres veces: una en el pipeline —la que pasaba los tests—,
otra cuando Render desplegaba QA y otra cuando desplegaba producción. Los tres builds salían
del mismo commit, pero nada garantizaba que salieran iguales.

El ejemplo lo tenía en mis propios Dockerfiles. Arrancan de etiquetas que se mueven:
`mcr.microsoft.com/dotnet/sdk:8.0`, `aspnet:8.0`, `node:22-alpine`, `nginx:1.27-alpine`. Cada
una apunta a la última versión publicada de esa base. El pipeline las resolvía en su build
—algunas a través de un espejo, por el 429 de Docker Hub del TP6—, y Render volvía a resolverlas
en el suyo, minutos después. Si en el medio salía una versión nueva de alguna, QA y producción
corrían una imagen que mis tests nunca vieron, del mismo commit, y yo no tenía forma de
demostrar lo contrario. Fijar la
versión en el `FROM` achicaría el problema, pero seguiría construyendo dos veces.

Ahora se construye **una vez**. La imagen que pasó los tests es la que se publica, y QA y
producción ejecutan ésa, bit a bit. El hook dejó de decir «construí el commit X» y pasó a decir
«ejecutá la imagen `sha-X`».

Se nota también en el tiempo. El primer servicio que pasé a imagen quedó *live* en **11
segundos**; reconstruyendo desde el repo, en el TP6, eran minutos.

## Las etiquetas

| | Dónde vive | Qué garantiza |
|---|---|---|
| `sha-<commit>` | el registry, en las dos imágenes | de qué commit salió esa imagen. Es la que viaja en el hook |
| `v7.0.0` | git | el nombre de una versión que anduvo en producción |

**Se promueve por `sha-<commit>`.** Es lo que el hook manda, y como backend y frontend salen de
la misma corrida, llevan la misma etiqueta: entre una y otra sólo cambia `-backend` por
`-frontend`.

**El pipeline no publica `latest`.** `latest` es un puntero, no una versión: dice «lo último
que se publicó», haya pasado las e2e o no. Un deploy que lo nombrara no podría decir qué
desplegó. Con una sola etiqueta por imagen, la de su commit, no hay forma de desplegar algo sin
decir exactamente qué es.

**Las etiquetas igual se pueden mover.** Volver a correr la corrida de un commit publicaría otra
vez `sha-<ese commit>`, y el contenido podría no ser el mismo. Por eso una corrida vieja no se
relanza para desplegar. Lo único que nadie puede mover es el digest (`@sha256:…`), la huella
del contenido; no lo uso porque habría que pasarlo de un job a otro, y la etiqueta del commit se
arma sola.

### Con qué imagen configuré Render, y por qué no es la que corre

A los cuatro servicios les cambié la fuente a *Existing Image* con la API de Render, sin
recrearlos, así que conservan URL, variables, deploy hooks e historial. Comprobé que los
cuatro conservaran sus variables, comparando los nombres —los valores no los imprimí: ahí está
la cadena de conexión—. Cambiar la fuente no desplegó
nada: siguieron corriendo lo último que habían construido hasta que los desplegó el pipeline.

La imagen que les puse es la del último merge del TP6, `sha-7251717…`, que antes había bajado
sin credenciales. Pero es sólo el punto de partida. Render exige que el hook nombre el mismo
registry y la misma imagen, y deja cambiar la etiqueta, así que cada deploy del pipeline manda la
suya. La configuración queda clavada en `sha-7251717…` y el servicio corre la del último merge.

¿Cómo sé cuál corre sin entrar al panel? En **Deployments** del repo: cada entorno muestra el
commit de su último deploy, y la imagen es `sha-<ese commit>`.

Esto abre una trampa: el botón *Manual Deploy* de Render despliega la imagen de la
configuración, no la que está corriendo. Apretarlo volvería producción a `sha-7251717…`, en verde
y sin avisar. Si hace falta redesplegar, se hace desde una corrida.

## Del tag a la imagen, en un paso

`git rev-list -n1 v7.0.0` da el commit, y las dos imágenes de ese commit están en los paquetes
como `sha-<commit>`. Volver a esa versión sería mandarle esa imagen al hook, con el mismo
mecanismo del deploy. No lo practiqué acá: el rollback fue del TP6.

## Cómo se comprueba desde afuera que un entorno ejecuta la imagen

Tienen que coincidir tres lugares:

1. **El log de la corrida**: el `imgURL=…:sha-<commit>` de cada deploy y la respuesta del hook,
   que trae el id del deploy.
2. **Render**: ese mismo id, *Triggered via Deploy Hook*, ejecutando la imagen `sha-<commit>`.
3. **El paquete**: la etiqueta existe.

Lo comprobé en la primera corrida que desplegó imágenes. Los ids que devolvió el hook en el log
—`dep-dbc0nguq…` en la api y `dep-dbc0nhjn…` en el front— eran los que Render tenía *live*, con
trigger `deploy_hook` y la imagen `sha-a32c3b8…` del merge. Y en producción, después de
aprobar, el mismo `imgURL` que en QA.

**Lo que el smoke no alcanza a probar es justamente eso.** El smoke pregunta «¿respondés?», y
mientras Render levanta la imagen nueva, la vieja sigue contestando. Si un deploy fallara, el
servicio seguiría sirviendo la versión anterior y el smoke daría verde igual. Cerrarlo
automáticamente necesita dos mitades: que la imagen lleve horneado el commit que la produjo, y
que el smoke compare el que devuelve la api con el de la corrida. Queda como concepto.

En la corrida roja lo revisé a mano, porque ahí importaba: el front de QA quedó *live* con
`sha-247d8bc…` a las 01:45:33 (hora UTC), y la e2e arrancó a las 01:46:43. Probó la imagen rota,
no la anterior.

## Las dos suites

Las dos corren con Playwright contra QA desplegado, y viven en `frontend/e2e/`.

**Integración** (`api.spec.ts`): le habla a la api de QA directo, sin navegador, con el fixture
`request`, que es un cliente HTTP.

| Prueba | Qué comprueba |
|---|---|
| Alta y borrado de una categoría | `POST` → 201; un `GET` aparte la encuentra; `DELETE` → 204; otro `GET` ya no la encuentra |
| Nombre vacío | la api contesta 409 con el motivo, y la cantidad de categorías no cambió |
| Entrada y venta de stock | la entrada sube el stock en la base, el movimiento quedó guardado con su fecha, y la venta lo deja como estaba |

**E2E** (`flujos.spec.ts`): la app con Chromium, como la usa una persona.

| Prueba | Qué comprueba |
|---|---|
| Crear una categoría | aparece en la lista; eliminarla la saca |
| Un prefijo que ya usa otra categoría | el usuario ve el error que habla de *su* prefijo, y al recargar no se creó nada |
| Entrada y venta de stock, desde la pantalla | la cantidad sube a la vista, y después vuelve a la de antes |

Las que crean datos usan un nombre con la hora adentro, para que ninguna otra corrida use el
mismo, y comprueban al final que lo que crearon ya no está. Las credenciales llegan por secrets:
ningún archivo del repo las tiene.

### La tercera de cada una

En las dos elegí el movimiento de stock. Es lo que se hace todos los días en el mostrador —la
pantalla de Stock es la principal de la app—, y si mañana no anda, el que escribe es la sucursal
que no puede registrar una venta. Además toca la base en el lugar más delicado: el movimiento
guarda su fecha en una columna `timestamptz`, que Npgsql sólo acepta escribir en UTC. Si alguien
cambiara la hora del registro por la local, la api contestaría 500 en el alta, y ningún unitario
lo vería: no hay ninguna base en ellos que pueda rechazar la fecha.

Y es la misma en las dos a propósito. La lectura de los colores, que viene más abajo, sólo vale
si la integración prueba los endpoints que usa la e2e. Si la e2e de stock se pusiera roja y la
integración no probara movimientos, su verde no diría nada sobre la api de stock, y le echaría
la culpa al front sin motivo.

### Los movimientos no se borran: se compensan

Las categorías se crean y se borran. Los movimientos no se pueden borrar, y está bien que sea
así: en un sistema de stock la historia no se borra, se compensa, como en un libro contable. Por
eso las pruebas de stock registran una entrada y una venta de una unidad, y lo que comprueban
al final es que la cantidad volvió a la de antes. En el historial de QA quedan los dos
movimientos, identificados con una observación que empieza con `api` o `e2e`.

Comprobé que las corridas repetidas no se ensucian entre sí: después de nueve corridas de la e2e
seguidas contra QA, seguían las cuatro categorías del seed y el stock del producto en 11, igual
que al empezar.

### Nombre vacío: 409 y no 400

El enunciado espera un 400 para el título vacío, que es lo que contesta la app de la cátedra.
La mía contesta **409**, y el test fija eso. Lo comprobé antes de escribirlo: un nombre vacío da
409, y sólo un pedido *sin* el campo da 400, por la validación automática.

Pasa porque el middleware de errores traduce toda `ReglaNegocioException` a 409, en un solo
lugar, y el nombre vacío se valida como regla de negocio. Es discutible: HTTP reserva el 409
para conflictos con el estado de los datos —un prefijo repetido, una categoría con productos—, y
un nombre vacío no es eso, es un dato inválido, que debería ser 400. No lo cambié: hacerlo
bien es separar en toda la api los datos inválidos (400) de los conflictos (409), y eso cambia
la respuesta de la api y la excepción que esperan cinco tests del TP5 con su tipo exacto.
Queda anotado como deuda.

### Qué no puse en cada suite

La pirámide reparte lo que va en cada capa, y lo que ya prueba una capa de abajo no se repite
arriba, donde cuesta más:

- **En ninguna de las dos**: las reglas que ya cubren los unitarios del TP5 —normalizar el SKU,
  la coherencia entre rol y local, la matriz de permisos por local, los claims del JWT, el
  cálculo del stock resultante—. Ahí corren en milisegundos y prueban todos los bordes; una e2e
  tardaría minutos en probar cada uno.
- **En la integración**: nada de pantalla, y un solo caso de error. Las variantes del formato
  del prefijo ya las prueba el unitario de `GeneradorSku`; acá alcanza con fijar una vez el
  contrato del error.
- **En la e2e**: las validaciones que hace el formulario solo. Con un nombre vacío el botón
  *Guardar* ni se habilita, así que ese pedido nunca llega a la api, y la e2e está para probar
  el camino entero. Por eso el error elegido es el prefijo repetido, que sí da la vuelta
  completa.
- **Productos y transferencias, afuera de las dos**: los productos no se borran, se dan de baja,
  así que una prueba que los creara dejaría uno nuevo en cada corrida.

## Integración y e2e no prueban lo mismo

| | Qué ve | Qué no puede ver |
|---|---|---|
| Unitario (TP5) | una regla, sola | lo que contesta la base de verdad: en su lugar hay un doble |
| Integración | que la api desplegada y su base se entienden | si la pantalla usa bien esa api |
| E2E | el sistema entero, como lo usa una persona | lo que no recorren sus tres flujos |

Juntas no dan sólo una alarma: dan un diagnóstico. Para la evidencia rompí **el front** a
propósito, en un commit que toca sólo código de la app y nada de `e2e/`: renombré `nombre` a
`name` en el tipo que el front manda al crear una categoría, como podría pasar en un refactor.
TypeScript compiló, porque el contrato con el backend no está tipado de punta a punta, y los 31
unitarios del front siguieron en verde, porque ninguno toca esa pantalla.

La corrida:

```
build ✓ → deploy-qa ✓ (smoke verde) → integración ✓ → e2e ✗ → deploy-prod ni pidió aprobación
```

En la e2e fallaron los dos tests de categorías —también en su reintento— y pasó el de stock.
El diagnóstico lo saqué del reporte rojo, no del código. La traza del test fallido, en su
pestaña *Network*, muestra:

```
POST /api/categorias → 400
  el navegador mandó: {"name":"e2e 1792547260080","prefijoSku":"TGCU00"}
  la api contestó:    {"errors":{"Nombre":["The Nombre field is required."]}}
```

Leído con la tabla del §2.5: **integración verde y e2e roja quiere decir que el roto es el
front.** A la api le hablé directo, con el nombre correcto del campo, y creó, encontró y borró
en su base sin problema. La que no la usaba bien era la pantalla, que mandaba un campo que la
api no conoce. Y el smoke en verde es parte del punto: el sistema respondía, y aun así no se
podía dar de alta una categoría.

Si hubiera roto la api, la integración habría quedado roja y la e2e **no habría corrido**: su
`needs` no se cumple, y no tiene sentido gastar un navegador en confirmar algo que ya se sabe.
Esa sería la otra fila de la tabla, y también dice algo: el roto es la api o la base.

Lo que la cadena **no** ataja: lo que no recorren los tres flujos. Si mañana se rompe la
transferencia entre locales, las dos suites siguen verdes.

El arreglo fue revertir ese commit. La cadena completa volvió a verde y, con la aprobación,
producción recibió la misma imagen que verificaron QA y las dos suites.

## Por qué la integración amplia y no la estrecha

La integración puede escribirse de dos formas. La *estrecha* arma la api adentro del pipeline,
contra un Postgres descartable que nace y muere con la corrida. La *amplia*, la que elegí, le
habla a la api ya desplegada en QA, con su base de verdad.

| | Amplia (la mía) | Estrecha |
|---|---|---|
| Qué prueba además | que el despliegue quedó bien: la imagen, las variables, la cadena de conexión | — |
| Configuración | ninguna nueva: QA ya existe | `WebApplicationFactory` y un Postgres como `services:` del job |
| Cuándo avisa | después del deploy a QA, minutos más tarde | antes del deploy, y también en los PR |
| Aislamiento | comparte QA con las demás corridas | cada corrida tiene su base |
| De qué depende | de que Render y Neon estén arriba | sólo del runner |

Elegí la amplia porque ya tenía QA ejecutando la imagen exacta de cada corrida: el mismo
concepto con una fracción de la configuración, y probando además lo que la estrecha no puede,
que es el despliegue. Lo que pierdo es feedback temprano: un error de la api se ve recién
después del merge, y no en el PR.

## El cold start, y el flaky que sí apareció

QA está en el plan gratuito, que duerme los servicios sin tráfico. La e2e lo aguanta sin
`sleep` y sin saltearse nada:

- **Topes largos**: 60 segundos por test y 15 por aserción. Playwright espera lo que busca
  hasta ese tope; no hay esperas fijas.
- **Un reintento**, que absorbe una demora suelta de la red.
- **QA ya está despierto** cuando la e2e arranca: el smoke y la integración le pegaron antes.

Un **test flaky** es el que pasa y falla sin que cambie el código. Es peor que no tener test:
cada rojo que «se arregla solo» entrena al equipo a ignorar el rojo, y el día que es de verdad
nadie lo mira. El reintento no lo esconde: el reporte marca *flaky* cualquier test que pasó
recién al reintentar, aunque la corrida quede verde.

Y me pasó. En la primera corrida completa, la e2e de stock falló por timeout y pasó al
reintentar: `2 passed · 1 flaky`. El log del intento fallido mostraba que el test buscaba
`getByRole('row', { name: 'Cargando…' })`: había leído el SKU de la primera fila justo
mientras la tabla recargaba, y tomó «Cargando…» como si fuera un SKU. Contra el compose local,
que contesta en milisegundos, la carrera no aparecía nunca; contra Render, sí.

No esperé a que se repitiera: el log ya decía que el problema era el test y no el plan gratuito.
Ahora el test espera la respuesta del pedido que dispara el filtro, y a que la tabla deje de
decir «Cargando…», antes de leer. Lo verifiqué contra QA, sin reintentos y repitiendo cada test
tres veces: 9 de 9. La corrida siguiente dio `3 passed` en las dos suites.

Por lo mismo las pruebas corren de a una (`workers: 1`): las dos suites usan la misma base y las
dos mueven stock. En paralelo, los movimientos de una le cambiarían la cantidad a la otra a
mitad de la cuenta.

## La misma imagen del front en QA y en producción

Se ve en Render: los dos fronts ejecutan `frontend:sha-<commit>`, con la misma etiqueta. Lo hizo
posible el cambio del TP6, cuando la dirección del backend salió del `nginx.conf` y pasó a ser
una plantilla que nginx completa al arrancar.

| Adentro de la imagen, igual en los dos | Por variable del servicio, distinto en cada uno |
|---|---|
| el bundle de React, compilado con la api en el mismo origen | `BACKEND_URL`: la api de *su* entorno |
| la plantilla de nginx con `${BACKEND_URL}` y `${DNS_RESOLVER}` | `DNS_RESOLVER` |
| la configuración de nginx: caché, proxy, rutas de la SPA | `PORT` |

## Límite conocido: un solo QA para todas las corridas

**Qué es.** QA es uno solo y lo comparten todas las corridas. Con dos merges seguidos, la
corrida B puede redesplegar QA mientras la integración o la e2e de la A todavía lo están
usando. La ventana es más larga que en el TP6, porque ahora son dos jobs seguidos usando QA.

**Cómo lo reconocería.** Un rojo en las pruebas de A que no es de mi código, o algo peor que no
avisa: un verde que en parte probó la imagen de B. Para saberlo miro en Render qué imagen corre
QA, y en *Actions* a qué hora corrió el `deploy-qa` de B: si fue con las pruebas de A en el
medio, esas pruebas no valen. Si dudo de un rojo, desempatan las pruebas de B, que traen los dos
cambios.

**Qué haría.** Rechazar la A con el motivo escrito, si llega a pedir aprobación: la que vale es
la B. Eso es mío, no lo hace el `concurrency`, que cancela lo que está en cola, y un job
esperando aprobación no está en cola. Tampoco re-correría las pruebas de A, porque QA ya tiene la
imagen de B. Y la regla que seguí todo el TP: **un merge por vez**, esperando a que la corrida
anterior termine su integración y su e2e. Un equipo real lo resuelve con un entorno por corrida,
que nace y se destruye con ella.

## Dos decisiones del workflow

**El smoke dejó de hacer login.** En el TP6 probaba la cadena entrando con la contraseña de la
administradora, escrita en el script. La misma cadena —nginx, proxy, api, base— la recorre
`/health` pedido a través del front, sin credenciales. El login de verdad lo prueba la e2e, que
para eso está.

**Los reportes duran hasta la defensa.** Cada suite publica su reporte como artefacto con su
propio nombre, también cuando falla, y el resumen de la corrida dice cuántas pruebas pasaron,
fallaron o se saltearon. La guía sugiere `retention-days: 7`; no lo puse, porque con eso el
reporte de la corrida roja, que es un entregable, habría vencido antes de la corrección. Con el
default de GitHub duran 90 días.

## Problemas que encontré y cómo los resolví

**El `pull` sin credenciales tenía trampa.** Al hacer `docker logout ghcr.io` antes del
checkpoint, Docker contestó *Removing login credentials for ghcr.io*: tenía una sesión vieja
del TP2. Sin el logout, el `pull` habría funcionado igual y no habría probado que los paquetes
son públicos.

**Un selector que no encontraba el filtro de local.** `getByLabel('Local')` no daba con el
`<select>`, porque está *adentro* de su `<label>` y el texto de la etiqueta incluye las opciones.
Lo encontré por su rol y su nombre accesible, `getByRole('combobox', { name: 'Local' })`, que es
como lo anuncia un lector de pantalla.

**`innerText` devuelve el texto como se ve.** El test buscaba la columna «Cantidad» y no la
encontraba, porque el CSS pone los encabezados en mayúsculas e `innerText` devolvía
«CANTIDAD». Con `textContent`, que lee el HTML sin estilos, anduvo. Agregué una aserción para
que, si algún día falta la columna, el test lo diga en vez de comparar contra `NaN`.

**Secrets que se cargaron vacíos.** La primera vez que cargué las credenciales de prueba, el
comando que las extraía falló sin que se notara, y `gh secret set` aceptó un valor vacío sin
quejarse. Lo vi porque mostré el largo de cada valor antes de mandarlo. Si no, recién lo habría
visto en el pipeline, con la suite fallando al arrancar.

**La api de QA dormida.** Varias veces el primer pedido a través del front dio 502 mientras la
api se despertaba, que tardó unos 33 segundos. Por eso las pruebas tienen topes largos y el
smoke reintenta, y por eso conviene despertar los dos entornos unos minutos antes de mostrarlos.

## Declaración de uso de IA

(pendiente: lo completo yo)

# Decisiones — TP8

## Riel y versiones

Riel canónico: **Terraform** contra los mismos proveedores del TP6, **Neon** y **Render**. El
entorno declarado es uno nuevo, `preprod`, en `infra/`. QA y producción no se tocaron.

| | Versión fijada | |
|---|---|---|
| Terraform | 1.16.6 (`required_version = ">= 1.5"`) | la cátedra midió con 1.15.8 |
| `kislerdm/neon` | 0.18.0 | la última publicada; su repositorio está archivado y `init` lo informa como *self-signed* |
| `render-oss/render` | 1.9.1 | la última publicada; firmado por un partner de HashiCorp |
| `hashicorp/random` | 3.9.1 | para la clave JWT de la api (ver abajo) |

El `.terraform.lock.hcl` fija además el hash de cada uno. Preprod corre la imagen de `v7.0.0`,
la misma que producción, y su base está en la misma región (`aws-sa-east-1`) y con la misma
versión de Postgres (18) que la de producción.

## Problemas y cómo los resolví

**Neon no creaba nada sin `org_id`.** La API contestó *«org_id is required»*: la cuenta de Neon
es una organización, y el ejemplo de la guía no lo trae. Agregué `org_id` al `neon_project` como
variable; es un identificador, no un secreto.

**La api necesitaba más que la cadena de conexión.** La guía le pasa sólo
`ConnectionStrings__Default`, pero mi api no arranca sin `Jwt__Key`, y en Render corre en
Production, donde por defecto no aplica migraciones ni siembra datos. La clave la genera
Terraform con `random_password`: nadie la escribe, vive sólo en el estado y es distinta de la de
QA y de la de producción. Los dos interruptores de migraciones y seed van como variables del
servicio. Con eso, la base, que nace vacía, tiene sus tablas y sus datos iniciales la primera
vez que la api arranca.

**El `.gitignore` de la guía no cubría los planes guardados.** Apliqué siempre un plan guardado
con `-out`, para ejecutar exactamente lo que había revisado, y ese archivo también lleva los
valores sensibles. Agregué `*.tfplan` antes del primer apply.

**Lo que medí y la guía dejaba abierto.** Al destruir y recrear, Render devolvió **las mismas
URLs**, porque los nombres habían quedado libres. Neon, en cambio, devolvió **otro host** de base.
Ésa es la prueba de por qué la cadena se arma y no se copia: pegada a mano la primera vez,
preprod habría renacido apuntando a una base que ya no existe.

**Los servicios de Render no se modifican.** El provider no puede actualizar un servicio del plan
gratuito, así que en Render sólo se crea y se destruye. No hizo falta cambiar ninguno: la segunda
pasada, con preprod completo, dio *No changes* también en Render.

## Declaración de uso de IA

(pendiente: lo completo yo)

# Decisiones — TP9

## El PR de Dependabot que mergeé

Dependabot abrió trece PRs apenas entró el `dependabot.yml`. Mergeé el **#62**:
`Microsoft.AspNetCore.Authentication.JwtBearer` de **8.0.10 a 8.0.32**, la librería que valida los
tokens del login.

Me animé por tres cosas. Es un **parche** en SemVer: no cambia ni la versión mayor ni la menor, y
las notas de 8.0.11 a 8.0.32 son todas de mantenimiento de la rama 8.0, con correcciones y
endurecimiento de seguridad, sin cambios que rompan. Toca **un solo archivo**, el `.csproj`. Y lo
verificó la cadena entera: en el PR, los 53 unitarios con su umbral; después del merge, la
integración y la e2e contra QA, que hacen login en cada prueba. Recién ahí lo aprobé para
producción.

Los otros doce no: casi todos son saltos de versión mayor (`vite` 5 → 8, `vitest` 2 → 5,
`typescript` 5 → 7, `Identity.Core` 8 → 10, que pide .NET 10). El de `vitest` es el mismo
conflicto del TP5: vitest nuevo necesita vite 6 o más.

## El monitor y lo que mostró Sentry

**El monitor tardó 5 minutos 33 segundos en avisar.** Suspendí la api de producción a las 23:19:04
UTC y UptimeRobot marcó DOWN a las 23:24:37, con `503 Service Unavailable`. La reanudé, respondió a
las 23:25:37 y el UP llegó a las 23:29:43. Los dos avisos llegaron por mail. Sin el monitor, me
habría enterado cuando alguien se quejara.

**La operación más usada es `GET /api/stock`**, con 18 pedidos en la sesión de uso, más que
cualquier otra.
**La más lenta es `POST /api/auth/login`**, con un P95 de 2,7 segundos (P50 de 786 ms), la única
que además de ir a la base verifica una contraseña. Son tres pedidos nada más, así que el número
es de una muestra chica.

Lo más útil fue otra cosa: **casi todas las lecturas tardan lo mismo, unos 180 ms**, y cada
consulta a la base tarda 176 ms con muy poca variación. La api corre en Oregon y la base en São
Paulo, y cada consulta cruza el continente. Con ese dato abrí la tarjeta #66 del tablero.

## Problemas que encontré

**CodeQL encontró un bug en mi propio test del TP7.** La e2e de stock comparaba la cantidad con
`` new RegExp(`^${antes + 1}(?!\d)`) ``, escrita con una sola barra. Adentro de un template literal,
`\d` no es "un dígito" sino la letra `d`, así que la aserción aceptaba `12` cuando se esperaba `1`.
Lo comprobé, lo corregí y verifiqué el test contra QA sin reintentos. Era un hallazgo de nivel
*high* que ningún test podía ver, porque el que fallaba era el test.

**Cuatro jobs del pipeline no declaraban `permissions`** y corrían con los permisos por defecto
del token. Ahora cada uno declara sólo `contents: read`, y la cadena siguió en verde. Otros tres
hallazgos quedaron abiertos: el *log forging* del middleware de errores y el mail del
administrador en el log del seed. En esta app son de riesgo bajo.

**Me preocupaba que reanudar producción la volviera a una imagen vieja.** En Render, cada servicio
tiene configurada la imagen del primer merge del TP7, y un deploy disparado por Render usa ésa.
Lo medí: reanudar no despliega, el servicio volvió con el mismo deploy que tenía.

## Declaración de uso de IA

(pendiente: lo completo yo)
