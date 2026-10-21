# 开发环境

仓库项目使用 .NET 10。CI 工作流安装 .NET SDK 10.0.401；项目目标框架为 `net10.0`，Windows 项目为 `net10.0-windows`，Source Generator 为 `netstandard2.0`。仓库未使用 `global.json` 固定本机 SDK 补丁版本。

## 所需工具

- .NET 10 SDK：用于还原、构建和运行测试。
- Windows：构建 `PrintSharp.Windows`、WinForms 示例及整个当前解决方案时需要 Windows Desktop targeting/runtime。
- Node.js 22 或更高版本与 npm：仅在开发、构建文档门户时需要；门户不属于 .NET 运行时依赖。
- Git：获取源码和查看工作区变更。

Linux 上可构建不依赖 Windows 的项目；当前 CI 在 Windows 上构建整个解决方案。Linux API 发布的原生依赖和验证记录见[部署文档](/operations/deployment)。

## 检查环境

以下命令在仓库根目录执行：

```powershell
dotnet --info
dotnet --list-sdks
node --version
npm --version
git status --short
```

解决方案入口为 `PrintSharp.slnx`。项目文件位于 `src/`、`tests/`、`demos/` 和 `benchmarks/`。本机安装信息可能与 CI 使用的补丁版本不同；以项目目标框架和工作流声明为准。

## 文档站环境

在 `docs-site/` 执行 `npm ci` 后可使用文档站命令。Node/npm 依赖与 C# 项目完全分离；部署时只需托管 VitePress 输出的静态文件。
