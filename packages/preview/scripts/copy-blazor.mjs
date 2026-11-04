import { mkdir, copyFile } from 'node:fs/promises';
const destination = new URL('../../../src/PrintSharp.Preview.Blazor/wwwroot/', import.meta.url);
await mkdir(destination, { recursive: true });
await copyFile(new URL('../dist/index.js', import.meta.url), new URL('preview.js', destination));

await copyFile(new URL('../dist/continuous.js', import.meta.url), new URL('continuous.js', destination));

await copyFile(new URL('../dist/thumbnails.js', import.meta.url), new URL('thumbnails.js', destination));

await copyFile(new URL('../dist/search.js', import.meta.url), new URL('search.js', destination));

await copyFile(new URL('../dist/selection.js', import.meta.url), new URL('selection.js', destination));
