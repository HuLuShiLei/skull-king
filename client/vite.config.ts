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
    // 默认产出到服务端的静态目录，本地起一个进程就能跑通全链路。
    // 拆成两个容器部署时由 Dockerfile 把它指到 dist，交给 nginx 托管。
    outDir: process.env.SKULLKING_OUT_DIR ?? '../src/SkullKing.Server/wwwroot',
    emptyOutDir: true,
  },
})
