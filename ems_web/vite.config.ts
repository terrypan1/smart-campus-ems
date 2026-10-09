import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { quasar, transformAssetUrls } from '@quasar/vite-plugin'

// 後端一律走同源相對路徑，跟部署機 nginx（deploy/wsl/ems.nginx.conf）同一套路由；開發時由這裡的 proxy 代打。
// 開發用 port 3021（部署版 3020），避開 Vite 預設 5173，同一台機器跑其他專案才不會搶 port
export default defineConfig({
  plugins: [
    vue({ template: { transformAssetUrls } }),
    quasar({ sassVariables: fileURLToPath(new URL('./src/theme/quasar-variables.sass', import.meta.url)) }),
  ],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 3021,
    strictPort: true,
    host: '0.0.0.0',
    proxy: {
      '/api': { target: 'http://localhost:5321', changeOrigin: true },
    },
  },
  build: { chunkSizeWarningLimit: 3000 },
})
