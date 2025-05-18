import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

const proxyTarget = 
    process.env.ProxiedUrl ??
    process.env.services__AIPRACTICEWEBAPI__http__0 ?? 
    '';

console.log('Proxy target:', proxyTarget);

// https://vite.dev/config/
export default defineConfig({
    plugins: [svelte()],
    server: {
        host: '0.0.0.0',
        port: parseInt(process.env.PORT ?? "5173"),
        proxy: {
            '/api': proxyTarget
        },
        watch: {
            ignored: ['**/node_modules/**']
        }
    }
})
