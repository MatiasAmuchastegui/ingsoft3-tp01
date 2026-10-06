import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Falla si el puerto está ocupado en lugar de saltar a otro: si Vite se mueve al 5174,
    // el CORS del backend deja de reconocerlo y el error que se ve no dice eso.
    strictPort: true,
  },

  test: {
    coverage: {
      provider: 'v8',

      // 'json-summary' no es decorativo: deja el coverage-summary.json con los totales,
      // que es el archivo que el pipeline lee para armar la tabla del resumen.
      reporter: ['text', 'html', 'lcov', 'json-summary'],

      // QUÉ entra en la cuenta. Sin esta línea se mide el proyecto entero —componentes,
      // ruteo, pegamento de interfaz— y el número se hunde por código que no tiene sentido
      // probar unitariamente: la interfaz se verifica de punta a punta, en el TP7.
      // Acá se mide la lógica de negocio, que es lo que un unit test puede custodiar.
      include: ['src/lib/**'],

      // El umbral: si la cobertura cae por debajo, vitest sale con ERROR aunque todos los
      // tests estén en verde. Eso pone el job en rojo, y como build-frontend es un required
      // check, el merge se bloquea.
      //
      // Hoy mide 100% de línea y de rama sobre src/lib. 90 deja diez puntos de aire: frena
      // si entra lógica sin tests, sin romperse por un refactor menor.
      //
      // Ojo: los thresholds sólo se evalúan si la corrida pide cobertura. `vitest run` a
      // secas, sin --coverage, pasa en verde aunque se esté por debajo — por eso el script
      // test:ci la pide, y es ése el que corre el pipeline.
      thresholds: { lines: 90, branches: 90 },
    },
  },
})
