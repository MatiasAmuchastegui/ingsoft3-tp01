import { defineConfig } from '@playwright/test'

/**
 * Las dos suites que corren contra un entorno desplegado (TP7):
 *
 *   e2e/api.spec.ts      integración: le habla a la api de verdad, sin navegador
 *   e2e/flujos.spec.ts   end-to-end: usa la app con un navegador, como una persona
 *
 * Ninguna sabe a qué entorno apunta: lo dicen las variables. En el pipeline son las URLs
 * públicas de QA; en la máquina de uno, el compose del TP2, que publica el front en el
 * 8080 y le pasa /api al backend por nginx.
 */
export default defineConfig({
  testDir: './e2e', // las dos suites; cada job del pipeline corre la suya por nombre

  // De a una prueba por vez, también entre archivos. Las dos suites usan la MISMA base del
  // entorno, y las dos mueven stock: en paralelo, los movimientos de una le cambiarían la
  // cantidad a la otra a mitad de la cuenta, y el rojo no sería de nadie.
  workers: 1,

  // Topes generosos a propósito: QA vive en un plan gratuito que se duerme, y el primer
  // pedido después de un rato puede tardar decenas de segundos. El cold start se resuelve
  // esperando, no salteando el test.
  timeout: 60_000, // cada test
  expect: { timeout: 15_000 }, // cada aserción: no hereda el de arriba, el default es 5 s

  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080', // el FRONT
    trace: 'on-first-retry', // la traza se graba al reintentar un fallo
    screenshot: 'only-on-failure',
  },

  // Un reintento absorbe una demora suelta de la red. Si un test pasa recién ahí, el
  // reporte lo marca como «flaky», aunque la corrida quede en verde.
  retries: 1,

  // El json no es para leerlo: el pipeline lo usa para escribir en el resumen de la corrida
  // cuántos pasaron, fallaron y se saltearon. Va en test-results/, que Playwright vacía al
  // empezar cada corrida, no al terminar.
  reporter: [
    ['html', { open: 'never' }],
    ['list'],
    ['json', { outputFile: 'test-results/resultados.json' }],
  ],
})
