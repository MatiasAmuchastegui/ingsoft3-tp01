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
    },
  },
})
