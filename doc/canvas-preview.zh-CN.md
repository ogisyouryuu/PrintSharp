# 在自己的 ASP.NET Core 程序里集成 Canvas 预览

## 安装与服务端

服务端引用 `PrintSharp.Preview.AspNetCore`（传递引用 PrintSharp/Pdf）；组件所在的服务端和 WebAssembly Client 项目引用 `PrintSharp.Preview.Blazor`。Excel 导出另装 `PrintSharp.Excel`。日文、中文、韩文另装 `PrintSharp.Fonts.NotoSansCjk`；需要全部原有字体时安装 `PrintSharp.Fonts.Noto`。不需要复制 JS 或引用 PrintSharp.Api 应用。

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

也可传入 PreviewDocument。会话存储 gzip JSON、文档级共享字形和同一排版生成的 PDF，不受原 Document 后续修改影响。自有字体注册表至少存活到 Create 返回。Create 同步执行排版并接受 CancellationToken。

默认所有者由认证用户的 issuer/tenant/NameIdentifier（或 sub/Name）确定；匿名用户共享匿名所有者。宿主应在读取路由和创建入口配置授权。API Key 等自定义标识设置 `OwnerResolver = user => user.FindFirst("account_id")!.Value`，返回稳定且互不冲突的账户标识。

默认存储是单进程内存，重启丢失。多实例可实现 IPrintPreviewStore，在 AddPrintSharpPreview 后注册替代实现；Get/Delete 必须检查所有者和到期时间。Get 返回 PreviewEntry；兼容属性 Pages/Manifest/TextIndex 按需恢复 v1 字节，避免循环读取 Pages（会解压全部页面）。新路由使用 PageCount 和按页访问方法。Size 是实际保存的压缩 JSON、共享资源及 PDF 字节之和，不含对象开销或创建时的瞬时内存。限制同时包括会话数、总字节、单文档 MaxDocumentBytes（默认 64 MiB）；不会驱逐尚有效的会话来腾出空间。

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

销毁、清空或更换 DocumentId 会 destroy 旧控制器，取消请求并释放画布/缓存；断开连接由 DOM MutationObserver 兜底。默认不删除服务端会话；设置 `DeleteOnDispose="true"` 后，组件销毁、清空或更换 DocumentId 时向旧会话发送一次 DELETE，沿用旧会话的 RequestHeaders/Credentials。同一文档配置变化只释放客户端控制器。关闭标签页或网络断开时仍靠 TTL 清理。过期返回 404/410，宿主重新创建会话并更换 DocumentId。

## 语言、认证与 Vue

根据 html lang 选择 ja/en/zhCN/zhTW/ko，zh-Hant、zh-TW、zh-HK、zh-MO 使用繁体，未知语言回退英文。Blazor `Labels` 使用强类型 `PreviewLabels`，例如 `new PreviewLabels { Loading = "读取中…", Expired = "请重新打开预览" }`；属性有 XML 文档，null 保留语言默认值。旧的显式 Dictionary<string,string> 可隐式转换；原来 `Labels = new() { ["loading"] = ... }` 的写法应改成属性初始化。TS 导出五套语言，Labels 类型包含全部键。

RequestHeaders 可传 `new() { ["Authorization"] = "Bearer ..." }`；Credentials="include" 用于跨源 Cookie，API 需允许对应 origin 与凭据，默认 same-origin。宿主负责刷新令牌。GetPdfUrl 只是 URL，不携带 Bearer header；Bearer 下载需由宿主 fetch 为 blob 或通过 BFF。

```ts
import { PrintSharpPreview } from '@printsharp/preview/vue';
import { createPrintPreview, createHttpPreviewSource, ja } from '@printsharp/preview';
```

`npm i @printsharp/preview`，Vue 入口另需 Vue 3。TS source 支持 requestInit/自定义 fetch。包含全部 dist 模块及类型。npm.yml 随 vMAJOR.MINOR.PATCH tag 发布，与 NuGet 版本相同；需在 npm 配置该仓库工作流的 trusted publishing，首次发布需完成包账户初始化。代码修改本身不会发布远端包。

升级说明：组件控制方法从 ValueTask 改为 Task，原来显式调用 AsTask 的代码应移除该转换；旧 API 宿主需设置 BaseUrl。

## 输出支持范围与字体裁剪

PDF/Canvas 对 IFormattable 值应用 .NET 数值/日期格式，如 `#,##0`、`yyyy/MM/dd`，不是完整 Excel 格式解释器。优先使用 `Formula("=SUM(A2:A61)", value: total)` 的缓存值；无缓存时支持同一逻辑页网格的 SUM/AVERAGE/COUNT/MIN/MAX 范围聚合（支持绝对引用和逗号分隔的多个范围）。跨表、其它函数、循环引用及空集合 AVERAGE 不计算，默认留空；`FormulaWithoutValue = FormulaFallback.Text/Throw` 可改为显示公式或报出单元格地址。Excel 保留真实公式；`ToExcelBytes`/`SaveAsExcel` 同时写入显式缓存值，非计算型阅读器可直接显示。直接操作 `ToExcelWorkbook()` 返回的 ClosedXML 对象后再自行 SaveAs，缓存持久化受 ClosedXML 保存选项控制；需要可靠保留调用方缓存时使用 PrintSharp 导出方法。DateOnly/TimeOnly 在 Excel 中为日期/时间值。

字体独立为四个包：PrintSharp.Fonts.NotoSans（拉丁）、NotoSansCjk（中日韩黑体）、NotoSerifCjk（中日韩宋体）、NotoSansArabic（阿拉伯文）。Pdf 本体只依赖拉丁包，安装的字体通过 buildTransitive 自动复制到 Fonts，默认注册表自动发现完整的 Regular/Bold 字体对，无需代码。升级原有多语言应用请加 PrintSharp.Fonts.Noto 元包，或只装需要的字体包。未覆盖文字会报错并提示包名。字体使用 OFL-1.1，包内附许可证。

仍支持设置 `<PrintSharpIncludeBundledFonts>false</PrintSharpIncludeBundledFonts>` 关闭 NuGet 字体自动复制，自行提供 Fonts 或通过 `new NotoFontRegistry(fontDirectory, ["NotoSansCJKjp"])` / PdfRenderOptions.TextRenderer 使用指定字体。清理旧输出目录后验证拆包效果，旧版本残留字体不会自动删除。

参见 [JSON 协议](preview-protocol.md)。

## 同一份 Report 输出为一个 Excel 工作表

```csharp
var bytes = document.ToExcelBytes(new ExcelRenderOptions
{
    ReportLayout = ExcelReportLayout.SingleSheet,
    FreezeHeaderRows = true
});
```

每个逻辑页对应一个工作表。Header/Body/Footer 各写一次，Header 设置为打印标题行，正文连续，纸张/方向/边距保留，宽度适合一页、高度由 Excel 分页。默认仍为 PerPhysicalPage。普通 PageValue 用整个逻辑页正文的 BodyRowOffset=0/BodyRowCount 求值一次，公式缓存也支持 PageValue。

页码使用 `PageValue.PageNumbers`（或 PageNumber/PageCount），SingleSheet 将其移入 Excel 页脚 `&P / &N`；PDF/预览仍显示实际页码。任意委托不能可靠推断是不是页码，已有 `new PageValue(c => ...)` 需要加 `{ ExcelFooterText = "&P / &N" }` 或替换成上述内置值。

## 固定格式语言与下载文件名

格式语言优先级为 `PdfRenderOptions.Culture` > `Document.Create(d => d.Culture("ja-JP")...)` > InvariantCulture；不再跟随 CurrentCulture。`yyyy/MM/dd` 和 `#,##0` 的默认输出固定为斜线和逗号。Culture 在分页后保留，预览会话创建时使用相同规则。Excel 仍保存格式码，由阅读器解释，不提供完整 .NET/Excel 格式转换。

`GET /api/previews/{id}/pdf?filename=予約一覧-20261115.pdf` 可指定下载名称；Blazor 调用 `GetPdfUrl("予約一覧-20261115.pdf")` 会正确编码参数。不传时取文档 Title，再回退 document.pdf。服务端删除路径分隔符、控制字符及无效文件名字符，限制长度并统一 .pdf 后缀；非 ASCII 名称通过 filename* 传输。

## 预览传输与缓存

新 HTTP source 使用 schemaVersion=2：页面只包含 resourceIds，共享资源分批从 `/resources?ids=…` 取回；同一 source 跨页缓存资源，默认最多 16 MiB，可用 maxResourceCacheBytes 调整，销毁时清空。请求 ID 每批最多 64 个以适应常见 URL 长度限制。页面缓存另有原有的 maxCacheBytes/cachePages 限制。

服务端预先 gzip 压缩页面、manifest、文字索引和每个共享资源；接受 gzip 的页面请求直接发送缓存字节。动态资源批次按需组装压缩；不接受 gzip 时解压返回。文字索引只保留段落文字和区域边界，字符定位框仅在页面内保存；已加载页面的搜索仍使用字符框高亮。默认 v1 路由兼容旧客户端，按需重建包含资源/完整定位框的响应。

宿主可以继续启用 ASP.NET Core ResponseCompression 压缩其他响应；内置 JSON 路由无需宿主配置即可协商 gzip。ETag 按协议版本和编码区分，Vary: Accept-Encoding，缓存仍为 private。PDF 下载仍为 no-store。

本仓库九页日文合成报表（前八页 22×12、末页 4×12）测得原始 JSON 约 4.16 MB，v2 gzip 遍历全部页及搜索索引约 0.15 MB；含 PDF 的缓存从约 4.20 MB 降到 0.19 MB。这是可复现测试数据，并非调用方未提供的原始预约报表；数字随内容变化，回归测试要求传输和 JSON 缓存均低于旧版的三分之一。
