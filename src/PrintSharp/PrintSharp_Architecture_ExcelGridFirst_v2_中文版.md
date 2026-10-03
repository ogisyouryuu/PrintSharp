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

同时冻结 **Logical Grid Unit / Dimension Mapping** 边界，不把 ClosedXML、PDF、Printer 的物理单位带入 Core。

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

> **规则 26～40 见第 52 节，作为单位、字体、Layout Engine 边界的补充硬规则。**

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


------------------------------------------------------------------------

# 35. 三个必须在 Core 阶段冻结的基础抽象

到目前为止，PrintSharp 的架构已经可以进一步明确为三个互相独立、但连续工作的基础层：

``` text
① Layout Model
   Excel-like Grid
   Row / Column / Span

        ↓

② Dimension Model
   Logical Grid Unit
   → Excel
   → PDF
   → Printer

        ↓

③ Font Model
   Logical FontSpec
   → Font Resolver
   → actual font resource
```

这三个层次必须明确分开。

原因是：

- Grid 决定“在哪里、占几个格子”
- Dimension 决定“这个格子在目标输出中有多大”
- Font 决定“文字以什么字体资源绘制”

任何一个 Renderer 都不能反过来污染另外两个层次。

------------------------------------------------------------------------

# 36. Dimension Model：Core 不直接使用 ClosedXML 的单位

虽然 Excel 是 PrintSharp 的母布局模型，但是：

> **PrintSharp Core 不应该把 ClosedXML 的内部 Column Width / Row Height 数值直接当成自己的公共单位。**

原因很简单：

ClosedXML 的尺寸最终属于 Excel 文件格式及其实现语义；PDF、Windows Printer、未来 Linux Renderer 并没有同样的单位。

因此 Core 应该定义：

``` text
PrintSharp Logical Grid Unit
```

它是：

> **布局模型中的逻辑尺寸，而不是某个 Renderer 的物理单位。**

例如：

``` csharp
document.Columns[0].Width = 12;
document.Columns[1].Width = 8;
document.Rows[0].Height = 2;
```

这里的 `12 / 8 / 2` 表示 PrintSharp 的逻辑 Grid 尺寸。

Renderer 再进行转换：

``` text
                    Core
             Logical Grid Unit
                     │
       ┌─────────────┼─────────────┐
       ▼             ▼             ▼
     Excel          PDF          Printer
       │             │             │
 Excel width      points /      device /
                  physical      printer unit
                    unit
```

因此：

> **Core 决定比例和 Grid 关系；Renderer 决定最终物理尺寸。**

这样可以避免 PrintSharp 变成一个“ClosedXML API 的包装器”。

------------------------------------------------------------------------

# 37. Row Height / Column Width 的职责必须固定

这是 Grid-first 模型非常重要的一条规则。

``` text
Column
 └── Width

Row
 └── Height
```

而不是：

``` text
Cell
 ├── X
 ├── Y
 ├── Width
 └── Height
```

Cell 只描述：

``` text
Row
Column
RowSpan
ColumnSpan
```

因此一个 Cell 的最终矩形：

``` text
X      = Sum(Column Width)
Y      = Sum(Row Height)

Width  = Sum(covered Column Width)
Height = Sum(covered Row Height)
```

例如：

``` text
Column:
A = 10
B = 20
C = 30

Row:
1 = 5
2 = 8
3 = 10
```

一个：

``` csharp
Row = 1
Column = 1
RowSpan = 2
ColumnSpan = 2
```

覆盖：

``` text
B2:C3
```

其逻辑矩形就是：

``` text
X      = width(A)
Y      = height(row 0) + height(row 1)

Width  = width(B) + width(C)
Height = height(row 1) + height(row 2)
```

这就是 Layout Engine 的职责。

------------------------------------------------------------------------

# 38. RectF 是“结果”，不是“模型”

`RectF` 可以继续存在，而且非常有价值。

但必须明确：

``` text
Grid Document
     │
     ▼
Layout Engine
     │
     ▼
Calculated Rect
```

而不是：

``` text
Rect → Grid
```

也就是说：

> **Grid → Rect 是合法方向；Rect → Grid 只能作为特殊的导入/分析过程，不能成为 Core 的基本设计。**

PDF、GDI+ 等 Renderer 可以消费 Layout Engine 产生的物理矩形。

Core 本身不需要知道：

``` text
PDF points
pixels
GDI device units
1/100 inch
1/1000 inch
```

------------------------------------------------------------------------

# 39. PDF 不使用 Pixel 作为 Core 尺寸

PDF 是矢量文档。

因此 PDF Renderer 最终应该使用 PDFsharp 所适合的物理/矢量单位（通常是 point 等），而不是把：

``` text
Pixel
```

当作 PrintSharp Core 的基本单位。

正确方向：

``` text
Grid Logical Unit
       ↓
Layout / PDF measurement
       ↓
PDF physical unit
       ↓
PDFsharp drawing
```

这样 PDF 输出不会被某一个 DPI 假设绑死。

特别要避免：

``` text
96 DPI
72 DPI
Windows DPI
Screen Pixel
```

进入 Core Layout Model。

------------------------------------------------------------------------

# 40. Windows Printer 的单位也不能反向污染 Core

Windows PrintDocument / GDI+ 最终可能使用：

``` text
device unit
1/100 inch
1/1000 inch
printer-specific measurement
```

具体采用哪一种，由：

``` text
PrintSharp.Windows
```

决定。

因此：

``` text
Core
 └── Logical Grid Unit

Windows Renderer
 └── Printer Measurement

PDF Renderer
 └── PDF Measurement

Excel Renderer
 └── Excel Measurement
```

这是同一原则：

> **Renderer 负责“怎么画”，Core 负责“画什么、在哪里”。**

------------------------------------------------------------------------

# 41. Font Model：字体必须与 Layout Model 解耦

跨 Windows / Linux / PDF / Excel 时，字体是一个非常容易把架构搞坏的地方。

Core 不应该直接假设：

``` text
Arial
Meiryo
Yu Gothic
MS Gothic
Noto Sans CJK JP
```

一定存在于运行机器。

因此 Core 应定义逻辑字体描述，例如：

``` csharp
public sealed record FontSpec
{
    public float Size { get; init; } = 10;
    public bool Bold { get; init; }
    public bool Italic { get; init; }

    // optional logical family
    public string? Family { get; init; }
}
```

这里：

``` text
Size
Bold
Italic
```

是字体的逻辑属性。

`Family` 可以存在，但不能让 Core 依赖某一个操作系统实际安装的字体。

------------------------------------------------------------------------

# 42. Font Resolver / Font Registry

真正的字体文件选择应该放在 Renderer / Font Infrastructure 层。

推荐概念：

``` text
                 Core FontSpec
                      │
                      ▼
                Font Resolver
                      │
        ┌─────────────┼─────────────┐
        ▼             ▼             ▼
       PDF          Windows       Excel
        │             │             │
   TTF / OTF       system /      family name
   font resource   private font
```

例如：

``` text
Logical Family:
Noto Sans CJK

PDF:
→ /fonts/NotoSansCJK-Regular.ttf

PDF Bold:
→ /fonts/NotoSansCJK-Bold.ttf

Windows:
→ installed/private font

Excel:
→ "Noto Sans CJK JP"
```

因此：

> **Core 只描述“想要什么字体”；Resolver 决定“实际使用哪个字体资源”。**

------------------------------------------------------------------------

# 43. Font Fallback 必须成为未来设计的一等概念

多语言报表不能假定：

``` text
一个字体 = 所有字符
```

实际可能出现：

``` text
Latin
Japanese
CJK
Symbol
Emoji
```

未来 Font Resolver 可以支持：

``` text
Font Family
     ↓
Script / Unicode Range
     ↓
Fallback Font
```

例如：

``` text
Latin
  ↓
Noto Sans

Japanese / CJK
  ↓
Noto Sans CJK

Symbol
  ↓
Symbol-compatible font
```

但是：

> **第一阶段不要为了 Font Fallback 提前实现复杂的字体排版系统。**

第一阶段只需要把接口边界设计正确：

``` text
FontSpec
FontResolver
ResolvedFont
```

以后再扩展 fallback。

------------------------------------------------------------------------

# 44. Font 文件与操作系统环境

未来支持 Linux 时，不能假设服务器一定安装了某个字体。

因此 PrintSharp 可以考虑两种策略：

``` text
Strategy A
依赖系统字体

Strategy B
应用私有字体资源
```

对于需要稳定 PDF 输出的场景，更适合：

``` text
Application
 └── fonts/
      ├── Regular.ttf
      ├── Bold.ttf
      ├── Italic.ttf
      └── BoldItalic.ttf
```

然后：

``` text
PDF Renderer
     ↓
FontResolver
     ↓
private font resource
```

这样相同版本的应用在：

``` text
Windows
Linux
Docker
AWS
Sakura VPS
```

上更容易得到一致的 PDF 字体结果。

但是，任何随 NuGet / Application 发布的字体，都必须确认其具体版本和许可证允许这样重新分发。

------------------------------------------------------------------------

# 45. Excel 与字体的特殊关系

Excel 和 PDF 的字体模型不完全相同。

PDF 可以嵌入：

``` text
TTF / OTF
```

而 Excel 文件通常保存的是：

``` text
Font Family Name
```

所以：

``` text
Core FontSpec
       │
       ├── PDF
       │    ↓
       │  actual font resource
       │
       └── Excel
            ↓
          concrete family name
```

这是合理的。

因此 Core 可以保存：

``` csharp
Family = "Noto Sans CJK JP"
```

但 Excel Renderer 不应该因为这个字符串就假设：

``` text
Windows 一定安装了这个字体
```

它只是写入 `.xlsx` 的字体名称。

最终 Excel 客户端负责字体实际呈现。

------------------------------------------------------------------------

# 46. 字体大小与单位

Core 中的字体大小也应该是逻辑/排版层面的值，而不是：

``` text
Pixel
```

不要出现：

``` csharp
Font.Size = 14 pixels;
```

更合适的是：

``` csharp
Font.Size = 10;
```

其具体解释由字体排版/Renderer 采用稳定的排版单位。

对于 PDF，通常可以映射到：

``` text
points
```

对于 Windows，可以转换到 GDI+ 所需要的字体尺寸单位。

对于 Excel，则映射到 Excel 的字体大小语义。

因此：

> **字体大小与 Grid Row Height / Column Width 是两个不同的单位系统。**

不能简单认为：

``` text
Row Height = Font Size
```

二者之间必须由 Layout Engine / Renderer 处理。

------------------------------------------------------------------------

# 47. 文字测量是 Layout Engine 的第二阶段能力

Grid 首先决定：

``` text
Cell = C3
```

然后 Style 决定：

``` text
Font
Alignment
Wrap
Padding
```

最后 Renderer / Layout Engine 才进行：

``` text
Text Measurement
Line Breaking
Line Height
Vertical Alignment
```

例如：

``` text
┌───────────────┐
│ 日本語の長い文字列 │
│ です。           │
└───────────────┘
```

Wrap 后可能需要两行。

因此未来需要区分：

``` text
Grid Geometry
+
Text Layout
```

但不要因此重新把 Cell 改成自由 X/Y 模型。

------------------------------------------------------------------------

# 48. 一个关键的“防漂移”规则

PrintSharp 必须避免 Renderer 自己重新计算 Grid。

禁止：

``` text
Excel Renderer
 └── 自己计算一套 X/Y

PDF Renderer
 └── 自己计算另一套 X/Y

Printer Renderer
 └── 又计算一套 X/Y
```

推荐：

``` text
             Grid Document
                   │
                   ▼
              Layout Engine
                   │
             Calculated Layout
                   │
        ┌──────────┼──────────┐
        ▼          ▼          ▼
      Excel       PDF       Printer
```

其中：

``` text
Excel
```

可以直接使用：

``` text
Row / Column / Span
```

而：

``` text
PDF / Printer
```

使用：

``` text
Calculated Rect
```

这样可以最大程度避免：

``` text
Excel 看起来对
PDF 偏 1px
Printer 又偏 2px
```

这样的长期维护问题。

------------------------------------------------------------------------

# 49. Core 的三个“世界”必须保持独立

以后 Agent 写代码时，可以用下面这个规则判断设计是否越界：

``` text
世界 1：Grid World
--------------------------------
Row
Column
RowSpan
ColumnSpan
Row Height
Column Width


世界 2：Physical Layout World
--------------------------------
X
Y
Width
Height
Text Bounds
Measured Font


世界 3：Renderer World
--------------------------------
Excel Width
PDF Point
GDI Device Unit
TTF / OTF
ClosedXML
PDFsharp
System.Drawing
```

依赖方向必须是：

``` text
Grid World
    ↓
Physical Layout World
    ↓
Renderer World
```

不能反过来。

尤其禁止：

``` text
ClosedXML → Core
PDFsharp → Core
GDI+ → Core
Windows Font → Core
Linux Font → Core
```

------------------------------------------------------------------------

# 50. 第一版 Core API 建议

第一阶段可以保持非常简单：

``` csharp
public sealed class Document
{
    public IList<Row> Rows { get; } = [];
    public IList<Column> Columns { get; } = [];
    public IList<Cell> Cells { get; } = [];
}
```

``` csharp
public sealed class Row
{
    public float Height { get; set; }
}
```

``` csharp
public sealed class Column
{
    public float Width { get; set; }
}
```

``` csharp
public sealed class Cell
{
    public int Row { get; init; }
    public int Column { get; init; }

    public int RowSpan { get; init; } = 1;
    public int ColumnSpan { get; init; } = 1;

    public CellType Type { get; init; }
    public object? Value { get; init; }

    public CellStyle? Style { get; init; }
}
```

``` csharp
public sealed record FontSpec
{
    public float Size { get; init; } = 10;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public string? Family { get; init; }
}
```

注意：

这只是第一阶段的**概念 API**。

不要在第一版就加入：

``` text
X
Y
Pixel
Dpi
PdfPoint
Graphics
XFont
XLCell
PrintDocument
```

------------------------------------------------------------------------

# 51. 统一的单位原则

PrintSharp 以后遇到任何“单位”问题，都可以用下面这张表判断：

| 对象 | Core | Renderer |
|---|---|---|
| Row Height | Logical Grid Unit | Excel / PDF / Printer |
| Column Width | Logical Grid Unit | Excel / PDF / Printer |
| Cell Position | Row / Column | X / Y（计算结果） |
| Cell Size | Span | Width / Height（计算结果） |
| Font Size | Logical typography value | target-specific |
| Text Bounds | Layout result | target-specific |
| PDF drawing | 不存在 | PDF physical unit |
| Printer drawing | 不存在 | printer/device unit |
| Excel Column Width | 不存在 | Excel-specific |
| TTF / OTF | 不存在 | Font Resolver / Renderer |

一句话：

> **Core 保存逻辑关系，Renderer 负责物理实现。**

------------------------------------------------------------------------

# 52. 更新后的 Agent 开发规则

在原有规则基础上，增加以下硬性规则：

26. Core 的 Row Height / Column Width 必须是 PrintSharp 自己的逻辑 Grid 尺寸，不得直接绑定 ClosedXML 的内部尺寸实现。

27. 不允许把 Pixel、DPI、PDF Point、GDI Device Unit、1/100 inch、1/1000 inch 等 Renderer 单位引入 Core Layout Model。

28. Cell 的 Width / Height 只能来自 RowSpan / ColumnSpan 覆盖的 Row / Column 尺寸计算，不得重新引入自由 Width / Height。

29. `RectF`、`X/Y`、Physical Bounds 属于 Layout / Renderer 结果，不是 Grid Document 的主坐标系。

30. Core Font 必须使用逻辑 `FontSpec`，不得直接依赖 Windows、Linux 或某个 Renderer 的字体对象。

31. 字体实际文件、字体安装位置、TTF/OTF、字体嵌入等问题必须通过 FontResolver / FontRegistry 等边界处理。

32. Renderer 可以将 FontSpec 解析成自己的字体对象，但不得把该对象泄漏回 Core。

33. Core 不得依赖 ClosedXML、PDFsharp、System.Drawing、PrintDocument、GDI+ 或具体操作系统字体。

34. PDF Renderer 不得把 Pixel/DPI 作为核心布局依据。

35. Windows Printer Renderer 的物理单位必须停留在 Windows Renderer 内。

36. Excel Renderer 可以把 Core Logical Grid Unit 转换为 Excel Width/Height，但该转换逻辑不能进入 Core。

37. 任何新增 Renderer 都必须遵守：

``` text
Grid → Layout → Renderer
```

而不是：

``` text
Renderer → Core Layout
```

38. 多语言字体 fallback 可以作为未来扩展，但第一阶段只实现稳定的 FontSpec / FontResolver 边界，不提前实现完整字体排版引擎。

39. 任何需要“修正 0.1 / 0.2 坐标误差”的设计，都应该首先检查是否错误地引入了自由坐标模型。

40. 如果某个设计让 Excel、PDF、Printer 各自产生一套布局计算，应优先重构为共享 Layout Engine。

------------------------------------------------------------------------

# 53. 更新后的核心架构图

最终架构进一步明确为：

``` text
                  DataSet / T / Excel
                         │
                         ▼
                Data Binding / Parser
                         │
                         ▼
              ┌──────────────────────┐
              │   Grid Document      │
              │                      │
              │ Row                  │
              │ Column               │
              │ RowSpan              │
              │ ColumnSpan           │
              │ Logical Dimensions   │
              │ Cell Value           │
              │ Cell Style           │
              │ FontSpec             │
              └──────────┬───────────┘
                         │
                         ▼
                  Layout Engine
                         │
             ┌───────────┴───────────┐
             │                       │
             ▼                       ▼
      Physical Layout           Text Layout
       X/Y/Width/Height        Measure/Wrap
             │                       │
             └───────────┬───────────┘
                         │
                         ▼
                Renderer Boundary
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
       Excel            PDF          Windows
      ClosedXML       PDFsharp       GDI+
          │              │              │
          ▼              ▼              ▼
       .xlsx           .pdf        PrintDocument
```

字体则走独立但平行的路径：

``` text
             Core FontSpec
                  │
                  ▼
             FontResolver
                  │
       ┌──────────┼──────────┐
       ▼          ▼          ▼
      PDF       Windows     Excel
    TTF/OTF    font object  family name
```

因此 PrintSharp 实际上形成三个清晰边界：

``` text
        Grid Model
            │
            ├── Dimension Mapping
            │
            └── Font Resolution
                    │
                    ▼
                Renderer
```

------------------------------------------------------------------------

# 54. 最终设计结论（修订版）

PrintSharp 的核心不是：

``` text
RectF
```

也不是：

``` text
ClosedXML
PDFsharp
GDI+
```

甚至也不只是：

``` text
Excel Cell
```

真正的核心是：

``` text
              Excel-like Grid Document
                       │
          ┌────────────┼────────────┐
          │            │            │
      Grid Layout   Dimensions     Fonts
          │            │            │
          │            │            │
          ▼            ▼            ▼
       Row/Col     Logical Unit   FontSpec
       Span        → Renderer     → Resolver
          │            │            │
          └────────────┼────────────┘
                       ▼
                  Layout Engine
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
        Excel         PDF        Printer
```

其中最重要的三条原则是：

### 第一条：布局必须 Grid-first

``` text
Row / Column / Span
```

是母坐标系。

### 第二条：单位必须 Renderer-neutral

``` text
Core Logical Unit
```

不能变成：

``` text
Pixel
PDF Point
GDI Unit
Excel Width
```

中的任何一种。

### 第三条：字体必须 Resource-neutral

``` text
FontSpec
```

描述需求。

``` text
FontResolver
```

决定实际字体资源。

最终：

> **PrintSharp 不是把一种输出格式转换成另一种输出格式，而是先建立一个稳定的、Excel 能表达的 Grid Document，再把它分别投影到 Excel、PDF 和打印机。**

这使得：

``` text
Grid
+
Dimension Mapping
+
Font Resolution
+
Layout Engine
+
Renderer
```

成为一个可以长期扩展、跨 Windows/Linux、跨 Excel/PDF/Printer、并且未来可以直接进入 JSON/微服务的统一文档基础设施。
