# PrintSharp.Preview.Blazor

Blazor RCL wrapper for the shared TypeScript Canvas preview. Reference this project from an interactive Blazor application; it targets .NET 10.

```razor
@using PrintSharp.Preview.Blazor

<PrintSharpPreview DocumentId="@documentId"
                  BaseUrl="/api/v1/previews"
                  Height="600px" />
```

Create a preview session using `PrintSharp.Api` first. `BaseUrl` must be reachable by the browser. Static SSR alone does not run this component's JavaScript initialization. For bearer tokens or other custom browser request behavior, use a same-origin BFF or the TypeScript source interface.

`wwwroot/preview.js`, `wwwroot/continuous.js`, `wwwroot/thumbnails.js`, `wwwroot/search.js`, and `wwwroot/selection.js` are generated from `packages/preview/src` and committed for .NET-only consumers. To update them, run `npm --prefix packages/preview run build` from the repository root. Do not edit the generated files directly.

See the [integration guide](../../doc/canvas-preview.zh-CN.md) for methods, callbacks, lifecycle, caching, and limitations. This package has not been published to NuGet.

Use `ViewMode="continuous"` or `@bind-ViewMode` to enable continuous scrolling. `ViewModeChanged` reports toolbar switches; `SetViewModeAsync` switches through the component reference. Page events in continuous mode track the viewport center. The demo displays the selected mode and verifies both directions of switching.

Use `Thumbnails="true"` or `@bind-Thumbnails` to show the thumbnail sidebar (default: closed). `ThumbnailsChanged` reports toolbar changes; `SetThumbnailsVisibleAsync(bool)` controls it through the component reference. Clicking a thumbnail updates the page in either viewing mode.

Search is available in the toolbar. `SearchChanged` returns a `PreviewSearchState` with Query, Total, Current, Busy, Truncated, and Error. Component methods: `SearchAsync(string)`, `NextSearchResultAsync()`, `PreviousSearchResultAsync()`, `CloseSearchAsync()`. The browser requests the snapshot `/text` endpoint, so proxies must forward it. New snapshots highlight positioned text boxes; older sources fall back to cell content regions.

Text selection defaults to enabled. Use `TextSelection` / `@bind-TextSelection`, `SelectionChanged` (string), `SetTextSelectionAsync(bool)` and `ClearSelectionAsync()`. Use the built-in Copy button or Ctrl+C / Command+C; the button runs directly in the browser to preserve clipboard user activation. Selection is limited to mounted pages and can be cleared when virtualization releases a page.
