import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import path from 'path'

const apiTarget = process.env.services__Api__http__0 || "http://localhost:7415"

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react()
  ],
  server: {
    host: true, // or '0.0.0.0'
    port: process.env.PORT ? parseInt(process.env.PORT, 10) : 5173,
    proxy: {
      "/api": {
        target: apiTarget,
        changeOrigin: true,
      },
      "/docs": {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
   optimizeDeps: {
    exclude: [],
    include: ["@tabler/icons-react"], // (optional) force include
  },
})
