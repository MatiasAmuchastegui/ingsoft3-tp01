# Evidencias — TP1

## 1. Push directo a `main` rechazado
![push rechazado](img/push-rechazado.jpeg)
Intenté pushear un commit directo a `main` desde la consola y GitHub lo rechazó. En este caso puntual el mensaje que dio fue `[rejected] main -> main (fetch first)`, porque mi rama local estaba desactualizada respecto del remoto — no llegué a confirmar con un segundo intento (después de un `git pull`) que el rechazo también se produce por la regla de protección en sí. Lo dejo aclarado en `decisiones.md`.

## 2. El PR de la rama B no se puede mergear: conflicto
![aviso de conflicto](img/conflicto-aviso.jpeg)
Al abrir el Pull Request de la rama B (que modificaba la misma línea del `README.md` que ya había entrado por la rama A), GitHub avisa "This branch has conflicts that must be resolved" y deshabilita el botón de mergear hasta resolverlos.

## 3. Los marcadores del conflicto
![marcadores del conflicto](img/conflicto-marcadores.jpeg)
El editor de resolución de conflictos de GitHub muestra el `README.md` con los marcadores `<<<<<<<`, `=======` y `>>>>>>>`: arriba mi versión (rama B, "versión B"), abajo la que ya estaba en `main` (rama A, "versión A"). Tuve que elegir el contenido final y borrar los marcadores para poder mergear.

## 4. Release `v1.0.0` publicada
![release publicada](img/release-v1.0.0.jpeg)
La release `v1.0.0`, marcada como *Latest*, publicada sobre el tag creado en `main`, con las notas de qué incluye esta versión.
