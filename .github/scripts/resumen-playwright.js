// Escribe en el resumen de la corrida cuántas pruebas pasaron, fallaron, quedaron flaky o se
// saltearon, leyendo el reporte json de Playwright.
//
// Uso (desde frontend/):  node ../.github/scripts/resumen-playwright.js "<título>"
//
// No decide nada: el job ya quedó verde o rojo en el paso que corrió las pruebas. Esto sólo
// deja los números a la vista sin tener que abrir el log.
const fs = require('fs')

const titulo = process.argv[2] || 'Playwright'
const archivo = 'test-results/resultados.json'

console.log(`### ${titulo}`)
if (fs.existsSync(archivo)) {
  const s = JSON.parse(fs.readFileSync(archivo, 'utf8')).stats
  console.log(`**${s.expected} passed** · ${s.unexpected} failed · ${s.flaky} flaky · ${s.skipped} skipped`)
} else {
  console.log('La suite no llegó a correr: no hay resultados.')
}
