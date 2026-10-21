# 构建与测试

解决方案入口为根目录 `PrintSharp.slnx`。CI 工作流在 Windows 上还原、Release 构建并运行整套测试。

## 构建

在仓库根目录执行：

```powershell
dotnet restore PrintSharp.slnx
dotnet build PrintSharp.slnx -c Release --no-restore
```

也可用 `dotnet build PrintSharp.slnx` 同时执行隐式还原。Windows 打印和 WinForms 项目以 `net10.0-windows` 为目标。

## 测试

单元测试和 API 集成测试分别位于 `tests/PrintSharp.Tests`、`PrintSharp.Excel.Tests`、`PrintSharp.Api.Tests`、`PrintSharp.Pdf.Tests` 和 `PrintSharp.Windows.Tests`。在仓库根目录执行：

```powershell
dotnet test PrintSharp.slnx -c Release
```

其中 API 测试使用 WebApplicationFactory 覆盖 HTTP 路由和请求处理；PDF 测试验证渲染输出。该命令运行解决方案中标记为测试项目的项目。

## 冒烟与发布验证

`tests/PrintSharp.Excel.AotSmoke` 是单独的控制台冒烟项目，不是测试框架项目。CI 当前不运行它；需要时可在仓库根目录执行：

```powershell
dotnet run --project tests/PrintSharp.Excel.AotSmoke
```

Rocky Linux 的多语言 PDF 真实 HTTP 验证脚本为 `eng/verify-multilingual-pdf.sh`，需在具备 .NET 10、脚本要求的 Linux 依赖及 `poppler-utils` 的 Rocky 环境执行。历史验证详情及适用范围见[多语言 PDF 验证记录](https://github.com/ogisyouryuu/PrintSharp/blob/main/doc/multilingual-pdf-validation.md)。

上述内容记录的是仓库入口和已有工作流，不表示本页编辑期间重新运行过这些 .NET 检查。
