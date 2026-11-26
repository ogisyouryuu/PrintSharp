<div align="center">

# PrintSharp

**面向 .NET 的 Grid-first 文档生成库**

[![.NET 11](https://img.shields.io/badge/.NET-11.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PrintSharp](https://img.shields.io/nuget/v/PrintSharp.svg?label=PrintSharp)](https://www.nuget.org/packages/PrintSharp/)
[![PrintSharp.Excel](https://img.shields.io/nuget/v/PrintSharp.Excel.svg?label=PrintSharp.Excel)](https://www.nuget.org/packages/PrintSharp.Excel/)
[![PrintSharp.Pdf](https://img.shields.io/nuget/v/PrintSharp.Pdf.svg?label=PrintSharp.Pdf)](https://www.nuget.org/packages/PrintSharp.Pdf/)
[![PrintSharp.Html](https://img.shields.io/nuget/v/PrintSharp.Html.svg?label=PrintSharp.Html)](https://www.nuget.org/packages/PrintSharp.Html/)
[![PrintSharp.Image](https://img.shields.io/nuget/v/PrintSharp.Image.svg?label=PrintSharp.Image)](https://www.nuget.org/packages/PrintSharp.Image/)
[![PrintSharp.Word](https://img.shields.io/nuget/v/PrintSharp.Word.svg?label=PrintSharp.Word)](https://www.nuget.org/packages/PrintSharp.Word/)
[![PrintSharp.Windows](https://img.shields.io/nuget/v/PrintSharp.Windows.svg?label=PrintSharp.Windows)](https://www.nuget.org/packages/PrintSharp.Windows/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[English](README.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md)

</div>

用一份类 Excel 的网格文档导出 **Excel**、**PDF**、**HTML**、**PNG**、**无损 WebP** 或**可编辑的 Word（DOCX）**。浏览器预览和 Windows 打印也使用同一份网格。

> **状态：** 早期库（`0.5.1`）。稳定版本前，公开 API 和渲染行为可能会演进。

HTTP 微服务：[PrintSharp.Api](src/PrintSharp.Api/README.md) 支持 Base64 或 multipart 上传 Excel 模板、类型化 JSON 多表数据，并返回 PDF 或 XLSX 文件。运行命令：`dotnet run --project src/PrintSharp.Api --urls http://127.0.0.1:5080`。

Canvas 预览的 TypeScript / Vue / Blazor API 见[预览包 README](packages/preview/README.md)。预览 API 按页返回 JSON Display List，并从同一快照下载 PDF；支持翻页、缩放和适配页面。[运行浏览器演示](packages/preview/README.md)。

## TypeScript、Vue 与 Blazor 预览

PDF 与 Canvas 共用不可变绘制计划，包括物理分页、换行、内边距和字形位置。Canvas 回放字形轮廓，不重新塑形文字；抗锯齿效果可能不同。

- **连续滚动：** 支持单页与连续模式切换，仅为可见页及邻页创建画布，约束页面缓存和画布像素预算。缩放保留阅读位置，重绘期间保留已完成画面。
- **缩略图：** 可选虚拟化侧栏支持点击跳页和当前页高亮，加载与绘制优先让位于正文。
- **搜索：** 按需获取 `/text` 索引，支持不区分大小写的字面搜索、结果计数、上一处/下一处和文字框高亮。关键词最多 256 个 UTF-16 代码单元，结果最多 1,000 处。
- **选择与复制：** 拖选后按 Ctrl+C / Command+C 或点击“复制”，保留多语言逻辑原文顺序。鼠标起止位置按实际文字框计算；demo 的选区摘要预留固定高度，防止拖选时纸张移动。

使用 `npm i @printsharp/preview` 安装预览包，使用 `dotnet add package PrintSharp.Preview.Blazor` 安装 Blazor 组件。Vue 入口为 `@printsharp/preview/vue`，ASP.NET Core 服务端安装 `PrintSharp.Preview.AspNetCore`。API 和部署方式见[预览包 README](packages/preview/README.md)。

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
- 可在浏览器查看和打印的独立 HTML
- 按页导出 PNG 和无损 WebP
- 可编辑的 DOCX 表格
- Windows `PrintDocument` 渲染和打印
- 文本、数值、日期、公式和图片字节数组单元格值
- 字体、颜色、边框、内边距、对齐、换行和数字格式

## 安装

按应用所需的输出目标安装包：

```bash
dotnet add package PrintSharp
dotnet add package PrintSharp.Excel
dotnet add package PrintSharp.Pdf
dotnet add package PrintSharp.Html
dotnet add package PrintSharp.Image
dotnet add package PrintSharp.Word
dotnet add package PrintSharp.Fonts.NotoSansCjk
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

引用 `PrintSharp.Html` 后，可以将文档导出为独立 HTML，在浏览器中查看和打印。文字的默认内边距与 PDF 一致。

```csharp
using PrintSharp.Html;

string html = document.ToHtml();
document.SaveAsHtml("sales-report.html");
```

引用 `PrintSharp.Image` 可以使用与 PDF 相同的绘制计划和字体，将每个物理页导出为一张图。文件名自动加页码，例如 `sales-report-1.webp`。WebP 使用无损压缩，默认分辨率为 144 DPI。

```csharp
using PrintSharp.Image;

document.SavePageImages("sales-report.png");
document.SavePageImages("sales-report.webp", dpi: 300);
```

引用 `PrintSharp.Word` 可以把报表导出为可编辑的 Word 表格。字体与换行由 Word 处理，编辑后可能重新分页。

```csharp
using PrintSharp.Word;

document.SaveAsDocx("sales-report.docx");
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
PDF、HTML、PNG、WebP、DOCX 和 Windows 打印使用计算后的物理页。Excel 默认每个物理页生成一个工作表。
原始文档不变。`PageValue` 在整个文档分页结束后求值；`BodyRowOffset` 和 `BodyRowCount` 表示当前页明细网格的行范围，一条记录对应一行时可直接用来计算每页小计。

Excel 模板使用 `ExcelTemplateParser.Instance.RenderReport(path, data, settings)` 启用分页。
每个工作表须包含一行 `Items`（或指定集合名称）的重复明细，之前和之后的行分别作为重复页头、页尾。
`PageValue` 同时支持独立占位符、文本内嵌占位符和 DataSet 汇总表中的值。

分页按定义的实际行高计算，先预留页头、页尾高度，再填入完整明细行。跨行合并的行组整体换页。
行组超过整页可用高度、合并单元格跨越区域边界、列宽超过可打印宽度时明确报错。
自定义纸张须提供正的宽高。暂不根据文本折行自动测量行高；页尾紧随明细，末页不拉伸行高。

## Excel 报表输出与公式

`ExcelReportLayout.SingleSheet` 为每个逻辑 Report 生成连续工作表，支持打印标题和可选冻结页头。默认 `PerPhysicalPage` 按物理页生成工作表。

`SumBodyColumn(column, value)` 使用从 0 开始的列索引，自动生成输出工作表的实际明细范围，应放在报表页头或页尾。空明细生成 `=0`，缓存值和打印值均为 0。分页小计使用 `PageValue` 提供对应页的缓存值。原有 `Formula(...)` 不变，手写公式仍需自行处理空明细和倒序范围。

`PageContext.IsContinuous` 在 SingleSheet 中为 true，在分页输出中为 false，即使 PDF 只有一页也能区分。返回空字符串即可隐藏“本页”文字，页尾行本身仍保留。

```csharp
var items = Enumerable.Range(1, 50).ToArray();
var document = Document.Create(d => d.Page(p => p
    .Settings(s => s.PaperKind(PaperKind.A4).Margins(24))
    .Columns(100, 120)
    .Report(r => r
        .Header(h => h.Row(24, row => { row.Cell("No"); row.Cell("Amount"); }))
        .Body(items, 20, (row, item) => { row.Cell(item); row.Cell(item * 1000); })
        .Footer(f => f.Row(24, row =>
        {
            row.Cell(new PageValue(page => page.IsContinuous ? "" : $"Page: {page.BodyRowCount}"));
            row.Cell(c => c.SumBodyColumn(1, value: new PageValue(page =>
                items.Skip(page.BodyRowOffset).Take(page.BodyRowCount).Sum(x => x * 1000)))
                .Format("#,##0"));
        })))));

var excel = document.ToExcelBytes(new ExcelRenderOptions
{
    ReportLayout = ExcelReportLayout.SingleSheet,
    FreezeHeaderRows = true
});
var pdf = document.ToPdfBytes();
```

公式缓存由 Excel、PDF、Canvas 共用；无缓存的未支持公式按 `FormulaWithoutValue` 处理。使用 `Document.Culture` 统一数值和日期格式。预览 v2 跨页共享字形数据，支持多语言控件、PDF 下载文件名及显式释放服务端会话。详见[集成指南](doc/canvas-preview.zh-CN.md)。

PDF 依赖拉丁字体包 `PrintSharp.Fonts.NotoSans`。中日韩追加 `PrintSharp.Fonts.NotoSansCjk`，中日韩衬线字体追加 `PrintSharp.Fonts.NotoSerifCjk`，阿拉伯语追加 `PrintSharp.Fonts.NotoSansArabic`；`PrintSharp.Fonts.Noto` 可安装全部四种字体。

## NuGet 包

PDF 使用 SkiaSharp、随包携带的 Noto 字体和 HarfBuzz 排版。发布时保留 `Fonts/`、`Licenses/` 及原生运行库。`SaveAsPdf()`、`ToPdfBytes()` 保留调用方式，`ToPdfDocument()` 返回只读的 `RenderedPdfDocument`。

| 包 | 用途 | 主要依赖 |
| --- | --- | --- |
| `PrintSharp` | 网格文档模型、样式、Fluent API 与布局引擎 | — |
| `PrintSharp.Excel` | `.xlsx` 渲染器和模板解析器 | ClosedXML |
| `PrintSharp.Pdf` | PDF 渲染器 | SkiaSharp |
| `PrintSharp.Windows` | Windows 打印渲染器 | `PrintDocument` / GDI+ |
| `PrintSharp.Html` | 独立 HTML 导出 | HTML / CSS |
| `PrintSharp.Image` | 按页导出 PNG、无损 WebP | SkiaSharp |
| `PrintSharp.Word` | 可编辑的 DOCX 导出 | Open XML SDK |
| `PrintSharp.Preview.AspNetCore` | 预览 HTTP 接口与会话 | ASP.NET Core |
| `PrintSharp.Preview.Blazor` | Blazor 预览组件 | Blazor |
| `PrintSharp.Fonts.NotoSans` / `NotoSansCjk` / `NotoSerifCjk` / `NotoSansArabic` | 可选字体包（完整包名共用 `PrintSharp.Fonts.` 前缀） | Noto fonts |
| `PrintSharp.Fonts.Noto` | 全部四种字体包 | Font packages |

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

解决方案包含网格模型、布局、Excel 模板、PDF、HTML、PNG、WebP、DOCX 和 Windows 打印的测试。

## 许可证

基于 [MIT License](LICENSE) 发布。

## Windows Forms 演示

[WinForms 演示程序](demos/PrintSharp.WinForms.Demo)包含可编辑的 100 行 DataGridView、代码定义和 Excel 模板驱动的报表、日本酒店收据与发票、Excel、PDF、HTML、PNG、WebP 和 DOCX 导出及嵌入式打印预览。仅支持在 Windows 上运行：

```bash
dotnet run --project demos/PrintSharp.WinForms.Demo
```

## NuGet 与 npm 发布

推送未使用的 `vMAJOR.MINOR.PATCH` 标签，会以同一版本发布 14 个 NuGet 包和 `@printsharp/preview`。NuGet 工作流构建并测试解决方案、验证包内容与消费项目，然后发布到 NuGet.org 和 GitHub Packages；npm 工作流构建预览包并携带 provenance 发布。

```bash
git tag v0.5.1
git push origin v0.5.1
```

两端均使用 Trusted Publishing。配置 `NUGET_USER` 和 NuGet.org 针对 `ogisyouryuu/PrintSharp`、`nuget.yml` 的策略，并在 npm 配置 `npm.yml` trusted publisher。本机不需要 API Key，不要复用已发布的版本号。
