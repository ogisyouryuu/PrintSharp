# Canvas Preview 实现与接入

PrintSharp 提供后端 JSON Display List 和 TypeScript Canvas 预览控件，可独立使用，也可通过 Vue 组件或 Blazor Razor Class Library 接入。源码中的 npm / NuGet 包尚未发布，请先使用本地构建或项目引用。

## 一致性架构

```text
Excel 模板 + 数据
  → 共用校验、绑定、金额/日期格式化
  → GridLayoutEngine 物理分页
  → Noto / RichTextKit / HarfBuzz 文字塑形与定位
  → PreviewDocument 页面绘制计划
      ├─ Skia PDF：已定位的矢量字形、背景、边框、图片
      └─ JSON：字形轮廓资源 + 最终位置 → Canvas Path2D
```

前端不使用 `fillText`、浏览器字体或 DOM 排版来重算文字。中日韩、阿拉伯文、双向混排、换行、两端对齐、模拟斜体、下划线、删除线和裁剪都由服务端决定。背景、边框、图片先绘制，文字随后绘制，沿用原有顺序。

一致性指物理分页、页面尺寸、字形位置、换行、图片位置和裁剪区域一致；浏览器与 PDF 查看器的抗锯齿、hinting、图片采样可能不同，不承诺逐像素相同。PDF 保留文字对象；Canvas 首版没有文字选择、搜索和文字无障碍层。工具栏支持键盘和读屏标签。

预览页数是物理分页后的页数，不是 `Document.PageCount`。缩放不会重新排版：

```text
CSS 尺寸 = PDF point × 96 / 72 × zoom
画布像素尺寸 = CSS 尺寸 × 有效 devicePixelRatio
```

画布默认限制 1600 万像素、单边不超过 16384 像素；超限时降低像素密度，CSS 尺寸和排版不变。最大缩放为 800%，最小为 10%；自动适配可以低于 10%。

## 首版范围与方案调整

- 创建时生成整份布局、页面 JSON 和 PDF 快照，之后翻页只读取缓存；尚未实现按需生成页面或后台任务队列。
- 每页响应内附该页使用的去重资源，避免逐字形 HTTP 请求。跨页可能重复发送相同资源；尚未提供独立资源端点或资源协商协议。
- 浏览器默认 LRU 缓存 3 页，页面路径在每次绘制中复用；尚未实现跨页 Path2D 缓存、预取、Worker 或连续滚动。
- 服务端采用单进程内存缓存。重启会丢失快照，多实例需会话粘滞；当前没有 Redis / 持久化实现。
- 不新增 Excel 格式化规则或公式计算规则，继续复用现有 PDF 输入准备流程。

## 本地运行

需要 .NET 10 SDK 和支持 ES2022 的 Node.js / npm。首次执行：

```powershell
dotnet run --project src/PrintSharp.Api --urls http://127.0.0.1:5080
```

另开终端，在仓库根目录执行：

```powershell
npm --prefix packages/preview ci
npm --prefix packages/preview run demo
```

打开 `http://127.0.0.1:5198`。上传 Excel 模板，填写 data JSON，创建预览并下载同一快照 PDF。示例数据对应模板中的 `{{T.Value}}`。本地演示服务器把 `/api/*` 转发到 `http://127.0.0.1:5080`，可通过 `PRINTSHARP_API` 环境变量覆盖；演示代理只支持 HTTP，不能作为生产代理使用。

## 独立 Vue / Blazor 项目验收

| 项目 | 地址 | 启动及验收说明 |
|---|---|---|
| Vue 3 + TypeScript + Vite | http://127.0.0.1:5199 | [Vue demo README](../demos/PrintSharp.Preview.Vue/README.md) |
| Blazor Interactive Server | http://127.0.0.1:5200 | [Blazor demo README](../demos/PrintSharp.Preview.Blazor.Demo/README.md) |

两个项目都连接真实 API，支持点击“使用内置两页示例”直接验收，也可以上传 Excel、修改 JSON、翻页缩放、下载同一快照 PDF。示例模板与生成器位于 [PreviewSamples](../demos/PreviewSamples/README.md)。

安装两个 npm 项目依赖后，运行 `npm --prefix packages/preview run test:demos` 自动启动三个服务并验证两个 demo，生成 `artifacts/preview-tests/vue-demo.png`、`blazor-demo.png` 以及对应下载 PDF。测试要求 5080 / 5199 / 5200 端口空闲。

## HTTP API

| 方法 | 路径 | 返回 |
|---|---|---|
| POST | `/api/v1/previews` | 201，文档 manifest，`Location` 为读取地址 |
| POST | `/api/v1/previews/upload` | 201，multipart 上传后的 manifest |
| GET | `/api/v1/previews/{id}` | manifest |
| GET | `/api/v1/previews/{id}/pages/{pageNumber}` | 指定物理页及其资源 |
| GET | `/api/v1/previews/{id}/pdf` | 同一快照生成的 PDF |
| DELETE | `/api/v1/previews/{id}` | 204，释放快照 |

JSON 创建请求与现有 `/api/v1/print/pdf` 相同：`{ "template": { "contentBase64": "..." }, "data": { "tables": [...] } }`。multipart 请求与现有 `/pdf/upload` 相同，包含 `template` 和 `data` 两个部分。所有原有校验、上传限制和错误格式继续有效。

```powershell
curl.exe --fail-with-body http://127.0.0.1:5080/api/v1/previews/upload -F "template=@invoice.xlsx" -F "data=<data.json"
```

创建结果示例：

```json
{
  "schemaVersion": 1,
  "documentId": "opaque-random-id",
  "revision": "opaque-revision",
  "expiresAt": "2026-11-02T03:15:00+00:00",
  "pageCount": 1,
  "pages": [{ "pageNumber": 1, "width": 595.28, "height": 841.89 }]
}
```

所有页号从 1 开始。manifest 和页面 GET 返回 ETag，支持 `If-None-Match` / 304，使用 `private, no-cache`。PDF 使用 `private, no-store`。响应没有设置公开缓存，也没有把整个 PDF 转成 Base64。

| 错误 | 含义 |
|---|---|
| 404 `page_out_of_range` | 页码超出范围 |
| 404 `preview_unavailable` | ID 不存在、已删除、已清理过期，或当前身份无权访问 |
| 410 `preview_expired` | 查询时发现尚未清理的过期记录 |
| 413 `limit_exceeded` | 上传、模板或序列化快照超限 |
| 503 `preview_capacity` | 会话数量或缓存总大小已满，可释放会话后重试 |

404 / 410 都应允许用户重新生成快照。控件不会保留模板和业务数据，也不会自动创建新会话。`destroy()` 只释放浏览器资源，不会 DELETE 服务器快照，以免影响其他控件或下载；宿主可显式调用 DELETE，或等待 TTL。

## TypeScript / 原生 JavaScript

构建后可直接托管 `packages/preview/dist/index.js`。需要本地 npm 包时，在该目录运行 `npm pack`，然后在消费项目安装生成的 `.tgz`。

```ts
import { createPrintPreview, createHttpPreviewSource } from '@printsharp/preview';

const container = document.querySelector<HTMLElement>('#preview')!;
const preview = createPrintPreview(container, {
  source: createHttpPreviewSource({
    baseUrl: '/api/v1/previews',
    documentId: manifest.documentId
  }),
  initialPage: 1,
  zoom: 'fit-width',
  cachePages: 3,
  toolbar: true
});

preview.addEventListener('error', event => {
  console.error((event as CustomEvent).detail);
});
await preview.ready;
await preview.goToPage(2);
preview.setZoom(1.25);
preview.fitPage();
```

容器必须有明确高度，例如 `height: 600px`。控件挂载独立 Shadow DOM，避免宿主样式影响布局。主题使用 `--printsharp-color`、`--printsharp-background`、`--printsharp-toolbar` CSS 自定义属性。`labels` 允许覆盖工具栏和常规状态文案；服务错误及校验错误保持原始信息。

公开操作：`goToPage`、`zoomIn`、`zoomOut`、`setZoom`、`fitWidth`、`fitPage`、`retry`、`destroy`。可读状态：`page`、`pageCount`、`zoom`、`actualZoom`。`goToPage` / `ready` 的失败通过 Promise 报告，同时触发 `error`；UI 操作的失败显示在控件内。

| 事件 | `CustomEvent.detail` |
|---|---|
| `pagechange` | 绘制完成的页码 |
| `zoomchange` | `{ zoom, scale }`，模式及实际比例 |
| `loadingchange` | `boolean`，页面加载/绘制状态 |
| `error` | Error / PreviewError |

快速翻页会取消前一次加载，渲染序号防止旧请求覆盖新页。图片资源加载后才提交画面；绘制失败时不会显示半张新页。同页缩放和尺寸适配保留已完成的画面，后台画布绘制完毕后一次性替换，不先隐藏页面或闪现加载提示。ResizeObserver 处理容器尺寸变化，MutationObserver 在容器移除时清理控件。

HTTP 鉴权可通过 `requestInit.headers`、`requestInit.credentials` 或注入自定义 `fetch` 实现。动态 token 推荐使用自定义 `fetch`。也可实现 `PreviewSource.getManifest(signal)` 和 `getPage(pageNumber, signal)`，脱离内置 HTTP 客户端。

## Vue 3

核心模块没有 Vue 运行时依赖；只有 `@printsharp/preview/vue` 入口要求 Vue 3。

```vue
<script setup lang="ts">
import { ref } from 'vue';
import { createHttpPreviewSource, type Zoom } from '@printsharp/preview';
import { PrintSharpPreview } from '@printsharp/preview/vue';

const props = defineProps<{ documentId: string }>();
const page = ref(1);
const zoom = ref<Zoom>('fit-width');
const source = createHttpPreviewSource({
  baseUrl: '/api/v1/previews',
  documentId: props.documentId
});
</script>

<template>
  <div style="height: 600px">
    <PrintSharpPreview :source="source" v-model:page="page" v-model:zoom="zoom" />
  </div>
</template>
```

源对象变化时组件重建控件；若 documentId 会改变，用 `computed` 生成 source，或替换源对象。不要在每次 render 时无条件创建新 source。可监听 `error` 和 `loadingchange`。组件卸载时自动销毁；通过组件 ref 的 `getPreview()` 可取得底层实例。

## Blazor

引用 `src/PrintSharp.Preview.Blazor/PrintSharp.Preview.Blazor.csproj`。模块通过 RCL 静态资源发布，路径为 `_content/PrintSharp.Preview.Blazor/preview.js`。仓库提交了生成的 JS 以便纯 .NET 消费；修改 TypeScript 后必须执行 `npm run build` 更新该文件，不要直接编辑生成文件。

在启用了交互式 Server 或 WebAssembly 渲染的组件中：

```razor
@using PrintSharp.Preview.Blazor

<PrintSharpPreview @ref="_preview"
                  DocumentId="@documentId"
                  BaseUrl="/api/v1/previews"
                  Height="600px"
                  InitialPage="1" />

@code {
    private PrintSharpPreview? _preview;
    private string documentId = "替换为创建接口返回的 ID";
}
```

组件支持 `PageChanged` 和 `Error` 回调，实例方法为 `GoToPageAsync`、`SetZoomAsync`、`FitWidthAsync`、`FitPageAsync`。组件参数变化会重载对应快照。交互和绘制都在浏览器执行，不把绘图命令逐条送过 SignalR。

静态 SSR 不会执行 `OnAfterRenderAsync`，因此需要宿主配置交互式渲染。BaseUrl 是**浏览器可访问地址**；同源 Cookie 可直接使用。首版 Razor 包装没有 bearer-token 参数，跨域或自定义授权请通过同源 BFF / 反向代理，或直接使用 TypeScript 的可定制 source。

## 文字内边距（Padding）

PDF 和 Canvas 的共享文字区域现在默认左右留 3、上下留 2 个逻辑单位；默认缩放下等于 PDF point。内边距在后端排版前应用，因此左对齐、右对齐、居中、两端对齐及自动换行均使用同一个缩小后的内容区域。背景、边框、图片和网格尺寸不受默认文字内边距影响。

类库可设置全局默认值：

```csharp
var options = new PdfRenderOptions
{
    DefaultTextPadding = new PaddingSpec(left: 4, top: 2, right: 4, bottom: 2)
};
var preview = SkiaPdfRenderer.Instance.CreatePreview(document, options);
byte[] pdf = preview.ToPdfBytes();
```

已有的单元格、行、列样式 Padding 优先，不与默认值叠加。例如 Fluent 单元格设置 `.Padding(6, 2, 6, 2)`，或通过 `CellStyle.Padding` 设置。显式 `.Padding(0)` 可让该单元格恢复零内边距；全局设置 `DefaultTextPadding = PaddingSpec.Zero` 可恢复旧版文字区域。

Excel 模板 API 可在 `appsettings.json` 的 `PrintApi` 节配置：

```json
{
  "TextPaddingHorizontal": 3,
  "TextPaddingVertical": 2
}
```

也可使用环境变量 `PrintApi__TextPaddingHorizontal` / `PrintApi__TextPaddingVertical`。值必须有限且不小于 0；两者设为 0 可恢复旧版。此默认值应用于 PDF 和 Canvas，不改变 Excel 文件或 Windows 渲染器的默认行为。

文字可用宽高会相应减少，长文本可能提前换行；固定行高不自动增加。已有快照不会随配置更新，重启服务后需要重新创建预览。模板经过 Excel 格式化中转，因此 HTTP 场景使用上述服务设置，不承诺在 Excel 文件中往返保存任意四边 Padding。

## Display List v1

页面接口返回 `{ documentId, revision, page, resources }`，资源字典按 SHA-256 内容哈希去重。`page` 包含 `schemaVersion: 1`、`unit: "pt"`、`pageNumber`、`width`、`height`、`commands`。

| op | 字段与含义 |
|---|---|
| `save` / `restore` | 成对保存/恢复绘制状态 |
| `clipRect` | `values: [x, y, width, height]`，与当前裁剪区域相交 |
| `fillRect` | 同上，加 `color` |
| `line` | `values: [x1, y1, x2, y2]`、`color`、`strokeWidth`、可选 `dash` |
| `image` | 矩形 `values` 和 `resourceId`，拉伸至指定区域 |
| `glyphRun` | `color`、`glyphs: [{ resourceId, x, y, size }]` |

- 坐标左上原点、Y 向下，页面尺寸沿用 PDF 的 0.01 point 舍入。
- 颜色统一 `#RRGGBBAA`，注意与 Core 的 `ColorSpec.FromHex` 输入约定不同。
- 路径资源 `{ kind: "path", data: "SVG path data" }` 是字号 1 point 的字形轮廓，基线原点为 `(0,0)`。先按 `size` 缩放，再平移到 `(x,y)`；斜体倾斜已包含在轮廓中。
- 轮廓按 nonzero 规则填充，字形空白可使用空路径。
- 图片资源 `{ kind: "png", data: "PNG base64" }`，后端统一转换 PNG，客户端不访问外部图片 URL。
- 线端点为 butt，dash phase 为 0；双线由后端展开为两条线。
- 未使用字段可以省略或为 null；不支持的版本/操作会明确报错。
- 前端必须校验 documentId 和 revision，避免文档切换时混用旧页。

首版没有任意 transform / SVG 文档命令，不执行资源中的脚本或 HTML。

## 类库与自定义文字渲染器

```csharp
using PrintSharp.Pdf;

var snapshot = SkiaPdfRenderer.Instance.CreatePreview(document);
var page = snapshot.Pages[0];
byte[] pdf = snapshot.ToPdfBytes();
```

`CreatePreview` 生成只读绘制计划，原 Document 的后续修改不会影响它。直接类库调用沿用原 PDF 文字值规则；金额/日期格式化的 Excel 中转属于 API 的输入准备流程，不会自动添加到裸 `Document` 调用中。

默认 `NotoPdfTextRenderer` 实现 `IDisplayListTextRenderer`。现有仅实现 `IPdfTextRenderer` 的自定义渲染器仍可输出 PDF，但 `CreatePreview` 明确抛出 `NotSupportedException`。要支持预览，实现 `AppendText`，通过 `DisplayListBuilder.DrawGlyphRun` 添加已塑形字形，或通过 `Add` 添加支持的图形命令。不要自行构造 `glyphRun` 命令，PDF 需要 `DrawGlyphRun` 同时保留内部字体信息。

使用自建 `NotoFontRegistry` 或自定义 SKTypeface 时，必须保持字体对象存活直到快照 PDF 输出完成。API 在同一个生成并发槽内完成快照序列化和 PDF 生成，缓存只保存最终字节，不持有字体对象。

## 缓存、部署与资源限制

配置在 `PrintApi` 节，可使用同名环境变量覆盖：

| 配置 | 默认值 |
|---|---:|
| `PreviewTtlSeconds` | 900 秒，固定期限，读取不续期 |
| `MaxPreviewSessions` | 32 |
| `MaxPreviewDocumentBytes` | 32 MiB |
| `MaxPreviewCacheBytes` | 128 MiB，须不小于单文档上限 |

大小统计为 manifest、各页 JSON（包含该页资源）和 PDF 的字节总和。此限制约束缓存内容，不是进程内存硬上限；模板解析、文字排版、序列化以及 HTTP 在途响应还有临时内存。创建仍受 `MaxConcurrency`、处理超时及已有模板/单元格上限约束。取消在排版单元格和 PDF 页边界检查，不能强制中断正在执行的原生调用。

过期记录每分钟清理，也会在创建及读取过程中清理。满额时返回 503，不静默驱逐有效快照。业务使用结束后建议 DELETE，降低内存占用。

当前服务与原版本一样没有自带登录。接入认证中间件后，快照归属使用 issuer、tenant 和稳定用户 ID；跨身份读取/删除返回 404。匿名部署的随机 ID 是访问凭据，生产中应通过宿主认证保护创建、读取和下载路由，并避免公开记录 ID。跨域调用仍需宿主配置 CORS；Cookie 身份场景沿用宿主 CSRF 策略。

## 构建与验证

```powershell
npm --prefix packages/preview ci
npm --prefix packages/preview exec playwright install chromium
npm --prefix packages/preview test
dotnet test tests/PrintSharp.Api.Tests
dotnet test tests/PrintSharp.Pdf.Tests
dotnet build src/PrintSharp.Preview.Blazor
```

`npm test` 会先生成 .NET 多语言样例，再运行 Chromium 测试：Canvas / Skia 页面像素容差比较、翻页竞争、缩放、容器变化、销毁、协议拒绝，以及 Vue v-model / 卸载。样例写入 `artifacts/preview-tests`，包含页面 JSON、Skia PNG 和 PDF。像素测试允许少量边缘差异，不代表所有 PDF 查看器的逐像素验证。

API 测试覆盖两种创建方式、物理页数据、ETag、页码边界、同快照 PDF 的金额格式、删除、容量限制和身份隔离。既有 PDF 测试继续验证多语言文字对象、分页、元数据、边框、图片、斜体和两端对齐。Blazor RCL 与完整 Interactive Server demo 均通过编译及 Chromium 端到端验收；Vue demo 通过生产构建及同样的真实 API 验收。完整解决方案 196 项 .NET 测试通过，控件浏览器测试 5 项（包含延迟绘制下连续缩放无空白帧回归）、独立 demo 浏览器测试 2 项通过。WebAssembly 宿主、Firefox、WebKit 和本次改动的 Linux 运行验证尚未完成。

文字内边距追加验证：PDF 覆盖默认值、显式覆盖、零值、缩放、无剩余空间、背景位置与 PDF 字形坐标；API 覆盖配置后的共享裁剪区域。
