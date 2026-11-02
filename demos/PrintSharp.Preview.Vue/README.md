# Vue 3 Preview Demo

独立的 Vue 3 + TypeScript + Vite 项目，通过本地 `@printsharp/preview/vue` 组件调用真实 PrintSharp.Api。支持内置两页样例、自定义 Excel 上传、JSON 编辑、页码双向绑定、缩放和同快照 PDF 下载。

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
