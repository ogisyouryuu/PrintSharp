# 在自己的 ASP.NET Core 程序里集成 Canvas 预览

## 安装与服务端

服务端引用 `PrintSharp.Preview.AspNetCore`（传递引用 PrintSharp/Pdf）；组件所在的服务端和 WebAssembly Client 项目引用 `PrintSharp.Preview.Blazor`。Excel 导出另装 `PrintSharp.Excel`。不需要复制 JS 或引用 PrintSharp.Api 应用。

```csharp
using PrintSharp.Preview.AspNetCore;
builder.Services.AddPrintSharpPreview(options =>
{
    options.TimeToLive = TimeSpan.FromMinutes(10);
    options.MaxSessions = 100;
    options.MaxCacheBytes = 256 * 1024 * 1024;
});
// app = builder.Build() の後に登録します。
app.MapPrintSharpPreview("/api/previews").RequireAuthorization();
```

在业务服务中注入 `IPrintPreviewStore previews`，使用当前用户创建会话：

```csharp
var session = previews.Create(ReservationReport.BuildReport(rows, options), user);
string documentId = session.DocumentId;
```

也可传入 PreviewDocument。会话存储序列化 JSON 和同一排版生成的 PDF，不受原 Document 后续修改影响。自有字体注册表至少存活到 Create 返回。Create 同步执行排版并接受 CancellationToken。

默认所有者由认证用户的 issuer/tenant/NameIdentifier（或 sub/Name）确定；匿名用户共享匿名所有者。宿主应在读取路由和创建入口配置授权。API Key 等自定义标识设置 `OwnerResolver = user => user.FindFirst("account_id")!.Value`，返回稳定且互不冲突的账户标识。

默认存储是单进程内存，重启丢失。多实例可实现 IPrintPreviewStore，在 AddPrintSharpPreview 后注册替代实现；Get/Delete 必须检查所有者和到期时间。Get 返回 PreviewEntry，含可持久化的已序列化字节。限制同时包括会话数、总字节、单文档 MaxDocumentBytes（默认 64 MiB）；不会驱逐尚有效的会话来腾出空间。

## Blazor 自定义工具栏

```razor
@using PrintSharp.Preview.Blazor
<button disabled="@(!_ready || _page <= 1)" @onclick="() => _preview!.GoToPageAsync(_page - 1)">上一页</button>
<span>@_page / @_pageCount</span>
<button disabled="@(!_ready || _page >= _pageCount)" @onclick="() => _preview!.GoToPageAsync(_page + 1)">下一页</button>
<button disabled="@(!_ready)" @onclick="() => _preview!.ZoomOutAsync()">缩小</button>
<span>@($"{_zoom:P0}")</span>
<button disabled="@(!_ready)" @onclick="() => _preview!.ZoomInAsync()">放大</button>
<PrintSharpPreview @ref="_preview" DocumentId="@_documentId" BaseUrl="/api/previews"
    Toolbar="false" @bind-Page="_page" @bind-Zoom="_zoom"
    PageCountChanged="n => _pageCount = n" Ready="() => _ready = true"
    LoadingChanged="busy => _loading = busy" Height="70vh" />
@code {
    private PrintSharpPreview? _preview;
    private string _documentId = "";
    private int _page = 1, _pageCount;
    private double _zoom;
    private bool _ready, _loading;
}
```

将按钮换成 MudBlazor 等 UI 组件即可。控制方法返回 Task，可以直接作为 Razor/MudBlazor 事件处理器。Page 为 1 基页码，Zoom=0 初次适合宽度，正数指定倍率；ZoomChanged 始终给出实际倍率。挂载前操作会抛 InvalidOperationException，应等待 Ready。换 DocumentId 时重置自己的 ready/loading 状态。

方法包括 GoToPageAsync、SetZoomAsync、ZoomInAsync、ZoomOutAsync、FitWidthAsync、FitPageAsync。首页传 1，末页传总页数。GetPdfUrl 返回 PDF 下载 URL。默认 BaseUrl 为 `/api/previews`，与新服务端扩展一致。使用旧 PrintSharp.Api 时显式设置 `/api/v1/previews`。

适用 Interactive Server、Interactive WebAssembly、Interactive Auto。宿主注册相应交互服务/RenderMode，Auto/WASM 的页面位于 Client 项目，两个项目都引用组件包。静态 SSR 不执行 JS，不要在 OnInitialized/预渲染期间调用组件操作。RCL 自动提供 `_content/PrintSharp.Preview.Blazor/` 下的 preview、continuous、thumbnails、search、selection、labels.js。

销毁、清空或更换 DocumentId 会 destroy 旧控制器，取消请求并释放画布/缓存；断开连接由 DOM MutationObserver 兜底。关闭页面不自动删除服务端会话。过期返回 404/410，宿主重新创建会话并更换 DocumentId。

## 语言、认证与 Vue

根据 html lang 默认选择 ja/en/zh，其它语言回退中文。Labels 字典可覆盖工具栏、页码、搜索、复制及缩略图文本，键见 packages/preview/src/labels.ts；繁中、韩文可自行提供，运行时换语言需传入新的 Labels。TS 导出 ja/en/zhCN。

RequestHeaders 可传 `new() { ["Authorization"] = "Bearer ..." }`；Credentials="include" 用于跨源 Cookie，API 需允许对应 origin 与凭据，默认 same-origin。宿主负责刷新令牌。GetPdfUrl 只是 URL，不携带 Bearer header；Bearer 下载需由宿主 fetch 为 blob 或通过 BFF。

```ts
import { PrintSharpPreview } from '@printsharp/preview/vue';
import { createPrintPreview, createHttpPreviewSource, ja } from '@printsharp/preview';
```

`npm i @printsharp/preview`，Vue 入口另需 Vue 3。TS source 支持 requestInit/自定义 fetch。包含全部 dist 模块及类型。npm.yml 随 vMAJOR.MINOR.PATCH tag 发布，与 NuGet 版本相同；需在 npm 配置该仓库工作流的 trusted publishing，首次发布需完成包账户初始化。代码修改本身不会发布远端包。

升级说明：组件控制方法从 ValueTask 改为 Task，原来显式调用 AsTask 的代码应移除该转换；旧 API 宿主需设置 BaseUrl。

## 输出支持范围与字体裁剪

PDF/Canvas 对 IFormattable 值应用 .NET 数值/日期格式，如 `#,##0`、`yyyy/MM/dd`。不是完整 Excel 格式解释器，不保证支持 Excel 专用条件、颜色、区域码。PDF/Canvas **不计算公式**，保留公式文本；需要计算结果时由调用方计算后传入。Excel 仍写入公式。DateOnly/TimeOnly 在 Excel 中为日期/时间值。

默认 Pdf 包仍携带全套 Noto，并通过 buildTransitive 复制间接依赖的字体。宿主可设置 `<PrintSharpIncludeBundledFonts>false</PrintSharpIncludeBundledFonts>` 关闭默认复制，然后自行包含所需字体。只需日文黑体时，宿主构建/发布步骤可移除输出 Fonts 目录中其它字体，保留 NotoSansCJKjp-Regular.otf、NotoSansCJKjp-Bold.otf 和许可证。使用 `new NotoFontRegistry(fontDirectory, ["NotoSansCJKjp"])`，通过 `new PdfRenderOptions { TextRenderer = new NotoPdfTextRenderer(fonts) }` 传给 Create/导出；不要使用要求全套字体的 NotoFontRegistry.Default。裁剪后未覆盖字种会报错。此方案缩减宿主部署文件，不缩减 NuGet 下载本体。

参见 [JSON 协议](preview-protocol.md)。
