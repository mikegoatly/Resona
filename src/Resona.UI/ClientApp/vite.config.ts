import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
// To serve over HTTPS, uncomment these imports and the server.https block below
//import { readFileSync } from 'fs'
//import { certFilePath, keyFilePath } from './aspnetcore-https.cjs'

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
