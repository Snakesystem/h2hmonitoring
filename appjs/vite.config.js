/* eslint-disable no-undef */
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import million from 'million/compiler'
import macrosPlugin from "vite-plugin-babel-macros"
import Unfonts from 'unplugin-fonts/vite'
import Inspect from 'vite-plugin-inspect'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    Inspect(), Unfonts({ google: {families: ['Source Sans Pro']} }), million.vite(), react(), macrosPlugin(), 
  ],
  build: {
    chunkSizeWarningLimit: 1600,
    emptyOutDir: true,
    outDir: 'dist',
    rollupOptions: {
        external: [
            'bootstrap', 
            'file-saver', 
            'framer-motion', 
            'jspdf', 
            'primeicons', 
            'primeflex', 
            'primereact', 
            'jspdf-autotable', 
            'xlsx',
            'vite-plugin-babel-macros'
        ],
        output: {
            entryFileNames: 'script.min.js',
            assetFileNames: (assetInfo) => {
                if(assetInfo.name === "index.css")
                return 'style.min.css'
            return assetInfo.name
            },
            chunkFileNames: "chunk.js",
            manualChunks: undefined
            }
        }
    }
})
