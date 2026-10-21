# 故障排查

本页只记录仓库已有验证支持的结论。未在代码或验证记录中证实的原因列为建议检查项。

## 已验证的解决办法

- **文档构建找不到站点内容**：在 `docs-site/` 安装 lockfile 指定的依赖，再运行 `npm run docs:build`；构建输出位于 `docs-site/.vitepress/dist/`。
- **API 根路径返回 404**：API README 说明根路径 `/` 没有页面。使用 `GET /api/v1/health` 检查本机服务状态。
- **Linux API 缺少字体或原生库**：部署时保留完整 `dotnet publish` 输出，包括 `Fonts/`、`Licenses/` 及 Skia/HarfBuzz 库；Rocky 所需系统包见[部署](/operations/deployment)。
- **Rocky PDF 字体异常**：先对照 [多语言 PDF 验证记录](https://github.com/ogisyouryuu/PrintSharp/blob/main/doc/multilingual-pdf-validation.md) 的系统依赖、发布文件和 Poppler 检查方式。记录所验证的是 Rocky 9.6/10.2 x64，不等于任意 Linux/VPS 环境。

## 建议检查项（尚未验证为通用修复）

- **VPS 上页面资源 404**：确认文档 `base` 与实际挂载路径一致，并将整个构建目录内容部署到站点根目录。
- **API 上传返回 413**：核对 API `PrintApi` 请求/模板限制和前置代理的请求体上限；不要为了绕过限制而无评估地提高值。
- **Linux API 启动失败**：检查目标架构、完整发布文件、Rocky 系统依赖及日志；其他发行版需单独核实所需原生库。
- **特定工作簿渲染差异**：查看 API 错误详情、模板结构和相应 Excel/PDF 测试；不要将未经复现的猜测当作已知缺陷。
