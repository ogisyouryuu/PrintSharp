# PrintSharp Architecture

# PrintSharp 架构设计

> **核心定位：Excel Grid-first 的通用文档生成与打印中间件**
>
> PrintSharp 不是 Excel 库、PDF 库，也不是打印机库。 它是一个以 **Excel
> 网格（Grid）为核心布局模型** 的通用文档模型， 再通过不同 Renderer 输出
> Excel、PDF、Windows PrintDocument。
>
> 最重要的目标：
>
> **同一份布局定义，生成 Excel 时不发生"列崩溃"，同时 PDF
> 和实际打印也保持与 Excel 一致的视觉布局。**

------------------------------------------------------------------------

# 1. 为什么 PrintSharp 要采用 Excel Grid-first

传统报表工具经常采用"自由坐标"思路：

``` text
X = 12.3
Y = 35.7
Width = 128.4
Height = 20.2
```

这种方式非常适合：

-   PDF
-   打印机
-   画布
-   GDI+
-   ActiveReports 等打印型报表

但是它存在一个非常严重的问题：

> **自由坐标布局无法天然映射到 Excel 的二维网格。**

例如：

``` text
第一行：

A 的 X = 10.0
B 的 X = 80.0

第二行：

C 的 X = 10.2
D 的 X = 80.0
```

在 PDF / 打印机上：

``` text
A ───────── B
C ───────── D
```

人眼基本看不出问题。

但是转换到 Excel 后：

``` text
A B
C   D
```

由于第二行的 X 坐标与第一行不同，转换器可能被迫创建额外的 Excel Column。

最终出现：

``` text
┌──────┬──────┬──────┐
│  A   │  B   │      │
├──────┼──────┼──────┤
│  C   │      │  D   │
└──────┴──────┴──────┘
```

甚至更严重的"列崩溃"。

这正是 PrintSharp 要解决的核心问题之一。

------------------------------------------------------------------------

# 2. PrintSharp 的根本原则

PrintSharp 不采用：

> PDF / GDI+ 自由坐标 → 再想办法转换 Excel

而采用：

> **Excel Grid → 统一 Document Model → 分别渲染 Excel / PDF / Printer**

即：

``` text
                 Excel Grid
                     │
                     ▼
             PrintSharp Document
                     │
          ┌──────────┼──────────┐
          │          │          │
          ▼          ▼          ▼
        Excel       PDF      Printer
       ClosedXML  PDFsharp   GDI+
```

因此：

> **Excel Grid 是布局的"母坐标系"。**

PDF 和打印机并不是决定布局的对象。

它们只是把 Excel Grid 的布局转换成自己的物理坐标。

------------------------------------------------------------------------

# 3. 核心设计目标

PrintSharp 必须满足以下目标。

## 3.1 Excel 所见即所得

如果一个模板在 Excel 中是：

``` text
┌──────────┬──────┬──────┐
│ 商品名称 │ 数量 │ 金额 │
├──────────┼──────┼──────┤
│ 苹果     │  10  │ 1000 │
└──────────┴──────┴──────┘
```

PrintSharp 读取模板后，内部必须理解为：

``` text
A1  B1  C1
A2  B2  C2
```

而不是：

``` text
X=0
X=83.21
X=142.83
```

------------------------------------------------------------------------

## 3.2 PDF 与打印机忠实复现 Grid

PDF / PrintDocument 需要将：

``` text
Row
Column
RowSpan
ColumnSpan
RowHeight
ColumnWidth
```

转换成：

``` text
X
Y
Width
Height
```

因此：

``` text
Excel Grid
    │
    ▼
Layout Calculator
    │
    ▼
Physical Rect
    │
    ├── PDF
    └── GDI+
```

这样 PDF / 打印机不会产生任意的 X 偏移。

------------------------------------------------------------------------

## 3.3 同一份文档可以输出多个目标

例如：

``` csharp
Document document = ...
```

然后：

``` csharp
excel.Render(document);
pdf.Render(document);
printer.Print(document);
```

三者共享同一个布局模型。

------------------------------------------------------------------------

# 4. PrintSharp 的核心模型

PrintSharp 的核心不是：

``` text
RectF
```

而是：

``` text
Grid Document
```

其基本元素是：

``` text
Cell
```

一个 Cell 首先由：

``` text
Row
Column
RowSpan
ColumnSpan
```

决定位置。

然后才有：

``` text
Value
Type
Style
```

概念：

``` csharp
public sealed class Cell
{
    public int Row { get; init; }

    public int Column { get; init; }

    public int RowSpan { get; init; } = 1;

    public int ColumnSpan { get; init; } = 1;

    public CellType Type { get; init; }

    public object? Value { get; init; }

    public CellStyle Style { get; init; }
}
```

------------------------------------------------------------------------

# 5. Cell 的位置模型

例如：

``` csharp
new Cell
{
    Row = 1,
    Column = 1,
    Value = "商品名称"
}
```

表示：

``` text
A1
```

而：

``` csharp
new Cell
{
    Row = 1,
    Column = 2,
    Value = "数量"
}
```

表示：

``` text
B1
```

------------------------------------------------------------------------

# 6. 合并单元格

例如 Excel：

``` text
┌───────────────────────┐
│       销售明细          │
├────────┬──────┬───────┤
│ 商品   │ 数量 │ 金额  │
└────────┴──────┴───────┘
```

可以表示：

``` csharp
new Cell
{
    Row = 1,
    Column = 1,
    RowSpan = 1,
    ColumnSpan = 3,
    Value = "销售明细"
};
```

也就是：

``` text
A1:C1
```

------------------------------------------------------------------------

# 7. Grid 的真正结构

推荐的核心模型：

``` text
Document
 │
 ├── Pages
 │
 │    └── Page
 │         │
 │         ├── Rows
 │         │
 │         ├── Columns
 │         │
 │         └── Cells
 │
 └── Metadata
```

更简单的第一版也可以：

``` text
Document
 ├── Rows
 ├── Columns
 └── Cells
```

以后再加入 Page。

------------------------------------------------------------------------

# 8. Row 和 Column

Row / Column 是非常重要的核心对象。

例如：

``` csharp
document.Columns[0].Width = 100;
document.Columns[1].Width = 60;
document.Columns[2].Width = 100;
```

以及：

``` csharp
document.Rows[0].Height = 30;
document.Rows[1].Height = 24;
```

这样整个文档的物理布局就可以由：

``` text
Column Width
Row Height
```

决定。

------------------------------------------------------------------------

# 9. 不允许 Cell 自己定义任意 X

这是 PrintSharp 最重要的设计原则之一。

不要把：

``` csharp
Cell.X
Cell.Y
Cell.Width
Cell.Height
```

作为第一层核心布局属性。

因为这会重新走向：

``` text
自由坐标 → Excel 转换
```

最终重新出现列崩溃。

Cell 的第一层定位应该是：

``` text
Row
Column
RowSpan
ColumnSpan
```

------------------------------------------------------------------------

# 10. RectF 的位置

`RectF` 并不是完全不能存在。

它应该是：

> **Layout Engine 根据 Grid 计算出来的最终物理矩形。**

例如：

``` text
Cell
Row = 2
Column = 3
RowSpan = 2
ColumnSpan = 2

        │
        ▼

Layout Engine

        │
        ▼

RectF
X = 160
Y = 80
Width = 140
Height = 48
```

因此：

``` text
Cell Grid Position
        │
        ▼
Layout Engine
        │
        ▼
Physical Rect
```

而不是：

``` text
Cell
 └── X/Y 直接决定布局
```

------------------------------------------------------------------------

# 11. 统一布局计算

PrintSharp 应该存在一个独立的 Layout Engine：

``` text
Document
   │
   ▼
Layout Engine
   │
   ├── Column Width
   ├── Row Height
   ├── RowSpan
   ├── ColumnSpan
   ├── Padding
   ├── Alignment
   └── Border
   │
   ▼
Calculated Layout
```

Renderer 使用 Layout Engine 的结果。

------------------------------------------------------------------------

# 12. Excel Renderer

Excel Renderer 使用：

**ClosedXML**

职责：

``` text
Grid Cell
    ↓
Excel Cell
```

例如：

``` text
PrintSharp Cell
Row = 2
Column = 3
```

直接对应：

``` text
Excel C2
```

Column Width：

``` text
PrintSharp Column Width
        ↓
Excel Column Width
```

Row Height：

``` text
PrintSharp Row Height
        ↓
Excel Row Height
```

这样 PrintSharp 的核心模型与 Excel 天然一致。

------------------------------------------------------------------------

# 13. PDF Renderer

PDF Renderer 使用：

**PDFsharp**

流程：

``` text
Grid Cell
   │
   ▼
Layout Engine
   │
   ▼
RectF
   │
   ▼
PDF drawing
```

例如：

``` text
Column 1 width = 100
Column 2 width = 60
Column 3 width = 100
```

计算：

``` text
Column 1 X = 0
Column 2 X = 100
Column 3 X = 160
```

所以 PDF 中不会出现：

``` text
Column 1 X = 0
Column 2 X = 101.23
```

这种人为漂移。

------------------------------------------------------------------------

# 14. Windows Printer Renderer

Windows Printer Renderer 使用：

``` text
System.Drawing
PrintDocument
GDI+
```

流程：

``` text
Grid Cell
   │
   ▼
Layout Engine
   │
   ▼
Printer Rect
   │
   ▼
Graphics.DrawString / DrawImage / ...
```

由于所有位置来自同一个 Grid：

``` text
Excel
PDF
Printer
```

三者的布局关系保持一致。

------------------------------------------------------------------------

# 15. Cell 内容类型

第一阶段：

``` text
Text
Image
```

未来：

``` text
Barcode
QRCode
Line
Rectangle
Shape
Table
Chart
```

例如：

``` csharp
public enum CellType
{
    Text,
    Image
}
```

未来再扩展。

------------------------------------------------------------------------

# 16. Cell Value

Cell 不应该只拥有：

``` csharp
string Text
```

更建议：

``` csharp
object? Value
```

例如：

``` csharp
Value = "苹果";
Value = 100;
Value = 1234.56m;
Value = DateTime.Now;
Value = image;
```

Renderer 决定如何将 Value 转换成目标格式。

但应避免让 Core 依赖：

``` text
ClosedXML
PDFsharp
GDI+
```

------------------------------------------------------------------------

# 17. Cell Style

Style 独立于 Cell 内容。

概念：

``` csharp
public sealed class CellStyle
{
    public FontSpec? Font { get; init; }

    public Color? ForeColor { get; init; }

    public Color? BackColor { get; init; }

    public BorderSpec? Border { get; init; }

    public HorizontalAlignment HorizontalAlignment { get; init; }

    public VerticalAlignment VerticalAlignment { get; init; }

    public bool WrapText { get; init; }
}
```

以后可以增加：

``` text
Number Format
Indent
Rotation
Text Trimming
Padding
```

------------------------------------------------------------------------

# 18. 为什么 Excel Template 会非常适合 PrintSharp

Excel 本身就是一个非常优秀的可视化 Grid Layout Designer。

用户可以：

``` text
调整列宽
调整行高
合并单元格
设置边框
设置字体
设置颜色
设置对齐
```

PrintSharp 不需要重新发明一个 Layout Designer。

直接：

``` text
Excel
  ↓
PrintSharp Template Parser
  ↓
Grid Document
```

因此 Excel 可以成为：

> **PrintSharp 的可视化报表设计器。**

------------------------------------------------------------------------

# 19. Excel Template 模式

例如模板：

``` text
invoice.xlsx
```

里面：

``` text
A1:C1  合并
A3     {{Customer.Name}}
B3     {{Invoice.No}}
C3     {{Invoice.Total}}
```

程序：

``` csharp
template.Render(data);
```

可以生成：

``` text
invoice.xlsx
```

这是最简单的模板模式。

------------------------------------------------------------------------

# 20. Excel Template → Grid Document

更高级：

``` text
Excel Template
       │
       ▼
Template Parser
       │
       ▼
Grid Document
       │
 ┌─────┼─────┐
 ▼     ▼     ▼
Excel  PDF  Printer
```

这意味着：

> **Excel 模板不仅可以生成 Excel，还可以作为 PDF / 打印报表设计器。**

这正是 PrintSharp 区别于单纯 Excel 模板工具的地方。

------------------------------------------------------------------------

# 21. Data Binding

数据源支持：

``` text
DataSet
DataTable
T
IEnumerable<T>
```

例如：

``` csharp
template.Render(dataSet);
```

或者：

``` csharp
template.Render(invoice);
```

或者：

``` csharp
template.Render(invoices);
```

未来支持：

``` text
{{Customer.Name}}
{{Invoice.Total}}
{{Items}}
```

以及：

``` text
重复行
重复区域
条件区域
```

------------------------------------------------------------------------

# 22. Code-first 模式

擅长写代码的开发者可以完全不使用 Excel 模板。

例如：

``` csharp
var document = new Document();

document.Columns[0].Width = 120;
document.Columns[1].Width = 60;
document.Columns[2].Width = 100;

document.Cells.Add(new Cell
{
    Row = 0,
    Column = 0,
    Value = "商品名称"
});

document.Cells.Add(new Cell
{
    Row = 0,
    Column = 1,
    Value = "数量"
});

document.Cells.Add(new Cell
{
    Row = 0,
    Column = 2,
    Value = "金额"
});
```

数据来自：

``` text
DataSet
```

或者：

``` text
T
```

最终形成：

``` text
Document
```

然后：

``` csharp
excel.Render(document);
pdf.Render(document);
printer.Print(document);
```

------------------------------------------------------------------------

# 23. Code-first 与 Template-first 的统一

最终：

``` text
                  Document
                     ▲
                     │
             ┌───────┴────────┐
             │                │
        Code Builder      Excel Template
             │                │
             │          Template Parser
             │                │
             └───────┬────────┘
                     │
                     ▼
                Grid Document
                     │
             ┌───────┼───────┐
             ▼       ▼       ▼
           Excel    PDF    Printer
```

因此：

> C# 和 Excel 只是两种"生成 Grid Document 的方式"。

而不是两套独立的报表系统。

------------------------------------------------------------------------

# 24. JSON / 微服务模式

未来可以直接把 Grid Document 序列化成 JSON。

例如：

``` json
{
  "columns": [
    { "width": 120 },
    { "width": 60 },
    { "width": 100 }
  ],
  "rows": [
    { "height": 30 },
    { "height": 24 }
  ],
  "cells": [
    {
      "row": 0,
      "column": 0,
      "value": "商品名称"
    },
    {
      "row": 0,
      "column": 1,
      "value": "数量"
    }
  ]
}
```

服务端：

``` text
JSON
 │
 ▼
Grid Document
 │
 ├── Excel
 ├── PDF
 └── Printer
```

------------------------------------------------------------------------

# 25. 微服务

未来可以提供：

``` text
POST /api/render/excel
POST /api/render/pdf
POST /api/print
POST /api/template/render
```

例如：

``` text
Client
  │
  │ JSON
  ▼
PrintSharp Service
  │
  ▼
Grid Document
  │
 ├── ClosedXML
 ├── PDFsharp
 └── GDI+
```

微服务不应该重新实现文档引擎。

它只是：

> **HTTP Transport + PrintSharp Core**

------------------------------------------------------------------------

# 26. 最终架构

``` text
                         DataSet / T
                              │
                              ▼
                    ┌──────────────────┐
                    │   Data Binding   │
                    └────────┬─────────┘
                             │
              ┌──────────────┴──────────────┐
              │                             │
              ▼                             ▼
       C# Code Builder              Excel Template
              │                             │
              │                     Template Parser
              │                             │
              └──────────────┬──────────────┘
                             │
                             ▼
              ┌────────────────────────────┐
              │     PrintSharp Core        │
              │                            │
              │     Grid Document         │
              │                            │
              │  Rows                      │
              │  Columns                   │
              │  Cells                     │
              │  RowSpan / ColumnSpan      │
              │  Styles                    │
              └─────────────┬──────────────┘
                            │
                            ▼
                     Layout Engine
                            │
              ┌─────────────┼─────────────┐
              │             │             │
              ▼             ▼             ▼
           Excel           PDF         Printer
         ClosedXML       PDFsharp       GDI+
              │             │             │
              ▼             ▼             ▼
            .xlsx          .pdf       PrintDocument
```

------------------------------------------------------------------------

# 27. 项目结构

推荐：

``` text
PrintSharp/
│
├── PrintSharp.slnx
├── README.md
├── LICENSE
│
├── src/
│   │
│   ├── PrintSharp/
│   │   ├── Documents/
│   │   ├── Grid/
│   │   ├── Cells/
│   │   ├── Styles/
│   │   ├── Layout/
│   │   ├── Data/
│   │   └── PrintSharp.csproj
│   │
│   ├── PrintSharp.Excel/
│   │   ├── ClosedXmlRenderer.cs
│   │   ├── ExcelTemplateParser.cs
│   │   └── PrintSharp.Excel.csproj
│   │
│   ├── PrintSharp.Pdf/
│   │   ├── PdfSharpRenderer.cs
│   │   └── PrintSharp.Pdf.csproj
│   │
│   └── PrintSharp.Windows/
│       ├── PrintDocumentRenderer.cs
│       └── PrintSharp.Windows.csproj
│
└── tests/
    ├── PrintSharp.Tests/
    ├── PrintSharp.Excel.Tests/
    ├── PrintSharp.Pdf.Tests/
    └── PrintSharp.Windows.Tests/
```

------------------------------------------------------------------------

# 28. NuGet

最终可以发布：

``` text
PrintSharp
PrintSharp.Excel
PrintSharp.Pdf
PrintSharp.Windows
```

依赖关系：

``` text
                    PrintSharp
                        ▲
          ┌─────────────┼─────────────┐
          │             │             │
          │             │             │
 PrintSharp.Excel PrintSharp.Pdf PrintSharp.Windows
          │             │             │
      ClosedXML      PDFsharp       GDI+
```

Core 不依赖任何 Renderer。

------------------------------------------------------------------------

# 29. Renderer 能力差异

虽然 Grid 是统一的，但不同 Renderer 的能力不一定完全一致。

  功能                Excel      PDF   Windows Print
  ---------------- -------- -------- ---------------
  Text                    ✓        ✓               ✓
  Image                   ✓        ✓               ✓
  Font                    ✓        ✓               ✓
  Color                   ✓        ✓               ✓
  Border                  ✓        ✓               ✓
  Alignment               ✓        ✓               ✓
  Row / Column            ✓     计算            计算
  Merge                   ✓     计算            计算
  Excel Formula           ✓        ×               ×
  Excel Chart             ✓        ×               ×
  Excel 专用功能          ✓        ×               ×
  Barcode            Future   Future          Future
  QR Code            Future   Future          Future

------------------------------------------------------------------------

# 30. 一个非常重要的边界

PrintSharp 的目标不是：

> **让 Excel、PDF、打印机的所有功能 100% 相同。**

而是：

> **让三种输出方式共享同一个稳定的 Grid Layout。**

例如：

``` text
Excel Formula
Excel Chart
Excel Macro
```

这些东西本来就是 Excel 专属功能。

它们不应该被硬塞进：

``` text
Grid Document
```

否则核心模型会越来越复杂。

------------------------------------------------------------------------

# 31. 第一阶段实现顺序

不要一次实现全部功能。

## Milestone 1：Grid Core

先实现：

``` text
Document
Row
Column
Cell
CellType
RowSpan
ColumnSpan
CellStyle
Font
Border
Alignment
```

------------------------------------------------------------------------

## Milestone 2：Layout Engine

实现：

``` text
Row Height
Column Width
Cell Position
Merge
Padding
Alignment
```

并能够计算：

``` text
Cell → RectF
```

------------------------------------------------------------------------

## Milestone 3：Excel Renderer

使用：

``` text
ClosedXML
```

首先验证：

> **PrintSharp Grid → Excel**

必须做到布局稳定。

------------------------------------------------------------------------

## Milestone 4：PDF Renderer

使用：

``` text
PDFsharp
```

验证：

> **同一个 Grid → PDF**

------------------------------------------------------------------------

## Milestone 5：Windows Printer Renderer

使用：

``` text
PrintDocument
GDI+
```

验证：

> **同一个 Grid → 实际打印机**

------------------------------------------------------------------------

## Milestone 6：Excel Template

实现：

``` text
Excel Template
      ↓
Grid Document
```

首先只支持：

``` text
文字
样式
行高
列宽
合并
边框
```

------------------------------------------------------------------------

## Milestone 7：Data Binding

支持：

``` text
DataSet
DataTable
T
IEnumerable<T>
```

增加：

``` text
Placeholder
Repeating Rows
Repeating Sections
```

------------------------------------------------------------------------

## Milestone 8：Template → PDF / Printer

实现：

``` text
Excel Template
      ↓
Grid Document
      ↓
PDF / Printer
```

这是 PrintSharp 的核心能力之一。

------------------------------------------------------------------------

## Milestone 9：JSON

实现：

``` text
Grid Document
      ↕
JSON
```

------------------------------------------------------------------------

## Milestone 10：Microservice

最后：

``` text
HTTP
 ↓
JSON
 ↓
Grid Document
 ↓
Excel / PDF / Printer
```

------------------------------------------------------------------------

# 32. Agent 开发规则

Codex / Claude Code / 其他 Coding Agent 在开发 PrintSharp 时必须遵守：

1.  **先阅读本 Architecture 文档，再修改代码。**
2.  `PrintSharp` Core 不得引用 ClosedXML。
3.  `PrintSharp` Core 不得引用 PDFsharp。
4.  `PrintSharp` Core 不得引用 GDI+ / PrintDocument。
5.  Core 的第一坐标体系必须是 `Row / Column / RowSpan / ColumnSpan`。
6.  不得把自由 `X/Y` 坐标作为 Cell 的主要布局模型。
7.  `RectF` 只能作为 Layout Engine 的计算结果或 Renderer
    层的物理布局结果。
8.  Excel Renderer 只能在 `PrintSharp.Excel` 中实现。
9.  PDFsharp 只能在 `PrintSharp.Pdf` 中使用。
10. PrintDocument / GDI+ 只能在 `PrintSharp.Windows` 中使用。
11. Excel Template Parser 属于 Excel 相关项目，不得污染 Core。
12. Core Document Model 必须保持未来 JSON 序列化的可能性。
13. 不得让 Core 依赖 Excel 的 Row/Column 类型。
14. 不得让 PDF Renderer 反向决定 Core 的布局。
15. 不得让打印机 Renderer 反向决定 Core 的布局。
16. Excel Grid 是布局的母模型。
17. 所有 Renderer 必须尽量忠实地表达同一个 Grid。
18. 不要创建一个巨大的 `PrintHelper`。
19. 不要引入没有必要的 ORM。
20. 不要引入没有必要的 DI Framework。
21. 先测试 Grid Core，再实现 Renderer。
22. Public API 必须有 XML Documentation。
23. `0.x` 阶段允许为了改善模型进行 Breaking Change。
24. 在 Core 模型稳定以前，不要实现微服务。
25. 不要为了"未来可能需要"提前实现大量复杂功能。

------------------------------------------------------------------------

# 33. 最核心的设计结论

PrintSharp 的核心不是：

``` text
RectF
```

也不是：

``` text
Excel Cell
```

而是：

``` text
          Excel-like Grid Document
```

它具有：

``` text
Row
Column
RowSpan
ColumnSpan
RowHeight
ColumnWidth
Cell Value
Cell Type
Cell Style
```

然后：

``` text
             Grid Document
                   │
             Layout Engine
                   │
       ┌───────────┼───────────┐
       ▼           ▼           ▼
     Excel        PDF        Printer
```

Excel：

``` text
Grid → Excel Cell
```

PDF：

``` text
Grid → Physical Rect → PDF
```

Printer：

``` text
Grid → Physical Rect → GDI+
```

------------------------------------------------------------------------

# 34. 最终一句话

> **PrintSharp 是一个以 Excel Grid 为母版的通用文档渲染引擎。**

它解决的不是：

> "怎么把 PDF 转成 Excel？"

而是：

> **"从一开始就用 Excel 能稳定表达的 Grid
> 来定义文档，因此同一份布局可以稳定地输出 Excel、PDF 和打印机。"**

这也是 PrintSharp 与传统自由坐标报表系统最大的架构区别。
