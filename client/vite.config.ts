import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

const backend = process.env.SKULLKING_API ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: backend, changeOrigin: true },
      '/hub': { target: backend, changeOrigin: true, ws: true },
    },
  },
  build: {
    // 直接产出到服务端的静态目录，生产环境只需部署一个进程。
    outDir: '../src/SkullKing.Server/wwwroot',
    emptyOutDir: true,
  },
})
