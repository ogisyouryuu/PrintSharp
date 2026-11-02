import { createServer, request as proxyRequest } from 'node:http';
import { readFile } from 'node:fs/promises';
const backend = new URL(process.env.PRINTSHARP_API ?? 'http://127.0.0.1:5080');
createServer(async (request, response) => {
  if (request.url.startsWith('/api/')) {
    const upstream = proxyRequest(new URL(request.url, backend), { method: request.method, headers: { ...request.headers, host: backend.host } }, result => {
      response.writeHead(result.statusCode, result.headers); result.pipe(response);
    });
    upstream.on('error', () => { response.writeHead(502); response.end('Start PrintSharp.Api on port 5080.'); });
    request.pipe(upstream); return;
  }
  const file = request.url === '/' ? ['text/html; charset=utf-8', './index.html'] : request.url === '/preview.js' ? ['text/javascript', '../dist/index.js'] : null;
  if (!file) { response.writeHead(404); response.end(); return; }
  try { response.setHeader('Content-Type', file[0]); response.end(await readFile(new URL(file[1], import.meta.url))); }
  catch { response.writeHead(500); response.end('Run npm run build first.'); }
}).listen(5198, '127.0.0.1', () => console.log('PrintSharp preview: http://127.0.0.1:5198'));
