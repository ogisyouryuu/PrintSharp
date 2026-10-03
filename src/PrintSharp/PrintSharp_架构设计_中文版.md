# PrintSharp 架构设计

## 1. 项目定位

PrintSharp 是一个面向 .NET 的**代码式文档布局与输出中间件类库**。

核心思想是把下面几个概念彻底分离：

1.  **输出什么** ------ 文档内容与布局
2.  **数据从哪里来** ------ `DataSet`、`DataTable`、POCO / `T` 等
3.  **如何输出** ------ Excel、PDF、Windows 打印机
4.  **如何定义布局** ------ C# 代码或者 Excel 模板
5.  **如何提供服务** ------ 先作为本地类库，未来可以作为微服务

同一份逻辑文档，在条件允许的情况下，可以复用于多个输出目标。

整体流程：

``` text
DataSet / DataTable / T
          │
          ▼
       文档定义
          │
     ┌────┴─────┐
     │          │
 C#代码布局   Excel模板
     │          │
     ▼          ▼
  List<Cell>   模板引擎
     │          │
     └────┬─────┘
          │
          ▼
    Document / Cell 模型
          │
     ┌────┼─────────────┐
     │    │             │
     ▼    ▼             ▼
  Excel  PDF        PrintDocument
ClosedXML PDFsharp     GDI+
```

------------------------------------------------------------------------

# 2. 主要使用场景

## 2.1 代码式生成文档

开发人员通过 C# 创建文档布局。

概念示例：

``` csharp
var cells = new List<Cell>
{
    new Cell
    {
        Text = "销售明细",
        Type = CellType.Text,
        Rect = new RectF(10, 10, 180, 20),
        Font = new FontSpec("Yu Gothic", 14),
        Align = TextAlign.Center
    }
};
```

然后同一个 `List<Cell>` 可以交给不同的 Renderer：

``` csharp
await excelRenderer.RenderAsync(cells, output);

await pdfRenderer.RenderAsync(cells, output);

await printerRenderer.PrintAsync(cells, printerName);
```

业务程序不需要知道 ClosedXML、PDFsharp、GDI+ 的具体实现。

------------------------------------------------------------------------

# 3. DataSet / DataTable 数据驱动

PrintSharp 必须支持传统业务系统经常使用的：

``` text
DataSet
DataTable
```

例如：

``` text
DataSet
   │
   ▼
DocumentBuilder
   │
   ▼
List<Cell>
```

这样老系统的数据访问层不需要为了使用 PrintSharp 而重新设计。

------------------------------------------------------------------------

# 4. 泛型 T 数据驱动

现代 .NET 程序还应该可以直接使用强类型对象：

``` text
Order
Customer
Invoice
List<Order>
```

例如：

``` csharp
var cells = documentBuilder.Build(order);
```

或者：

``` csharp
var cells = documentBuilder.Build(orders);
```

可以在数据绑定层适当地使用 Reflection，但核心 Renderer 不应该依赖复杂的
Reflection 框架。

------------------------------------------------------------------------

# 5. Excel 模板模式

不是所有开发人员都喜欢用 C# 写坐标。

因此 PrintSharp 必须支持：

> **Excel 模板 + 数据源**

例如设计人员先制作：

``` text
invoice-template.xlsx
```

里面可以预先设计：

-   固定文字
-   字体
-   颜色
-   边框
-   合并单元格
-   行高
-   列宽
-   公式
-   数据占位符
-   重复区域

然后代码只需要：

``` csharp
templateEngine.Render(
    "invoice-template.xlsx",
    data
);
```

直接生成 Excel。

------------------------------------------------------------------------

# 6. Excel 模板 → Cell 模型

这是 PrintSharp 一个非常重要的高级能力。

Excel 模板不一定只能生成 Excel。

还可以：

``` text
Excel模板
    │
    ▼
模板解析器
    │
    ▼
List<Cell>
    │
 ┌──┼────┐
 ▼  ▼    ▼
Excel PDF 打印机
```

这样一个不会写复杂 C# 布局代码的人，只需要会用 Excel 画报表。

然后同一个模板的布局，可以进一步生成：

-   Excel
-   PDF
-   Windows 打印

但是必须明确：

**Excel 特有的功能不一定能转换成通用 Cell 模型。**

例如：

-   Excel 公式
-   Excel 图表
-   Excel 专用条件格式
-   Workbook 级功能
-   VBA / Macro

这些属于 Excel 专用能力。

因此架构上必须区分：

``` text
通用文档功能
```

和：

``` text
Excel 专用功能
```

不能为了"跨平台"而假装所有 Excel 功能都能转换。

------------------------------------------------------------------------

# 7. 核心 Document / Cell 模型

`Cell` 是 PrintSharp 最重要的基础元素。

概念上：

``` csharp
public sealed class Cell
{
    public CellType Type { get; set; }

    public string? Text { get; set; }

    public RectF Rect { get; set; }

    public SizeF? Size { get; set; }

    public Color? Color { get; set; }

    public FontSpec? Font { get; set; }

    public BorderSpec? Border { get; set; }

    public TextAlign Align { get; set; }

    public VerticalAlign VerticalAlign { get; set; }
}
```

最终属性可以继续扩展。

## 内容类型

第一阶段考虑：

``` text
Text
Image
```

以后可以：

``` text
Barcode
QRCode
Shape
Line
```

## 几何

``` text
X
Y
Width
Height
RectF
SizeF
```

## 外观

``` text
Foreground Color
Background Color
Font
Border
Border Width
Border Style
```

## 文字布局

``` text
Horizontal Alignment
Vertical Alignment
Word Wrap
Trimming
Line Spacing
Rotation
```

------------------------------------------------------------------------

# 8. 坐标系统

PrintSharp 必须定义自己的统一坐标系统。

建议：

``` text
左上角为原点

(0,0)
  ┌──────────────────────► X
  │
  │
  │
  ▼
  Y
```

也就是说：

``` text
X 向右增加
Y 向下增加
```

Excel、PDF、GDI+ 都必须把自己的坐标系统转换成 PrintSharp 的坐标系统。

核心模型不能直接使用：

``` text
Excel 行号 / 列号
```

作为基础坐标。

也不能直接使用：

``` text
PDF Point
```

或者：

``` text
GDI+ Pixel
```

作为核心坐标。

这些全部属于 Renderer 的事情。

第一版可以采用类似 PDF 的 Point
作为物理单位，但单位转换必须隔离，以便以后扩展。

------------------------------------------------------------------------

# 9. Document / Page / Cell 层级

虽然 `Cell` 是核心元素，但不要把架构永久限制成：

``` csharp
List<Cell>
```

第一版可以接受：

``` csharp
IReadOnlyList<Cell>
```

但内部结构应该为以后多页输出留下空间：

``` text
Document
 ├── Page
 │    ├── Cell
 │    ├── Cell
 │    └── ...
 ├── Page
 │    └── ...
 └── ...
```

未来可以正式形成：

``` csharp
Document
    -> Pages
        -> Elements
```

其中 `Cell` 只是 Element 的一种。

**第一版不要为了这个做成复杂 Scene Graph。**

先简单实现，同时保留扩展方向。

------------------------------------------------------------------------

# 10. Renderer 架构

每一种输出格式都是一个独立 Renderer。

概念接口：

``` csharp
public interface IDocumentRenderer
{
    Task RenderAsync(
        Document document,
        Stream output,
        CancellationToken cancellationToken = default);
}
```

可以进一步有：

``` csharp
public interface IExcelRenderer : IDocumentRenderer
{
}

public interface IPdfRenderer : IDocumentRenderer
{
}

public interface IPrinterRenderer
{
    Task PrintAsync(
        Document document,
        string printerName,
        CancellationToken cancellationToken = default);
}
```

具体 API 可以继续调整。

最重要的原则是：

> **核心 Document 模型绝对不能引用具体 Renderer。**

------------------------------------------------------------------------

# 11. Excel Renderer

第一版使用：

**ClosedXML**

负责：

-   创建 Workbook / Worksheet
-   将 PrintSharp 坐标转换为 Excel 行列
-   写入文字
-   写入图片
-   设置字体
-   设置颜色
-   设置边框
-   设置对齐
-   合并区域
-   设置行高
-   设置列宽
-   保存 Excel

ClosedXML 的类型只能存在于 Excel Renderer 项目。

结构：

``` text
PrintSharp.Core
       │
       ▼
PrintSharp.Excel
       │
       ▼
ClosedXML
```

核心 `PrintSharp` 项目不能依赖 ClosedXML。

------------------------------------------------------------------------

# 12. PDF Renderer

第一版使用：

**PDFsharp**

负责：

-   创建 PDF
-   创建页面
-   绘制文字
-   绘制图片
-   绘制边框
-   绘制图形
-   坐标和单位转换
-   字体处理
-   页面边界处理

PDFsharp 的类型只能存在于 PDF Renderer 项目。

结构：

``` text
PrintSharp.Core
       │
       ▼
PrintSharp.Pdf
       │
       ▼
PDFsharp
```

------------------------------------------------------------------------

# 13. Windows 打印 Renderer

第一版使用：

**System.Drawing / GDI+ / PrintDocument**

这是 Windows 专用 Renderer。

负责：

-   指定打印机
-   创建 `PrintDocument`
-   处理 `PrintPage`
-   将 PrintSharp 坐标转换成 GDI+ 坐标
-   绘制文字
-   绘制图片
-   绘制边框
-   多页打印
-   处理打印边距
-   处理可打印区域

结构：

``` text
PrintSharp.Core
       │
       ▼
PrintSharp.Windows
       │
       ▼
System.Drawing
PrintDocument
GDI+
```

必须明确这个 Renderer 是 Windows-only。

------------------------------------------------------------------------

# 14. 推荐项目结构

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
│   │   ├── Elements/
│   │   ├── Geometry/
│   │   ├── Styles/
│   │   ├── Data/
│   │   └── PrintSharp.csproj
│   │
│   ├── PrintSharp.Excel/
│   │   ├── ClosedXmlRenderer.cs
│   │   ├── ExcelTemplateRenderer.cs
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

这样核心库就不会被 Excel、PDF、Windows 打印绑死。

------------------------------------------------------------------------

# 15. NuGet 包

最终建议拆成：

``` text
PrintSharp
PrintSharp.Excel
PrintSharp.Pdf
PrintSharp.Windows
```

例如只想使用核心：

``` xml
<PackageReference Include="PrintSharp" Version="x.y.z" />
```

需要 Excel：

``` xml
<PackageReference Include="PrintSharp.Excel" Version="x.y.z" />
```

需要 PDF：

``` xml
<PackageReference Include="PrintSharp.Pdf" Version="x.y.z" />
```

Windows 打印：

``` xml
<PackageReference Include="PrintSharp.Windows" Version="x.y.z" />
```

依赖方向：

``` text
                 PrintSharp
                    ▲
                    │
          ┌─────────┼─────────┐
          │         │         │
          │         │         │
   PrintSharp   PrintSharp  PrintSharp
      .Excel       .Pdf      .Windows
          │         │         │
       ClosedXML PDFsharp   GDI+
```

绝对不要让：

``` text
PrintSharp
```

反过来依赖这些 Renderer。

------------------------------------------------------------------------

# 16. Excel Template 模板引擎

模板功能应该和核心 Renderer 分离。

概念：

``` text
IPrintTemplate
       │
       ▼
Template Engine
       │
       ├── Excel Template
       │
       ▼
Document / Cell Model
```

Excel 模板有两种模式。

## 模式 A：Excel 原生输出

``` text
Excel Template
     +
Data
     │
     ▼
ClosedXML
     │
     ▼
Output Excel
```

尽量保留 Excel 自身的功能。

## 模式 B：转换成通用 Document

``` text
Excel Template
     +
Data
     │
     ▼
Template Parser
     │
     ▼
Document / List<Cell>
     │
 ┌───┼────┐
 ▼   ▼    ▼
Excel PDF Printer
```

模式 B 只能使用通用 Document 模型能够表达的功能。

------------------------------------------------------------------------

# 17. 数据绑定

数据绑定必须支持：

``` text
DataSet
DataTable
object
T
IEnumerable<T>
```

未来可以设计成：

``` csharp
document.Bind(data);
```

或者：

``` csharp
template.Render(data);
```

数据绑定需要考虑：

-   单值字段
-   嵌套属性
-   集合
-   重复行
-   重复区域
-   条件区域
-   格式化
-   null 处理

第一版不要过度设计 Reflection 框架。

优先简单、明确、可调试。

------------------------------------------------------------------------

# 18. 模板占位符

第一版可以考虑：

``` text
{{Customer.Name}}
{{Invoice.Number}}
{{Invoice.Total}}
```

集合：

``` text
{{#Items}}
{{Code}} | {{Name}} | {{Quantity}} | {{Amount}}
{{/Items}}
```

具体语法以后再决定。

不要把模板语法和 ClosedXML 内部实现绑死。

逻辑应该是：

``` text
Template
   │
   ▼
Parse
   │
   ▼
Template AST / Binding Model
   │
   ▼
Data Binding
   │
   ▼
Document Model / Excel Workbook
```

------------------------------------------------------------------------

# 19. 未来 JSON 文档模型

未来 PrintSharp 可以作为微服务。

客户端可以发送：

``` text
Excel Template + JSON Data
```

或者直接发送：

``` text
List<Cell> JSON
```

例如：

``` json
{
  "format": "pdf",
  "document": {
    "pages": [
      {
        "cells": [
          {
            "type": "text",
            "text": "Invoice",
            "rect": {
              "x": 10,
              "y": 10,
              "width": 180,
              "height": 20
            }
          }
        ]
      }
    ]
  }
}
```

服务器返回：

``` text
application/pdf
```

或者：

``` text
application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
```

打印则可以由服务器端 Windows 打印机执行。

------------------------------------------------------------------------

# 20. 未来微服务架构

``` text
Client
  │
  │ HTTP / JSON
  ▼
PrintSharp Service
  │
  ├── Template Service
  │
  ├── Data Binding
  │
  ├── Document Model
  │
  └── Renderers
       ├── Excel
       ├── PDF
       └── Windows Printer
```

未来可以有：

``` text
POST /api/render/excel
POST /api/render/pdf
POST /api/print
POST /api/template/excel
```

第一阶段不需要实现这些 API。

服务器应该直接复用同一个 PrintSharp Core，而不是再写一套文档生成引擎。

------------------------------------------------------------------------

# 21. 本地类库和微服务必须共用 Core

最终目标：

``` text
                  PrintSharp Core
                       │
            ┌──────────┴──────────┐
            │                     │
       本地类库              PrintSharp Service
            │                     │
       ┌────┼────┐           HTTP / JSON
       ▼    ▼    ▼
    Excel PDF Printer
```

这样：

``` text
应用 A
  → 直接引用 PrintSharp

应用 B
  → HTTP 调用 PrintSharp Service

应用 C
  → Excel 模板 + HTTP 调用 PrintSharp Service
```

三者实际上使用的是同一个文档模型和同一套 Renderer。

------------------------------------------------------------------------

# 22. 核心设计原则

## 22.1 Core 与 Renderer 完全分离

Core 里面绝对不要出现：

``` csharp
XLWorkbook
PdfDocument
Graphics
PrintDocument
```

------------------------------------------------------------------------

## 22.2 输出格式不是 Document Model

不要把整个系统设计成：

``` text
Excel Cell
```

因为 PDF 和打印机本质上并不是 Excel。

`Cell` 应该是：

> 一个带位置和样式的逻辑文档元素。

然后：

``` text
Excel Renderer
    Cell → Excel Row / Column

PDF Renderer
    Cell → PDF drawing command

Printer Renderer
    Cell → GDI+ Graphics
```

------------------------------------------------------------------------

## 22.3 第一版不要过度设计

第一版只需要：

``` text
Text
Image
Rectangle
Border
Font
Color
Alignment
RectF
Page
```

以及：

``` text
Excel
PDF
Windows PrintDocument
```

不要第一版就实现所有报表功能。

------------------------------------------------------------------------

## 22.4 保留扩展能力

未来可以加入：

``` text
Barcode
QRCode
Line
Shape
Table
Chart
Watermark
Signature
```

但不要因为未来需求而提前制造复杂架构。

------------------------------------------------------------------------

# 23. Renderer 能力差异

三个 Renderer 不可能永远 100% 支持相同功能。

例如：

  功能                Excel      PDF   Windows Print
  ---------------- -------- -------- ---------------
  Text                    ✓        ✓               ✓
  Image                   ✓        ✓               ✓
  Font                    ✓        ✓               ✓
  Color                   ✓        ✓               ✓
  Border                  ✓        ✓               ✓
  Alignment               ✓        ✓               ✓
  Rotation                △        ✓               ✓
  Barcode            Future   Future          Future
  Excel Formula           ✓        ×               ×
  Excel Chart             ✓        ×               ×
  Excel 专用格式          ✓        ×               ×

Renderer 不能对自己不支持的功能假装支持。

------------------------------------------------------------------------

# 24. 错误处理

需要明确处理：

-   无效的文档坐标
-   不支持的元素类型
-   模板字段不存在
-   模板语法错误
-   数据属性不存在
-   Renderer 不支持某功能
-   打印机不存在
-   打印机不可用
-   图片数据无效

模板错误应该尽量指出：

``` text
模板文件
Worksheet
Cell Address
Placeholder
Data Path
```

方便开发人员定位。

------------------------------------------------------------------------

# 25. 线程安全

核心 Document 模型应该尽量做到：

> 创建完成后不再随意修改。

Renderer 不应该依赖全局可变状态。

如果底层库允许，Renderer 实例应该可以重复使用。

打印机由于物理资源具有状态，打印操作可能需要额外的串行化控制。

------------------------------------------------------------------------

# 26. 第一阶段开发顺序

不要一次让 Agent 把整个架构全部实现。

## Milestone 1：核心模型

``` text
PrintSharp
 ├── Cell
 ├── CellType
 ├── RectF
 ├── FontSpec
 ├── BorderSpec
 ├── Alignment
 └── Document / Page
```

## Milestone 2：Excel

``` text
PrintSharp.Excel
 └── ClosedXML Renderer
```

## Milestone 3：PDF

``` text
PrintSharp.Pdf
 └── PDFsharp Renderer
```

## Milestone 4：Windows 打印

``` text
PrintSharp.Windows
 └── PrintDocument Renderer
```

## Milestone 5：Excel Template

``` text
Excel Template
 └── 简单 Placeholder
```

## Milestone 6：数据绑定

``` text
Excel Template
 ├── DataSet / DataTable
 ├── T
 └── 重复行
```

## Milestone 7：Template → Cell

``` text
Excel Template
       │
       ▼
List<Cell>
```

然后就可以：

``` text
Excel
PDF
Printer
```

三路复用。

## Milestone 8：微服务

``` text
PrintSharp HTTP Service
```

------------------------------------------------------------------------

# 27. 给 Codex / Claude Code Agent 的开发规则

Agent 在修改 PrintSharp 之前必须先阅读本文档。

必须遵守：

1.  Core `PrintSharp` 不得引用任何具体 Renderer 库。
2.  ClosedXML 代码只能放在 `PrintSharp.Excel`。
3.  PDFsharp 代码只能放在 `PrintSharp.Pdf`。
4.  `PrintDocument` / GDI+ 代码只能放在 `PrintSharp.Windows`。
5.  Core 使用小型、职责单一的类型。
6.  不要创建一个什么都干的巨大 `PrintHelper`。
7.  不引入 ORM。
8.  不引入没有必要的 DI Framework。
9.  Core Document 模型应该考虑未来 JSON 序列化。
10. 不要把 Core 设计成只能表达 Excel。
11. 不要让 PDF 或 Printer 依赖 Excel。
12. 所有 Renderer 特有的功能必须留在 Renderer 层。
13. 先测试 Core Document 模型，再开发 Renderer。
14. Public API 必须有 XML Documentation。
15. `0.x` 阶段允许为了改善架构而进行 Breaking Change。
16. Core API 稳定之前，不要实现微服务 API。
17. 不要为了"未来可能用到"而提前实现大量功能。

------------------------------------------------------------------------

# 28. 最终架构

``` text
                         ┌───────────────────────┐
                         │        数据源          │
                         │ DataSet / DataTable   │
                         │ T / IEnumerable<T>    │
                         └───────────┬───────────┘
                                     │
                                     ▼
                         ┌───────────────────────┐
                         │      数据绑定          │
                         └───────────┬───────────┘
                                     │
                    ┌────────────────┴────────────────┐
                    │                                 │
                    ▼                                 ▼
              C# Document Builder                Excel Template
                    │                                 │
                    │                                 ▼
                    │                          Template Engine
                    │                                 │
                    └──────────────┬──────────────────┘
                                   ▼
                         ┌───────────────────────┐
                         │ PrintSharp Core       │
                         │ Document / Page /     │
                         │ Cell / Image / Style  │
                         └───────────┬───────────┘
                                     │
                  ┌──────────────────┼──────────────────┐
                  │                  │                  │
                  ▼                  ▼                  ▼
          PrintSharp.Excel   PrintSharp.Pdf   PrintSharp.Windows
                  │                  │                  │
             ClosedXML            PDFsharp         GDI+/PrintDocument
                  │                  │                  │
                  ▼                  ▼                  ▼
               .xlsx                .pdf             打印机
```

未来：

``` text
                        HTTP / JSON
                            │
                            ▼
                 ┌─────────────────────┐
                 │ PrintSharp Service  │
                 └──────────┬──────────┘
                            │
                            ▼
                     PrintSharp Core
                            │
                 ┌──────────┼──────────┐
                 ▼          ▼          ▼
               Excel       PDF       Printer
```

------------------------------------------------------------------------

# 29. 一句话定义

> **PrintSharp 不是 Excel 库、PDF 库，也不是打印机库。**

它是：

> **一个统一的二维文档/布局模型，加上多个输出 Adapter。**

也就是：

``` text
              「我要画什么」
                    │
                    ▼
               PrintSharp
              Document Model
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
      Excel        PDF        Printer
```

Excel、PDF、打印机，只是这个模型的不同 Renderer。

这也是 PrintSharp 最核心的架构价值。
