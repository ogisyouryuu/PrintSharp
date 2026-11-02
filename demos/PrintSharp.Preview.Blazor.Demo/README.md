# Blazor Preview Demo

独立的 .NET 10 Blazor Interactive Server 项目，引用 `PrintSharp.Preview.Blazor` RCL。不是静态 HTML 包装：上传由 Razor 的 `InputFile` 接收，服务器向真实 PrintSharp.Api 创建快照，浏览器控件通过同源转发获取页面，页码事件回传 Razor 状态。

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
