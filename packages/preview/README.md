# @printsharp/preview

TypeScript Canvas preview for PrintSharp. The server supplies positioned glyph outlines; the browser does not reshape or wrap text. Includes a virtualized thumbnail sidebar, continuous scrolling, full-document text search, pagination, zoom, fit width/page, bounded page caching, cancellation, and a Vue 3 wrapper.

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

Run `npx playwright install chromium` once, then `npm test`. Tests require .NET 11 to generate the multilingual fixture.

See the [complete integration guide](../../doc/canvas-preview.zh-CN.md) for HTTP routes, protocol, Vue, Blazor, authentication, lifecycle, limits, and validation scope. Distributed server caching is not implemented in this version.

Independent runnable projects: [Vue 3 demo](../../demos/PrintSharp.Preview.Vue/README.md) and [Blazor Interactive Server demo](../../demos/PrintSharp.Preview.Blazor.Demo/README.md). Both include a two-page sample and real API upload/download verification. After installing the Vue demo dependencies, run `npm run test:demos` to launch and test both applications.

Set `viewMode: "continuous"` or use the toolbar to switch modes. `setViewMode()` switches without creating a new document. Continuous view keeps only visible pages and one neighbor on either side, preserves the reading anchor on zoom/resize, and follows the viewport center for page events. Configure `maxCacheBytes` (default 16 MiB) and `maxTotalCanvasPixels` (default 24 million) alongside `cachePages`. When serving ESM directly, deploy `dist/index.js`, `dist/continuous.js`, `dist/thumbnails.js`, `dist/search.js`, and `dist/selection.js` together.

Set `thumbnails: true` to open the sidebar initially (default: false), or call `setThumbnailsVisible(true)`. Read `thumbnailsVisible` and listen for `thumbnailschange` (boolean). Vue supports `v-model:thumbnails`; Blazor supports `@bind-Thumbnails`. Thumbnails render visible rows and neighbors only, yield to main-page work, and do not populate or evict the main page cache. They reuse full Display Lists; this version does not provide a server thumbnail image endpoint.

Search from the toolbar or call `await preview.search(query)`, `nextSearchResult()`, `previousSearchResult()`, and `closeSearch()`. Read `searchState` or listen for `searchchange`; Vue forwards that event. The HTTP source lazily fetches `/text`. Custom sources may implement optional `getTextIndex(signal)`. Search is case-insensitive literal matching within formatted cell text, capped at 1,000 results. New snapshots highlight positioned text boxes; sources without boxes fall back to cell content areas.

Text selection is enabled by default (`textSelection: true`). Drag to select, then use Ctrl+C / Command+C or the Copy toolbar button. Read `selectedText`, listen for `selectionchange` (string), call `clearSelection()` / `setTextSelection(bool)`, or invoke `copySelection()` inside a browser user gesture. Vue supports `v-model:text-selection`. Copy uses logical source text with newlines between cells. Only mounted pages participate; long-distance scrolling can release the selection. This is not persistent whole-document selection. Clipboard button use requires HTTPS/localhost and browser permission.
