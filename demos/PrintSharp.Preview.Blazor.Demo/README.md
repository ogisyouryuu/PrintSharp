# Blazor Preview Demo

独立的 .NET 11 Blazor Interactive Server 项目，引用 `PrintSharp.Preview.Blazor` RCL。不是静态 HTML 包装：上传由 Razor 的 `InputFile` 接收，服务器向真实 PrintSharp.Api 创建快照，浏览器控件通过同源转发获取页面，页码事件回传 Razor 状态。

## 启动

从仓库根目录启动 API：

```powershell
dotnet run --no-launch-profile --project src/PrintSharp.Api --urls http://127.0.0.1:5080
```

另开终端启动 Blazor：

```powershell
dotnet run --no-launch-profile --project demos/PrintSharp.Preview.Blazor.Demo --urls http://127.0.0.1:5200
```

访问 <http://127.0.0.1:5200>。前端 JS 已随 RCL 提交，因此运行 Blazor demo 不需要 Node.js；修改 TypeScript 后才需要在根目录执行 `npm --prefix packages/preview run build` 更新资源。

API 地址默认 `http://127.0.0.1:5080`，可设置环境变量 `PrintApiUrl`。演示项目只转发预览的 GET / DELETE 路由，不能作为通用生产网关；它没有用户认证，生产宿主应按完整指南配置身份与授权。

## 验收

1. 等待交互连接就绪，点击“使用内置两页示例”，应显示 **2 页、当前第 1 页、绘制完成**。
2. 点击整页、下一页、放大、缩小，验证 Canvas 变化以及 Razor 显示的页码更新。
3. 下载同一快照 PDF，与浏览器的两页内容对比。
4. 下载示例模板，再通过 InputFile 上传；修改 JSON 的 `T.Value` 后重新生成，应替换现有控件快照，显示新内容。

浏览器端到端自动验收与 Vue 共用，运行步骤见 [Vue demo README](../PrintSharp.Preview.Vue/README.md)。生成 `artifacts/preview-tests/blazor-demo.png` 和 `blazor-demo.pdf`。

实现入口：`Components/Pages/Home.razor`；同源转发与交互配置：`Program.cs`。此 demo 验证 Interactive Server，未包含独立 WebAssembly demo。

## 阶段 A 验收

生成示例后，点击工具栏“连续滚动”，滚动到第二页，确认页码跟随；在页中间放大/缩小，确认保留阅读位置。点击“单页”应保留当前页码。自动端到端测试也覆盖这一流程，并生成 `artifacts/preview-tests/vue-continuous.png` / `blazor-continuous.png`。千页虚拟化和内存预算边界由共享控件的浏览器测试覆盖。

## 阶段 B 验收

点击工具栏“缩略图”，左侧应显示两页缩略图；点击任一缩略图，正文跳转且当前页边框高亮，页面摘要显示“缩略图已打开”。分别在单页与连续滚动模式验收，并通过正文翻页确认侧栏高亮同步。再次点击“缩略图”关闭侧栏，摘要同步显示“缩略图已关闭”。

自动验收包含以上操作，截图位于 `artifacts/preview-tests/blazor-thumbnails.png`。千页侧栏虚拟化、正文优先取消慢请求、失败重试与关闭清理由共享控件浏览器测试覆盖。

## 阶段 C 验收

生成两页样例后，点击“搜索”，输入两页都有的 `PrintSharp` 或 `中文`。确认结果计数和页面外的搜索摘要同步，点击“下一处 / 上一处”能够循环跨页定位，命中的文字范围高亮。切换单页/连续模式和缩放后，高亮应随页面调整。输入不存在的关键词应显示“无匹配结果”；关闭搜索应清除高亮。

搜索按单元格原文匹配；新快照根据后端文字框高亮，完全被裁剪的原文仍可搜索，但不会显示文字高亮。自动测试上传 `Updated / 更新 / العربية`，验证两页搜索及状态回传，截图位于 `artifacts/preview-tests/blazor-search.png`。后端需要最新版本的 `/text` 接口；升级后重新生成快照。

## 阶段 D 验收

升级 API 后重新生成示例。工具栏“文字选择”默认开启；拖选中日韩文或阿拉伯文，页面摘要应显示“已选择”。按 Ctrl+C / Command+C 或点击“复制”，粘贴到文本编辑器，核对逻辑原文及换行。放大、缩小后再次选择，确认文字层贴合正文。关闭“文字选择”后应清除选区，重新打开可继续选择。

连续模式仅支持当前已加载页面间的选择；滚到远处导致旧页释放时，选区可能清除。自动验收检查框架 SelectionChanged / selectionchange 状态和实际剪贴板内容；截图位于 `artifacts/preview-tests/blazor-selection.png`。

拖选回归：从一个字缓慢拖到一个词，确认摘要出现和变长时纸张位置保持不变，复制内容仅包含所选文字。摘要固定单行高度，未选择时显示“未选择文字”；长文本仅在摘要中省略，不截断剪贴板内容。
