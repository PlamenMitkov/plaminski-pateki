import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { visualizer } from 'rollup-plugin-visualizer'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    process.env.ANALYZE === 'true'
      ? visualizer({
          filename: 'bundle-analysis.html',
          open: true,
          gzipSize: true,
          brotliSize: true,
        })
      : null,
  ],
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          'vendor-react': ['react', 'react-dom', 'react-router-dom'],
          'vendor-leaflet': ['leaflet', 'react-leaflet', 'react-leaflet-cluster'],
          'vendor-recharts': ['recharts'],
          'vendor-lucide': ['lucide-react'],
        },
      },
    },
  },
})
