import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { readFileSync } from 'fs'
import { certFilePath, keyFilePath } from './aspnetcore-https'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    //https: {
    //  key: readFileSync(keyFilePath),
    //  cert: readFileSync(certFilePath)
    //},
    port: 5002,
    strictPort: true,
    proxy: {
      '/api': 'http://127.0.0.1:8080'
    }
  }
})
