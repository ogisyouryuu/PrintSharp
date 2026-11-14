<div align="center">

# PrintSharp

**面向 .NET 的 Grid-first 文档生成库**

[![.NET 11](https://img.shields.io/badge/.NET-11.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PrintSharp](https://img.shields.io/nuget/v/PrintSharp.svg?label=PrintSharp)](https://www.nuget.org/packages/PrintSharp/)
[![PrintSharp.Excel](https://img.shields.io/nuget/v/PrintSharp.Excel.svg?label=PrintSharp.Excel)](https://www.nuget.org/packages/PrintSharp.Excel/)
[![PrintSharp.Pdf](https://img.shields.io/nuget/v/PrintSharp.Pdf.svg?label=PrintSharp.Pdf)](https://www.nuget.org/packages/PrintSharp.Pdf/)
[![PrintSharp.Windows](https://img.shields.io/nuget/v/PrintSharp.Windows.svg?label=PrintSharp.Windows)](https://www.nuget.org/packages/PrintSharp.Windows/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[English](README.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md)

</div>

使用一份类 Excel 的网格文档，同时稳定输出为 **Excel**、**PDF** 或 **Windows 打印机**。PrintSharp 以行、列和单元格跨度维护布局，而非自由的 `X` / `Y` 坐标；Excel 网格始终是布局的来源。

> **状态：** 早期库（`0.1.1`）。稳定版本前，公开 API 和渲染行为可能会演进。

HTTP 微服务：[PrintSharp.Api](src/PrintSharp.Api/README.md) 支持 Base64 或 multipart 上传 Excel 模板、类型化 JSON 多表数据，并返回 PDF 或 XLSX 文件。运行命令：`dotnet run --project src/PrintSharp.Api --urls http://127.0.0.1:5080`。

Canvas 预览的 TypeScript / Vue / Blazor API 见[预览包 README](packages/preview/README.md)。预览 API 按页返回 JSON Display List，并从同一快照下载 PDF；支持翻页、缩放和适配页面。[运行浏览器演示](packages/preview/README.md)。

## TypeScript、Vue 与 Blazor 预览

PDF 与 Canvas 共用不可变绘制计划，包括物理分页、换行、内边距和字形位置。Canvas 回放字形轮廓，不重新塑形文字；抗锯齿效果可能不同。

- **连续滚动：** 支持单页与连续模式切换，仅为可见页及邻页创建画布，约束页面缓存和画布像素预算。缩放保留阅读位置，重绘期间保留已完成画面。
- **缩略图：** 可选虚拟化侧栏支持点击跳页和当前页高亮，加载与绘制优先让位于正文。
- **搜索：** 按需获取 `/text` 索引，支持不区分大小写的字面搜索、结果计数、上一处/下一处和文字框高亮。关键词最多 256 个 UTF-16 代码单元，结果最多 1,000 处。
- **选择与复制：** 拖选后按 Ctrl+C / Command+C 或点击“复制”，保留多语言逻辑原文顺序。鼠标起止位置按实际文字框计算；demo 的选区摘要预留固定高度，防止拖选时纸张移动。

预览 npm 包和 Blazor RCL 目前通过源码接入，尚未发布到 npm / NuGet。Vue 使用 `@printsharp/preview/vue`，Blazor 引用 `src/PrintSharp.Preview.Blazor`。方法、双向绑定、事件和部署文件见[预览包 README](packages/preview/README.md)。

先在 5080 端口启动 API，再另开终端运行任一 demo：

| Demo | 启动与验收说明 | 默认地址 |
| --- | --- | --- |
| Vue 3 + TypeScript | [Vue demo](demos/PrintSharp.Preview.Vue/README.md) | http://127.0.0.1:5199 |
| Blazor Interactive Server | [Blazor demo](demos/PrintSharp.Preview.Blazor.Demo/README.md) | http://127.0.0.1:5200 |

两个 demo 都包含两页样例、Excel 上传、JSON 编辑和同快照 PDF 下载。安装依赖及 Chromium 后，运行 `npm --prefix packages/preview test` 验证控件；运行 `npm --prefix packages/preview run test:demos` 验证两个 demo 与真实 API 的集成，包括鼠标拖选和剪贴板内容。复用已有服务及更改 Blazor 端口的方法见 demo README。

选区仅覆盖当前已加载页面；虚拟化释放旧页时可能清除选区，尚无整份长文档的持久选区。完全被裁剪的原文仍可搜索，但没有可见的文字高亮或选择框。复制按钮需要 HTTPS/localhost 和浏览器剪贴板授权。已验证桌面 Chromium；触屏选择、Firefox/WebKit 和完整语义无障碍结构尚未专项验证。服务端快照目前使用单进程内存缓存。

## 为什么是 PrintSharp？

传统报表系统常先用绝对坐标布局，再尝试转换为 Excel。微小的坐标差异就可能额外生成 Excel 列，导致原有布局崩坏。

PrintSharp 从 Excel 兼容的网格开始：

```text
网格文档（行 + 列 + 跨度 + 样式）
                │
                ▼
             布局引擎
                │
    ┌───────────┼───────────┐
    ▼           ▼           ▼
  Excel        PDF       Windows
ClosedXML    SkiaSharp  PrintDocument
```

核心模型不绑定任何渲染器。只有在输出边界，渲染器才将逻辑尺寸映射为其自身的物理单位。

## 功能

- Grid-first 的 `Document` / `Page` / `Row` / `Column` / `Cell` 模型
- Excel A1 地址和合并区域，例如 `A1:C1`
- 文档、行、表格、单元格和样式的 Fluent API
- 共享的物理单元格边界布局计算
- 基于 ClosedXML 的 Excel 导出与导入
- Excel 模板占位符绑定和重复行展开
- 基于 SkiaSharp 的 PDF 导出
- Windows `PrintDocument` 渲染和打印
- 文本、数值、日期、公式和图片字节数组单元格值
- 字体、颜色、边框、内边距、对齐、换行和数字格式

## 安装

按应用所需的输出目标安装包：

```bash
dotnet add package PrintSharp
dotnet add package PrintSharp.Excel
dotnet add package PrintSharp.Pdf
dotnet add package PrintSharp.Windows
```

`PrintSharp.Windows` 面向 Windows，使用 Windows Forms / `PrintDocument`；核心、Excel 和 PDF 项目面向 .NET 11。

## 快速开始

使用 Fluent API 创建一份文档，再输出到所需目标：

```csharp
using PrintSharp.Documents;
using PrintSharp.Excel;
using PrintSharp.Fluent;
using PrintSharp.Pdf;

var document = Document.Create(d =>
{
    d.Title("销售报表")
     .Author("Contoso")
     .Page("汇总", p =>
     {
         p.Columns(180f, 70f, 110f)
          .Rows(32f, 24f, 24f, 24f)
          .Cell("A1:C1", c => c.Value("销售报表")
                              .Font("Microsoft YaHei", 16f, bold: true)
                              .AlignCenter()
                              .Background("#F3F4F6"))
          .Cell("A2", c => c.Value("商品").Bold())
          .Cell("B2", c => c.Value("数量").Bold().AlignRight())
          .Cell("C2", c => c.Value("金额").Bold().AlignRight())
          .Cell("A3", "笔记本")
          .Cell("B3", c => c.Value(2).AlignRight())
          .Cell("C3", c => c.Value(19.98m).Format("¥#,##0.00").AlignRight())
          .Cell("A4", "合计")
          .Cell("C4", c => c.Formula("=SUM(C3:C3)")
                              .Format("¥#,##0.00")
                              .AlignRight());
     });
});

document.SaveAsExcel("sales-report.xlsx");
document.SaveAsPdf("sales-report.pdf");
```

若需要 Windows 打印，请引用 `PrintSharp.Windows` 并调用：

```csharp
using PrintSharp.Windows;

document.Print();
```

## 用 Excel 设计模板

现有工作簿可解析为相同的网格模型。使用 `{{Property}}` 填充值；在模板行中使用 `{{Collection.Property}}`，即可按集合中的每项重复该行。

```csharp
using PrintSharp.Excel;
using PrintSharp.Pdf;

var invoice = new
{
    Customer = new { Name = "Ada Lovelace" },
    InvoiceNo = "INV-001",
    Items = new[]
    {
        new { Name = "笔记本", Quantity = 2, Price = 9.99m },
        new { Name = "钢笔", Quantity = 3, Price = 1.50m }
    }
};

// 在 invoice-template.xlsx 中使用 {{Customer.Name}}、{{InvoiceNo}}，
// 以及包含 {{Items.Name}}、{{Items.Quantity}}、{{Items.Price}} 的模板行。
var document = ExcelTemplateParser.Instance.Render("invoice-template.xlsx", invoice);

document.SaveAsExcel("invoice.xlsx");
document.SaveAsPdf("invoice.pdf");
```

## 纸张与页数

通过 `page.Settings = new PageSettings { PaperKind = PaperKind.A4 }` 选择纸张，
或在 Fluent API 中使用 `p.Settings(s => s.PaperKind(PaperKind.A4))`。
支持 A5、B5、A4、B4、A3 和 Customer；B 系列采用 ISO 尺寸。
标准尺寸以点（72 点/英寸）表示，横向布局自动交换宽高。
自定义尺寸使用 `s.Size(300, 400)`，这会选择 Customer；默认 Customer 保留内容自适应行为。
`page.CurrentPageNumber` 与 `page.PageNumber` 同步，`document.PageCount` 实时返回页面/工作表数量，
不代表打印机对工作表自动分页后的物理页数。

## 报表自动分页

自动分页是显式启用的追加功能。既有 `Table()`、模板 `Render()` 和手动创建的页面保持原有行为。
先在页面上配置纸张、余白和列宽，再定义重复页头、明细和重复页尾：

```csharp
using PrintSharp.Documents;
using PrintSharp.Fluent;

int[] items = Enumerable.Range(1, 100).ToArray();
var document = Document.Create(d => d.Page(p => p
    .Settings(s => s.PaperKind(PaperKind.A4).Margins(24))
    .Columns(100)
    .Report(r => r
        .Header(h => h.Row(30, row => row.Cell("编号")))
        .Body(items, 24, (row, item) => row.Cell(item))
        .Footer(f => f.Row(30, row => row.Cell(new PageValue(ctx =>
            $"{ctx.CurrentPageNumber} / {ctx.PageCount}")))))));

int logicalPages = document.PageCount;
int physicalPages = document.CalculateLayout().PageCount;
Document physicalDocument = document.Paginate();
```

每个区域的行索引从 0 开始。`Body(Action<PageBuilder>)` 也支持不同的行高和合并单元格。
已有网格可直接使用 `p.Paginate(headerRowCount: 3, footerRowCount: 1)`，声明页头、页尾行数。
PDF、Windows 打印和 Excel 导出时自动分页；Excel 每个物理页生成一个工作表。
原始文档不变。`PageValue` 在整个文档分页结束后求值；`BodyRowOffset` 和 `BodyRowCount` 表示当前页明细网格的行范围，一条记录对应一行时可直接用来计算每页小计。

Excel 模板使用 `ExcelTemplateParser.Instance.RenderReport(path, data, settings)` 启用分页。
每个工作表须包含一行 `Items`（或指定集合名称）的重复明细，之前和之后的行分别作为重复页头、页尾。
`PageValue` 同时支持独立占位符、文本内嵌占位符和 DataSet 汇总表中的值。

分页按定义的实际行高计算，先预留页头、页尾高度，再填入完整明细行。跨行合并的行组整体换页。
行组超过整页可用高度、合并单元格跨越区域边界、列宽超过可打印宽度时明确报错。
自定义纸张须提供正的宽高。暂不根据文本折行自动测量行高；页尾紧随明细，末页不拉伸行高。

## NuGet 包

PDF 使用 SkiaSharp、随包携带的 Noto 字体和 HarfBuzz 排版。发布时保留 `Fonts/`、`Licenses/` 及原生运行库。`SaveAsPdf()`、`ToPdfBytes()` 保留调用方式，`ToPdfDocument()` 返回只读的 `RenderedPdfDocument`。

| 包 | 用途 | 主要依赖 |
| --- | --- | --- |
| `PrintSharp` | 网格文档模型、样式、Fluent API 与布局引擎 | — |
| `PrintSharp.Excel` | `.xlsx` 渲染器和模板解析器 | ClosedXML |
| `PrintSharp.Pdf` | PDF 渲染器 | SkiaSharp |
| `PrintSharp.Windows` | Windows 打印渲染器 | `PrintDocument` / GDI+ |

## 设计原则

1. **Grid-first 布局。** 单元格由行、列、`RowSpan`、`ColumnSpan` 定位；自由坐标不是主模型。
2. **统一布局计算。** 布局引擎从网格计算物理边界，PDF 与 Windows 输出共享相同几何关系。
3. **核心独立于渲染器。** 核心使用逻辑尺寸和 `FontSpec`；Excel 列宽、PDF point、GDI+ 单位及具体字体都处于核心之外。
4. **Excel 是一等设计界面。** Excel 工作簿既可作为输出，也可作为 PDF 或打印输出的可视化模板。

完整架构说明请见：[PrintSharp 架构：Excel Grid-first](src/PrintSharp/PrintSharp_Architecture_ExcelGridFirst_v2_中文版.md)。

## 开发

```bash
dotnet test PrintSharp.slnx
```

解决方案包含核心网格/布局、Fluent API、样式、Excel 渲染与模板、PDF 渲染和 Windows 打印适配器的单元测试。

## 许可证

基于 [MIT License](LICENSE) 发布。

## Windows Forms 演示

[WinForms 演示程序](demos/PrintSharp.WinForms.Demo)包含可编辑的 100 行 DataGridView、代码定义和 Excel 模板驱动的报表、日本酒店收据与发票、Excel/PDF 导出及嵌入式打印预览。仅支持在 Windows 上运行：

```bash
dotnet run --project demos/PrintSharp.WinForms.Demo
```

## NuGet 发布

推送新的 `vMAJOR.MINOR.PATCH` Git 标签后，GitHub Actions 会构建并测试解决方案、验证四个 NuGet 包，然后通过 Trusted Publishing 发布到 NuGet.org。本机不需要 API Key。

```bash
git tag v0.1.2
git push origin v0.1.2
```

请使用尚未发布的版本号和标签。
首次发布前，还需在 GitHub Actions variables 中设置 `NUGET_USER`，并在 NuGet.org Trusted Publishing 中授权仓库 `ogisyouryuu/PrintSharp` 的 `nuget.yml` 工作流发布 `PrintSharp*` 包。
