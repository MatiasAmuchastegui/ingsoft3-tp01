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

Para este TP solo use IA para redactar algunas partes de este decisiones.md.
