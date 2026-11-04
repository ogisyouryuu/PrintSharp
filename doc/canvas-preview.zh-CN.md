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

一致性指物理分页、页面尺寸、字形位置、换行、图片位置和裁剪区域一致；浏览器与 PDF 查看器的抗锯齿、hinting、图片采样可能不同，不承诺逐像素相同。PDF 保留文字对象；Canvas 已支持逻辑原文搜索、文字选择和纯文本复制；尚未提供完整语义文档无障碍结构。工具栏支持键盘和读屏标签。

预览页数是物理分页后的页数，不是 `Document.PageCount`。缩放不会重新排版：

```text
CSS 尺寸 = PDF point × 96 / 72 × zoom
画布像素尺寸 = CSS 尺寸 × 有效 devicePixelRatio
```

画布默认限制 1600 万像素、单边不超过 16384 像素；超限时降低像素密度，CSS 尺寸和排版不变。最大缩放为 800%，最小为 10%；自动适配可以低于 10%。

## 首版范围与方案调整

- 创建时生成整份布局、页面 JSON 和 PDF 快照，之后翻页只读取缓存；尚未实现按需生成页面或后台任务队列。
- 每页响应内附该页使用的去重资源，避免逐字形 HTTP 请求。跨页可能重复发送相同资源；尚未提供独立资源端点或资源协商协议。
- 浏览器默认 LRU 缓存最多 3 页、序列化数据最多 16 MiB。阶段 A 已支持连续滚动及邻页加载；尚未实现跨页 Path2D 缓存和 Worker。
- 服务端采用单进程内存缓存。重启会丢失快照，多实例需会话粘滞；当前没有 Redis / 持久化实现。
- 不新增 Excel 格式化规则或公式计算规则，继续复用现有 PDF 输入准备流程。

## 阶段 A：连续滚动与虚拟化

工具栏新增“单页”和“连续滚动”。默认仍为单页，现有初始化代码不需要修改。

```ts
const preview = createPrintPreview(container, {
  source,
  viewMode: 'continuous',
  zoom: 'fit-width',
  cachePages: 6,
  maxCacheBytes: 16 * 1024 * 1024,
  maxTotalCanvasPixels: 24_000_000
});
await preview.ready;
await preview.goToPage(100);
preview.setViewMode('single-page');
```

- 根据 manifest 的逐页尺寸预先计算滚动空间。仅可见页及前后各一页创建 DOM 和 Canvas，移出范围立即释放画布；不是给所有页面保留隐藏 Canvas。
- 页码跟随视口中心所在页。滚轮和 PageUp / PageDown 用于自然滚动，页码输入及上下页按钮用于定位。
- 缩放和容器尺寸变化会记录视口中心的页号、页内相对位置，然后恢复阅读位置；文档首尾会受浏览器滚动边界约束。
- 连续模式各页使用相同缩放比例。适合宽度以最宽页面计算；整页模式同时考虑最大页面宽高，避免混合横竖页面撑出视口。
- 保留已绘制的页面供缩放过渡使用，再异步替换为新清晰度。可见中心页优先，邻页随后绘制；离开视口或切换模式会取消过期请求。
- 单页画布仍受 `maxCanvasPixels` 约束；连续模式另外用 `maxTotalCanvasPixels` 分配总像素预算，包含为绘制缓冲预留的份额。可见页变多时降低像素密度，保持 CSS 页面尺寸与排版不变。
- `maxCacheBytes` 按页面响应 JSON 的 UTF-8 字节数计算，并与 `cachePages` 同时约束 LRU。单页数据超过预算时仍可绘制，但不进入缓存。正在绘制的数据、图片解码、浏览器内部开销不等于缓存字节数，配置不是整个进程的内存硬上限。
- `viewmodechange` 事件返回 `single-page` / `continuous`；`viewMode` 可读取当前模式，`cacheUsage` 返回当前缓存页数和统计字节。连续模式的 `pagechange` 表示阅读位置变化，不要求对应页面已绘制完毕。

Vue 使用 `v-model:view-mode="viewMode"`，初始值可设为 `'continuous'`。Blazor 使用 `ViewMode="continuous"` 或 `@bind-ViewMode`，并提供 `SetViewModeAsync`。工具栏切换会回传框架状态，不重新创建文档会话。

阶段 A 浏览器回归覆盖 1000 页混合尺寸、远距离跳页、滚动页码同步、缩放和容器变化后的阅读位置、缓存页数/字节/画布像素上限、过期请求取消、失败重试及单页兼容。此处千页输入由浏览器测试构造，真实 API demo 使用两页模板；不是千页 Excel 后端生成性能基准。极长文档叠加高倍缩放仍受浏览器最大滚动区域尺寸限制。缩略图、搜索和文字选择不属于阶段 A。

直接托管 ESM 时需要同时发布同目录的 `dist/index.js`、`dist/continuous.js`、`dist/thumbnails.js`、`dist/search.js` 和 `dist/selection.js`。Blazor RCL 的 `preview.js`、`continuous.js`、`thumbnails.js`、`search.js` 与 `selection.js` 由同一次构建同步生成。

## 阶段 B：缩略图侧栏

工具栏“缩略图”按钮控制侧栏，默认关闭；单页和连续滚动模式均支持点击跳页、当前页高亮，以及焦点位于缩略图时用上下方向键切换页面。正文页码变化时，侧栏自动滚动到当前页。

```ts
const preview = createPrintPreview(container, { source, thumbnails: true });
await preview.ready;
preview.setThumbnailsVisible(false);
preview.addEventListener('thumbnailschange', event => {
  console.log((event as CustomEvent<boolean>).detail);
});
console.log(preview.thumbnailsVisible);
```

Vue 使用 `v-model:thumbnails="thumbnails"`，对应 `ref(false)`；Blazor 使用 `Thumbnails="true"` 或 `@bind-Thumbnails`，提供 `ThumbnailsChanged` 回调和 `SetThumbnailsVisibleAsync(bool)` 方法。开关不会创建新文档会话，两个独立 demo 都会在摘要中显示侧栏状态。

- 侧栏仅创建可见缩略图与前后各一项，移出范围释放 Canvas。每项画面适配 120 × 124 CSS 像素，每张 Canvas 最多 60,000 像素。
- 正文加载或绘制时暂停侧栏并取消在途任务；正文空闲 120 ms 后启动缩略图队列，串行绘制并在任务间让出事件循环。关闭侧栏会取消请求、释放 Canvas 和监听器。
- 优先读取已缓存页面；缩略图独立取得的 Display List 不进入正文 LRU，也不改变缓存淘汰顺序。再次滚回已释放的缩略图可能重新请求。
- 缩略图失败保留页码占位，错误可从按钮提示查看；工具栏“重试”会重试当前窗口失败项，不影响已显示的正文。
- 复用相同的页面 Display List 和 CanvasRenderer，不改变 PDF 排版。低分辨率仅减少画布像素，仍需下载完整页面 JSON；本阶段没有新增服务端缩略图片接口，图片较多的文档仍有网络和解码成本。

新增浏览器测试覆盖千页虚拟化、点击与高亮、独立滚动、画布限制、不挤占正文缓存、正文抢占慢请求、局部失败与关闭清理。Vue / Blazor 真实 API 验收均包含侧栏操作及状态回传。阶段 C 已增加搜索，阶段 D 已增加文字选择。


## 阶段 C：全文搜索

点击“搜索”输入关键词，可查看结果计数并使用“上一处 / 下一处”循环定位。Enter 跳到下一处，Shift+Enter 跳到上一处，Escape 关闭搜索。输入防抖 250 ms；关闭会清除查询与高亮，并取消未完成的索引获取。单页与连续模式均支持定位，缩略图和当前页状态随跳转更新。

搜索**格式化后的单元格逻辑原文**。阶段 D 的新快照使用后端文字框高亮命中部分；旧快照或没有文字框的自定义渲染器仍以单元格内容区域高亮。自动换行不会打断单元格内的匹配，显式换行属于原文。被完全裁剪掉的原文仍可搜索并跳页，但新快照不会给不可见文字绘制高亮。

- 采用 Unicode 模式、不区分大小写的字面子串匹配，不执行用户正则；不做重音折叠、Unicode 归一化或全半角转换。不跨单元格、物理页拼接匹配，重复页眉按每个物理页独立计数。
- 按物理页、单元格布局顺序、文本偏移返回不重叠结果。关键词最多 256 个 UTF-16 代码单元，单次最多保留 1,000 个结果，超限显示“已达上限”。同一单元格多个命中分别计数，相邻命中文字框在同一行合并显示。
- 首次查询调用 `GET /api/v1/previews/{id}/text` 获取全篇轻量索引，不下载全篇字形和图片。索引在当前控件中复用，不占正文 LRU；关闭搜索保留索引，销毁控件释放。索引仍为全篇数据，内存随文字量增长，不是流式服务端搜索。
- 索引属于同一不可变快照，受身份、过期、ETag 和服务端容量限制；客户端校验 documentId、revision、页码与有限区域坐标。改词或销毁会取消旧查询，过期与加载失败显示在搜索栏，再次提交可重试。
- 高亮为独立 SVG 覆盖层，不修改 Canvas 像素或 PDF。默认查看时不请求索引，旧版自定义 source 可继续预览；调用搜索会提示不支持。

```ts
await preview.search('1,234.50');
await preview.nextSearchResult();
await preview.previousSearchResult();
console.log(preview.searchState);
preview.addEventListener('searchchange', event => {
  console.log((event as CustomEvent).detail);
});
preview.closeSearch();
```

`searchState` / `searchchange` 为 `{ query, total, current, busy, truncated, error? }`；current 从 1 开始，无结果为 0。程序调用失败通过 Promise 报告，UI 操作失败显示在搜索栏。

Vue 监听 `@searchchange`；通过组件 ref 的 `getPreview()` 调用上述方法。Blazor 提供 `SearchChanged`（`PreviewSearchState`）、`SearchAsync(string)`、`NextSearchResultAsync()`、`PreviousSearchResultAsync()` 和 `CloseSearchAsync()`。两个 demo 均展示回传的查询和结果位置。

自定义 source 可增加可选方法 `getTextIndex(signal): Promise<TextIndex>`，返回：

```json
{
  "schemaVersion": 1,
  "documentId": "与 manifest 一致",
  "revision": "与 manifest 一致",
  "pages": [{ "pageNumber": 1, "regions": [
    { "text": "格式化后的原文", "x": 20, "y": 30, "width": 150, "height": 24 }
  ] }]
}
```

坐标单位为 PDF point，左上原点，pages 必须包含全部物理页并按页号排序。Display List v1 页面新增兼容字段 `textRegions`，绘制命令不变；自定义字体渲染器也由共享布局层记录输入原文和内容区域。`/text` 仅提取这些语义数据，不附带资源或绘制命令。

## 阶段 D：文字选择与复制

宿主监听 selectionchange 时，应为选区摘要预留固定高度，或将反馈放在不影响预览布局的位置。拖选期间动态插入状态行、让长文本撑高摘要，会移动纸张并使浏览器选区跳到其他行。两个 demo 的摘要均固定为单行，过长内容省略显示，复制结果仍保留完整文本。

默认启用透明文字层，鼠标拖选按文字框计算起止边界，再建立浏览器选区，避免透明 span 横向缩放后的原生命中偏差。拖动鼠标可选择正文，按 Ctrl+C / Command+C 或点击“复制”写入纯文本。工具栏“文字选择”可开关，关闭会清除当前选区。Canvas 仍然绘制字形，透明层仅参与选择，不改变 PDF / Canvas 排版。

后端在原有 RichTextKit 排版后输出 UTF-16 文字范围、坐标、行高和方向。组合字符、代理对按文字元素处理，合字中的零宽元素与邻接范围合并；前端按框放置透明 span，使用浏览器字体测量适配选择宽度，不参与正文换行。选择框表示文字排版范围而非字形墨迹边界，斜体外伸和浏览器原生选区外观可能有细小差异。

- 复制按物理页、单元格和文字偏移的逻辑顺序拼接，阿拉伯文不会按视觉顺序倒转。单元格内保留原始空白和显式换行，自动换行不新增换行；不同单元格和页面以换行分隔，不重建 Excel 表格或制表符。
- 部分选择一个组合字符或合字范围时，复制完整文字元素。完全处于裁剪区外的字符不进入文字层；部分可见元素按完整元素复制。跳过的不可见非空白文字不会混入复制结果。
- 连续模式仅为可见页和邻页创建文字层，DOM 顺序保持物理页序。可以选择当前已加载页面之间的文字；旧页被虚拟化释放、文档替换或模式切换后，不承诺保留选区。没有实现跨任意多页的持久选区或整份文档全选。
- 同一页面缩放时更新文字层尺寸，不重新塑形可见文字。页面重新加载时可能重建文字层；关闭选择隐藏当前层，销毁控件释放 DOM 和全局监听器。
- Ctrl+C / Command+C 使用浏览器 copy 事件。工具栏“复制”和 `copySelection()` 使用 Clipboard API，需要安全上下文（HTTPS / localhost）、浏览器授权及用户手势；失败会显示错误，可改用快捷键。Windows 剪贴板可能将 LF 转成 CRLF。
- 当前验收针对 Chromium 桌面。触屏选择手柄、Firefox / WebKit、完整键盘选区操作和屏幕阅读器语义结构尚未专项验收。字体覆盖范围没有扩展：内置字体不支持的 Emoji 仍遵循已有后端错误规则。

```ts
const preview = createPrintPreview(container, { source, textSelection: true });
preview.addEventListener('selectionchange', event => {
  console.log((event as CustomEvent<string>).detail);
});
console.log(preview.selectedText);
preview.setTextSelection(false);
preview.clearSelection();
// ユーザーのクリックハンドラー内から呼び出します。
copyButton.onclick = () => preview.copySelection();
```

`textSelection` 返回当前开关，`textselectionchange` 返回 boolean；`selectedText` 和 `selectionchange` 返回当前选区的逻辑纯文本。没有选区时为 `''`。Vue 使用 `v-model:text-selection`（默认 true）及 `@selectionchange`，其他方法通过 `getPreview()` 调用。Blazor 使用 `TextSelection` / `@bind-TextSelection`、`SelectionChanged`、`SetTextSelectionAsync(bool)`、`ClearSelectionAsync()`。Blazor 推荐直接使用内置复制按钮，以便在浏览器点击事件中保留剪贴板用户手势。

`textRegions` 及 `/text` 中每个 region 新增可选 `boxes`：

```json
{ "start": 0, "length": 1, "x": 20, "y": 30, "width": 12, "height": 18, "rtl": false }
```

start / length 为 region.text 的 UTF-16 偏移，坐标仍为页面 PDF point。boxes 为 null / 缺省表示不支持文字选择，空数组表示没有可见文字框。自定义 `IDisplayListTextRenderer` 可在 AppendText 中调用 `builder.SetTextBoxes(...)` 提供映射；不提供时 PDF 与 Canvas 仍正常绘制，搜索退回单元格区域高亮。文字框增加页面 JSON 和索引大小，继续计入已有服务端容量及前端页面缓存预算。

阶段 D 验证包括真实鼠标拖选、剪贴板内容、软换行与显式换行、阿拉伯文逻辑顺序、组合字符及增补平面汉字、裁剪、文字框搜索高亮、缩放、连续页 DOM 顺序与释放。PDF 30 项、API 94 项、控件浏览器 16 项、两个真实 demo 端到端测试通过。

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
| GET | `/api/v1/previews/{id}/text` | 全篇逻辑文字与区域索引 |
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

构建后可直接托管 `packages/preview/dist/index.js` 及同目录的 `continuous.js`、`thumbnails.js`、`search.js`、`selection.js`。需要本地 npm 包时，在该目录运行 `npm pack`，然后在消费项目安装生成的 `.tgz`。

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

大小统计为 manifest、各页 JSON（包含该页资源和文字区域）、独立文字索引和 PDF 的字节总和。此限制约束缓存内容，不是进程内存硬上限；模板解析、文字排版、序列化以及 HTTP 在途响应还有临时内存。创建仍受 `MaxConcurrency`、处理超时及已有模板/单元格上限约束。取消在排版单元格和 PDF 页边界检查，不能强制中断正在执行的原生调用。

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

API 测试覆盖两种创建方式、物理页数据、ETag、页码边界、同快照 PDF 的金额格式、删除、容量限制和身份隔离。既有 PDF 测试继续验证多语言文字对象、分页、元数据、边框、图片、斜体和两端对齐。Blazor RCL 与完整 Interactive Server demo 均通过编译及 Chromium 端到端验收；Vue demo 通过生产构建及同样的真实 API 验收。此前完整解决方案验证为 196 项 .NET 测试通过；本阶段按改动范围复跑 API / PDF 测试，控件浏览器测试 16 项（包含连续滚动及延迟绘制下连续缩放无空白帧回归）、独立 demo 浏览器测试 2 项通过。WebAssembly 宿主、Firefox、WebKit 和本次改动的 Linux 运行验证尚未完成。

文字内边距追加验证：PDF 覆盖默认值、显式覆盖、零值、缩放、无剩余空间、背景位置与 PDF 字形坐标；API 覆盖配置后的共享裁剪区域。

阶段 C 追加验证：API 94 项、PDF 27 项测试通过，覆盖文字格式化、内容区域与共享裁剪坐标一致、索引 ETag/删除和容量统计；浏览器覆盖多语言字面匹配、跨页循环、缩放和模式切换后的高亮、取消旧请求、版本拒绝、重试与结果上限。两个真实 API demo 端到端流程包含搜索及框架状态回传。
