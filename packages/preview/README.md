# @printsharp/preview

TypeScript Canvas preview for PrintSharp. The server supplies positioned glyph outlines; the browser does not reshape or wrap text. Includes pagination, zoom, fit width/page, bounded page caching, cancellation, and a Vue 3 wrapper.

This package is source-ready and has not been published to npm.

```powershell
npm ci
npm run build
npm run demo
```

Start `PrintSharp.Api` on port 5080 first, then open `http://127.0.0.1:5198`. For a local npm package, run `npm pack` after building and install the resulting tarball in your application.

```ts
import { createPrintPreview, createHttpPreviewSource } from '@printsharp/preview';

const preview = createPrintPreview(document.querySelector<HTMLElement>('#preview')!, {
  source: createHttpPreviewSource({ baseUrl: '/api/v1/previews', documentId }),
  zoom: 'fit-width'
});
await preview.ready;
```

Give the container a fixed height. Call `destroy()` when finished; server sessions expire separately or can be deleted explicitly. Vue consumers import `PrintSharpPreview` from `@printsharp/preview/vue`. Blazor uses the `PrintSharp.Preview.Blazor` project; `npm run build` updates its bundled JS.

Run `npx playwright install chromium` once, then `npm test`. Tests require .NET 10 to generate the multilingual fixture.

See the [complete integration guide](../../doc/canvas-preview.zh-CN.md) for HTTP routes, protocol, Vue, Blazor, authentication, lifecycle, limits, and validation scope. Canvas text selection/search, continuous scrolling, and distributed server caching are not implemented in this version.

Independent runnable projects: [Vue 3 demo](../../demos/PrintSharp.Preview.Vue/README.md) and [Blazor Interactive Server demo](../../demos/PrintSharp.Preview.Blazor.Demo/README.md). Both include a two-page sample and real API upload/download verification. After installing the Vue demo dependencies, run `npm run test:demos` to launch and test both applications.
