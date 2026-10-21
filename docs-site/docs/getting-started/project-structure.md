# 项目结构

以下清单来自 `PrintSharp.slnx` 及当前项目文件；只列出已存在并加入解决方案的项目。

## 产品代码

- `src/PrintSharp`：文档模型、网格布局、分页及 Fluent API 等核心能力。
- `src/PrintSharp.Excel`：Excel 模板解析与 ClosedXML 渲染，引用 Core 和 Source Generator。
- `src/PrintSharp.Pdf`：基于 SkiaSharp / HarfBuzz 的 PDF 输出及随包字体、许可资产。
- `src/PrintSharp.Windows`：Windows `PrintDocument` 渲染适配器。
- `src/PrintSharp.Api`：ASP.NET Core HTTP 服务，引用 Excel 和 PDF 项目。
- `src/PrintSharp.Excel.SourceGenerator`：面向 Excel 模板绑定的 Roslyn Source Generator。

## 验证与示例

- `tests/`：Core、Excel、API、PDF 和 Windows 项目的 xUnit 测试；`PrintSharp.Excel.AotSmoke` 是单独的控制台冒烟程序。
- `demos/PrintSharp.WinForms.Demo`：Windows Forms 报表演示，含 Excel 模板。
- `demos/PrintSharp.Multilingual.Demo`：多语言输出控制台演示。
- `benchmarks/PrintSharp.Excel.Benchmarks`：BenchmarkDotNet Excel 基准项目。

## 文档与工程资料

- 根目录 `README.md`、`README.zh-CN.md`、`README.ja.md`：GitHub 主页的英语、简体中文和日语介绍。
- `docs-site/docs/`：本门户的中文 Markdown 源文件。
- `docs-site/`：独立的 VitePress 配置、Node 依赖和静态站点构建产物。
- `doc/`：迁移、发布和验证记录，以及微服务实施规格等现有资料。
- `eng/`：Linux 多语言 PDF 验证脚本。
- `src/PrintSharp.Pdf/Fonts` 与 `Licenses`：随 PDF 包分发的字体与许可说明。

规划中的系统组件不混入当前项目清单。API 的 OpenAPI 页面或规范目前不在仓库配置中；参见[API 参考](/api-reference/)。
