# Decisiones — TP1

## 1. Por qué Git no pudo resolver el conflicto solo

Las ramas A y B partieron del mismo commit de `main` y las dos modificaron **la misma línea** del `README.md` (el título), cada una con un contenido distinto ("versión A" vs "versión B"). Cuando Git hace un merge de 3 vías compara cada rama contra el ancestro común: si una sola rama cambió esa línea, Git sabe qué versión "ganó" y la aplica solo. Pero acá **las dos** la cambiaron, y no hay forma de que Git sepa cuál de las dos versiones es la correcta — no es un problema técnico que un algoritmo pueda resolver, es una decisión de contenido que solo puede tomar una persona. Por eso Git se detiene, marca el archivo con `<<<<<<<`, `=======` y `>>>>>>>`, y me delega la decisión a mí.

Para que nunca hubiera aparecido, tendría que haber evitado que las dos ramas tocaran la misma línea al mismo tiempo: por ejemplo, mergeando la rama A y recién después creando la rama B (partiendo ya del `main` actualizado, con el título ya cambiado), en vez de crear las dos a la vez desde el mismo punto de partida. En un equipo real esto se traduce en ramas cortas, integración frecuente y comunicación sobre quién está tocando qué archivo — cuanto más tiempo vive una rama sin integrarse, más probable (y más grande) es el conflicto.

## 2. Problemas que encontré y cómo los solucioné

- **`Require approvals` quedó tildado por default.** Al activar "Require a pull request before merging" en la protección de `main`, GitHub tilda solo la casilla de "Require approvals" con el valor 1. Como el TP es individual y GitHub no te deja aprobar tu propio PR, esto me iba a dejar sin poder mergear nunca. Lo solucioné destildando esa opción para que quedara en cero aprobaciones obligatorias.

- **El botón de mergear no se habilitaba enseguida después de resolver el conflicto.** Después de sacar los marcadores del `README.md` en el editor web y tocar "Commit merge", el botón para mergear el PR de la rama B se quedó un rato sin habilitarse. Al principio pensé que mi resolución había quedado mal, pero era que GitHub estaba recalculando el estado del PR — esperando unos segundos se habilitó solo, sin que tuviera que tocar nada más.

- **La evidencia del push rechazado no aísla la causa.** Cuando probé pushear directo a `main`, el rechazo que me dio GitHub fue `[rejected] main -> main (fetch first)` — un rechazo normal por tener el `main` local desactualizado, no necesariamente el rechazo específico de la protección de rama (`protected branch hook declined`). No reintenté el push después de actualizar mi rama local, así que esta captura por sí sola no prueba de forma concluyente que la protección esté funcionando, aunque el resto de la configuración (branch protection activa, "Do not allow bypassing" activado) sí está verificada en la configuración del repositorio.

## 3. Declaración de uso de IA


Usé IA (Claude) durante el desarrollo del TP para entender conceptos de Git que la guía introduce — sobre todo cómo funciona un merge de 3 vías, la diferencia entre un rechazo por protección de rama y un rechazo por estar desactualizado, y por qué el reviewer no puede aprobar su propio PR en GitHub. Verifiqué esas explicaciones contrastándolas con el marco teórico de la guía de la cátedra (§3.4 y §3.6) y con lo que efectivamente veía en mi propio repositorio y mis propias capturas.

También usé Claude para redactar la versión final de este archivo y de `evidencias.md`, a partir de contarle qué había hecho, qué capturas tenía y qué problemas encontré. Verifiqué el resultado revisando cada captura contra la descripción que se le puso, y corrigiendo cuando la interpretación automática no coincidía con lo que realmente pasó — por ejemplo, la primera lectura de la captura del push rechazado asumía que probaba la protección de rama, y yo aclaré que el mensaje correspondía a un rechazo por estar desactualizado, no confirmado como rechazo por protección, lo cual quedó reflejado en el punto 2 de este documento.
