# Vue 3 Preview Demo

独立的 Vue 3 + TypeScript + Vite 项目，通过本地 `@printsharp/preview/vue` 组件调用真实 PrintSharp.Api。支持内置两页样例、自定义 Excel 上传、JSON 编辑、页码双向绑定、缩放和同快照 PDF 下载。

TypeScript 保持在 6.0.3：当前稳定版 vue-tsc 3.3.14 依赖 TypeScript 的 `lib/tsc` 入口，与 TypeScript 7 不兼容。待 vue-tsc 支持后再升级主版本。

## 启动

以下命令都从仓库根目录执行。先启动 API：

```powershell
dotnet run --no-launch-profile --project src/PrintSharp.Api --urls http://127.0.0.1:5080
```

另开终端：

```powershell
npm --prefix packages/preview ci
npm --prefix packages/preview run build
npm --prefix demos/PrintSharp.Preview.Vue ci
npm --prefix demos/PrintSharp.Preview.Vue run dev
```

访问 <http://127.0.0.1:5199>。Vite 将 `/api` 代理到 5080；可用 `PRINTSHARP_API` 环境变量覆盖。生产构建执行 `npm --prefix demos/PrintSharp.Preview.Vue run build`；部署 dist 时宿主需提供同源 `/api` 代理。

## 验收

1. 点击“使用内置两页示例”，应显示 **2 页、当前第 1 页、绘制完成**。
2. 点击“整页”查看完整纸张，检查中日韩阿拉伯文、金额格式、表格边框和斜体装饰。
3. 点击“下一页”，第二页标题和背景变化，Vue 状态显示第 2 页。
4. 点击放大、缩小、适合宽度、整页，页面按比例变化且不重新换行。
5. 下载同一快照 PDF，核对两页内容、字形位置、表格和分页。
6. 下载示例模板，选择它上传，修改 JSON 的 `T.Value` 后点击“上传并预览”；应回到第 1 页并显示新内容。

真实浏览器自动验收：先关闭占用 5080 / 5199 / 5200 的手动 demo，再从根目录运行：

```powershell
npm --prefix packages/preview exec playwright install chromium
npm --prefix packages/preview run test:demos
```

该命令自动启动真实 API、Vue、Blazor，验证两个项目，结束后关闭服务。生成截图及下载 PDF 位于 `artifacts/preview-tests/vue-demo.png` / `vue-demo.pdf`。

实现入口：`src/App.vue`。共用样例位于 `demos/PreviewSamples`，协议和部署限制见 [完整指南](../../doc/canvas-preview.zh-CN.md)。

## 阶段 A 验收

生成示例后，点击工具栏“连续滚动”，滚动到第二页，确认页码跟随；在页中间放大/缩小，确认保留阅读位置。点击“单页”应保留当前页码。自动端到端测试也覆盖这一流程，并生成 `artifacts/preview-tests/vue-continuous.png` / `blazor-continuous.png`。千页虚拟化和内存预算边界由共享控件的浏览器测试覆盖。

## 阶段 B 验收

点击工具栏“缩略图”，左侧应显示两页缩略图；点击任一缩略图，正文跳转且当前页边框高亮，页面摘要显示“缩略图已打开”。分别在单页与连续滚动模式验收，并通过正文翻页确认侧栏高亮同步。再次点击“缩略图”关闭侧栏，摘要同步显示“缩略图已关闭”。

自动验收包含以上操作，截图位于 `artifacts/preview-tests/vue-thumbnails.png`。千页侧栏虚拟化、正文优先取消慢请求、失败重试与关闭清理由共享控件浏览器测试覆盖。

如果已有最新版本服务运行，可设置 `$env:PREVIEW_TEST_REUSE_SERVERS = "1"` 后运行端到端测试；测试会保留复用的服务，只关闭自己启动的服务。Blazor 使用其他端口时，例如 5201，设置 `$env:PREVIEW_TEST_BLAZOR_PORT = "5201"`。默认不复用已有服务，避免误测旧版本。

## 阶段 C 验收

生成两页样例后，点击“搜索”，输入两页都有的 `PrintSharp` 或 `中文`。确认结果计数和页面外的搜索摘要同步，点击“下一处 / 上一处”能够循环跨页定位，命中的文字范围高亮。切换单页/连续模式和缩放后，高亮应随页面调整。输入不存在的关键词应显示“无匹配结果”；关闭搜索应清除高亮。

搜索按单元格原文匹配；新快照根据后端文字框高亮，完全被裁剪的原文仍可搜索，但不会显示文字高亮。自动测试上传 `Updated / 更新 / العربية`，验证两页搜索及状态回传，截图位于 `artifacts/preview-tests/vue-search.png`。后端需要最新版本的 `/text` 接口；升级后重新生成快照。

## 阶段 D 验收

升级 API 后重新生成示例。工具栏“文字选择”默认开启；拖选中日韩文或阿拉伯文，页面摘要应显示“已选择”。按 Ctrl+C / Command+C 或点击“复制”，粘贴到文本编辑器，核对逻辑原文及换行。放大、缩小后再次选择，确认文字层贴合正文。关闭“文字选择”后应清除选区，重新打开可继续选择。

连续模式仅支持当前已加载页面间的选择；滚到远处导致旧页释放时，选区可能清除。自动验收检查框架 SelectionChanged / selectionchange 状态和实际剪贴板内容；截图位于 `artifacts/preview-tests/vue-selection.png`。

拖选回归：从一个字缓慢拖到一个词，确认摘要出现和变长时纸张位置保持不变，复制内容仅包含所选文字。摘要固定单行高度，未选择时显示“未选择文字”；长文本仅在摘要中省略，不截断剪贴板内容。
