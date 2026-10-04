<div align="center">

# PrintSharp

**面向 .NET 的 Grid-first 文档生成库**

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[English](README.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md)

</div>

使用一份类 Excel 的网格文档，同时稳定输出为 **Excel**、**PDF** 或 **Windows 打印机**。PrintSharp 以行、列和单元格跨度维护布局，而非自由的 `X` / `Y` 坐标；Excel 网格始终是布局的来源。

> **状态：** 早期库（`0.1.0`）。稳定版本前，公开 API 和渲染行为可能会演进。

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
ClosedXML    PDFsharp  PrintDocument
```

核心模型不绑定任何渲染器。只有在输出边界，渲染器才将逻辑尺寸映射为其自身的物理单位。

## 功能

- Grid-first 的 `Document` / `Page` / `Row` / `Column` / `Cell` 模型
- Excel A1 地址和合并区域，例如 `A1:C1`
- 文档、行、表格、单元格和样式的 Fluent API
- 共享的物理单元格边界布局计算
- 基于 ClosedXML 的 Excel 导出与导入
- Excel 模板占位符绑定和重复行展开
- 基于 PDFsharp 的 PDF 导出
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

`PrintSharp.Windows` 面向 Windows，使用 Windows Forms / `PrintDocument`；核心、Excel 和 PDF 项目面向 .NET 10。

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

## 包

| 包 | 用途 | 主要依赖 |
| --- | --- | --- |
| `PrintSharp` | 网格文档模型、样式、Fluent API 与布局引擎 | — |
| `PrintSharp.Excel` | `.xlsx` 渲染器和模板解析器 | ClosedXML |
| `PrintSharp.Pdf` | PDF 渲染器 | PDFsharp |
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
