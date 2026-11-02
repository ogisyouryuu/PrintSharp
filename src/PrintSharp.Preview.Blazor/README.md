# PrintSharp.Preview.Blazor

Blazor RCL wrapper for the shared TypeScript Canvas preview. Reference this project from an interactive Blazor application; it targets .NET 10.

```razor
@using PrintSharp.Preview.Blazor

<PrintSharpPreview DocumentId="@documentId"
                  BaseUrl="/api/v1/previews"
                  Height="600px" />
```

Create a preview session using `PrintSharp.Api` first. `BaseUrl` must be reachable by the browser. Static SSR alone does not run this component's JavaScript initialization. For bearer tokens or other custom browser request behavior, use a same-origin BFF or the TypeScript source interface.

`wwwroot/preview.js` is generated from `packages/preview/src/index.ts` and committed for .NET-only consumers. To update it, run `npm --prefix packages/preview run build` from the repository root. Do not edit the generated file directly.

See the [integration guide](../../doc/canvas-preview.zh-CN.md) for methods, callbacks, lifecycle, caching, and limitations. This package has not been published to NuGet.
