import { Buffer } from 'node:buffer';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
function riderClientConsolePlugin() {
    return {
        name: 'rider-client-console',
        configureServer: function (server) {
            server.middlewares.use('/__client-log', function (req, res) {
                var request = req;
                if (request.method !== 'POST') {
                    res.statusCode = 405;
                    res.end();
                    return;
                }
                var chunks = [];
                request.on('data', function (chunk) { return chunks.push(Buffer.from(chunk !== null && chunk !== void 0 ? chunk : [])); });
                request.on('end', function () {
                    var _a, _b, _c, _d, _e, _f, _g;
                    try {
                        var raw = Buffer.concat(chunks).toString('utf-8');
                        var payload = JSON.parse(raw);
                        var prefix = "[CLIENT ".concat((_a = payload.scope) !== null && _a !== void 0 ? _a : 'App', "] ").concat((_b = payload.timestamp) !== null && _b !== void 0 ? _b : '', " ").concat((_c = payload.message) !== null && _c !== void 0 ? _c : '').trim();
                        var level = (_d = payload.level) !== null && _d !== void 0 ? _d : 'info';
                        if (level === 'error') {
                            console.error(prefix, (_e = payload.data) !== null && _e !== void 0 ? _e : '');
                        }
                        else if (level === 'warn') {
                            console.warn(prefix, (_f = payload.data) !== null && _f !== void 0 ? _f : '');
                        }
                        else {
                            console.info(prefix, (_g = payload.data) !== null && _g !== void 0 ? _g : '');
                        }
                    }
                    catch (error) {
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
