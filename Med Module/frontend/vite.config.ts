import { Buffer } from 'node:buffer';
import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';

function riderClientConsolePlugin(): Plugin {
  return {
    name: 'rider-client-console',
    configureServer(server) {
      server.middlewares.use('/__client-log', (req, res) => {
        const request = req as typeof req & {
          method?: string;
          on: (event: string, listener: (chunk?: Uint8Array) => void) => void;
        };
        if (request.method !== 'POST') {
          res.statusCode = 405;
          res.end();
          return;
        }

        const chunks: Buffer[] = [];
        request.on('data', (chunk) => chunks.push(Buffer.from(chunk ?? [])));
        request.on('end', () => {
          try {
            const raw = Buffer.concat(chunks).toString('utf-8');
            const payload = JSON.parse(raw) as {
              level?: 'info' | 'warn' | 'error';
              scope?: string;
              message?: string;
              timestamp?: string;
              data?: unknown;
            };

            const prefix = `[CLIENT ${payload.scope ?? 'App'}] ${payload.timestamp ?? ''} ${payload.message ?? ''}`.trim();
            const level = payload.level ?? 'info';
            if (level === 'error') {
              console.error(prefix, payload.data ?? '');
            } else if (level === 'warn') {
              console.warn(prefix, payload.data ?? '');
            } else {
              console.info(prefix, payload.data ?? '');
            }
          } catch (error) {
            console.error('[CLIENT] Failed to parse client log payload', error);
          }

          res.statusCode = 204;
          res.end();
        });
      });
    }
  };
}

export default defineConfig({
  plugins: [react(), riderClientConsolePlugin()],
  server: {
    port: 5173
  }
});
