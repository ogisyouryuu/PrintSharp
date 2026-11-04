import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
const files = {
  '/selection.js': ['text/javascript', new URL('../dist/selection.js', import.meta.url)],
  '/search.js': ['text/javascript', new URL('../dist/search.js', import.meta.url)],
  '/': ['text/html', new URL('./test.html', import.meta.url)],
  '/thumbnails.js': ['text/javascript', new URL('../dist/thumbnails.js', import.meta.url)],
  '/continuous.js': ['text/javascript', new URL('../dist/continuous.js', import.meta.url)],
  '/index.js': ['text/javascript', new URL('../dist/index.js', import.meta.url)],
  '/vue.js': ['text/javascript', new URL('../dist/vue.js', import.meta.url)],
  '/vue-runtime.js': ['text/javascript', new URL('../node_modules/vue/dist/vue.esm-browser.js', import.meta.url)],
  '/page.json': ['application/json', new URL('../../../artifacts/preview-tests/page.json', import.meta.url)]
};
createServer(async (request, response) => {
  const file = files[request.url];
  if (!file) { response.writeHead(404); response.end(); return; }
  try { response.setHeader('Content-Type', file[0]); response.end(await readFile(file[1])); }
  catch { response.writeHead(500); response.end('Run the PDF fixture tests first.'); }
}).listen(5197, '127.0.0.1');
