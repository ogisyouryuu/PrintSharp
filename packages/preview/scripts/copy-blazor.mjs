import { mkdir, copyFile } from 'node:fs/promises';
const destination = new URL('../../../src/PrintSharp.Preview.Blazor/wwwroot/', import.meta.url);
await mkdir(destination, { recursive: true });
await copyFile(new URL('../dist/index.js', import.meta.url), new URL('preview.js', destination));
