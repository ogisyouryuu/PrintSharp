# PrintSharp Architecture Specification

## 1. Project Positioning

PrintSharp is a **code-first document layout and output middleware
library for .NET**.

The core idea is to separate:

1.  **What to output** --- document content and layout
2.  **How to obtain data** --- `DataSet`, `DataTable`, POCO / `T`, etc.
3.  **How to render/output** --- Excel, PDF, Windows printer
4.  **How to define the layout** --- C# code or Excel template
5.  **How to expose the capability** --- local library first,
    microservice later

The same logical document should be reusable across multiple output
targets whenever possible.

The conceptual pipeline is:

``` text
DataSet / DataTable / T
          │
          ▼
   Document Definition
          │
     ┌────┴─────┐
     │          │
 C# layout   Excel Template
     │          │
     ▼          ▼
  List<Cell>   Template Engine
     │          │
     └────┬─────┘
          │
          ▼
   Document / Cell Model
          │
     ┌────┼─────────────┐
     │    │             │
     ▼    ▼             ▼
  Excel  PDF        PrintDocument
ClosedXML PDFsharp     GDI+
```

The architecture must avoid making the core model dependent on
ClosedXML, PDFsharp, or GDI+.

------------------------------------------------------------------------

# 2. Main Use Cases

## 2.1 Code-first document generation

A developer creates a document from data using C#.

Example conceptual usage:

``` csharp
var cells = new List<Cell>
{
    new Cell
    {
        Text = "売上明細",
        Type = CellType.Text,
        Rect = new RectF(10, 10, 180, 20),
        Font = new FontSpec("Yu Gothic", 14),
        Align = TextAlign.Center
    }
};
```

The application can then send the same `List<Cell>` to different
renderers:

``` csharp
await excelRenderer.RenderAsync(cells, output);

await pdfRenderer.RenderAsync(cells, output);

await printerRenderer.PrintAsync(cells, printerName);
```

The goal is that the application does not need to know the
implementation details of each output engine.

------------------------------------------------------------------------

## 2.2 DataSet / DataTable driven generation

The library must support traditional business-system data sources:

``` csharp
DataSet
DataTable
```

A developer can transform the data into a document model:

``` text
DataSet
   │
   ▼
DocumentBuilder
   │
   ▼
List<Cell>
```

This is important for legacy / enterprise applications where `DataSet`
remains a practical integration format.

------------------------------------------------------------------------

## 2.3 Generic `T` driven generation

Modern applications should also be able to generate documents directly
from typed objects:

``` csharp
Order
Customer
List<Order>
Invoice
```

Conceptually:

``` csharp
var cells = documentBuilder.Build(order);
```

or:

``` csharp
var cells = documentBuilder.Build(orders);
```

Reflection may be used at the application/model-binding boundary where
appropriate, but the core rendering model must remain simple and
dependency-light.

------------------------------------------------------------------------

# 3. Excel Template Mode

Not every developer should have to construct a document layout in C#.

Therefore PrintSharp must support an **Excel-template-first workflow**.

A designer creates an Excel template:

``` text
invoice-template.xlsx
```

The template contains:

-   static text
-   formatting
-   merged cells
-   column widths
-   row heights
-   formulas where appropriate
-   placeholder fields
-   repeating regions where supported

Then application code supplies the data:

``` csharp
templateEngine.Render(
    "invoice-template.xlsx",
    data
);
```

The output can be:

``` text
Excel
```

directly.

------------------------------------------------------------------------

# 4. Excel Template -\> Cell Model

The template workflow should not necessarily be limited to Excel output.

A second important scenario is:

``` text
Excel Template
      │
      ▼
Template Parser
      │
      ▼
List<Cell>
      │
 ┌────┼─────┐
 ▼    ▼     ▼
Excel PDF Printer
```

This allows a user who is comfortable designing reports in Excel but
does not want to write layout code to create a template and then use the
same logical layout for:

-   Excel
-   PDF
-   Windows printing

This is a key architectural feature of PrintSharp.

However, Excel-specific features that cannot be represented by the
common `Cell` model must remain Excel-specific rather than being
silently approximated.

Examples:

-   Excel formulas
-   Excel charts
-   Excel-specific conditional formatting
-   workbook-level features
-   macros

The architecture must distinguish **portable document features** from
**Excel-native features**.

------------------------------------------------------------------------

# 5. Core Document Model

The central abstraction is the document layout model.

The most important primitive is `Cell`.

Conceptually:

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

    // Additional properties as architecture evolves
}
```

The exact API is intentionally not fixed at this stage.

The architecture should support at least:

### Content

-   Text
-   Image
-   Barcode / QR code (future)
-   Shape (future)
-   Line (future)

### Geometry

-   X
-   Y
-   Width
-   Height
-   `RectF`
-   `SizeF`

### Appearance

-   Foreground color
-   Background color
-   Font
-   Border
-   Border width
-   Border style
-   Opacity (future)

### Text layout

-   Horizontal alignment
-   Vertical alignment
-   Wrapping
-   Trimming
-   Line spacing
-   Rotation (future)

------------------------------------------------------------------------

# 6. Coordinate System

The common document model must have a defined coordinate system.

The preferred initial model is:

``` text
Top-left origin

(0,0)
  ┌──────────────────────────► X
  │
  │
  │
  ▼
  Y
```

All renderers must convert from the common coordinate system to their
native coordinate system.

The core model must NOT expose:

-   Excel row/column indexes as its fundamental coordinate system
-   PDF point coordinates as its fundamental coordinate system
-   GDI+ printer pixels as its fundamental coordinate system

Those are renderer concerns.

A common physical unit should be selected for the first implementation.
`Point` / PDF-style points are a reasonable initial choice, but the
implementation must isolate unit conversion so another unit can be
introduced later.

------------------------------------------------------------------------

# 7. Document Model Hierarchy

Although `Cell` is the core primitive, the architecture should not
assume that the entire system will always be a flat `List<Cell>`.

The initial API may accept:

``` csharp
IReadOnlyList<Cell>
```

but the internal model should leave room for:

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

This is especially important for PDF and printing.

A future model may therefore become:

``` csharp
Document
    -> Pages
        -> Elements
```

where `Cell` is one kind of element.

Do not prematurely implement a complex scene graph. Keep the first
version small while preserving this direction.

------------------------------------------------------------------------

# 8. Renderer Architecture

Each output target is an independent renderer.

Suggested abstractions:

``` csharp
public interface IDocumentRenderer
{
    Task RenderAsync(
        Document document,
        Stream output,
        CancellationToken cancellationToken = default);
}
```

Possible specialized interfaces:

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

The exact API may change during implementation.

The important architectural rule is:

> The document model must not reference renderer implementations.

------------------------------------------------------------------------

# 9. Excel Renderer

The first Excel renderer will use:

**ClosedXML**

Responsibilities:

-   Create workbook / worksheet
-   Convert document coordinates to Excel rows/columns
-   Write text
-   Write images
-   Apply fonts
-   Apply colors
-   Apply borders
-   Apply alignment
-   Apply merged areas where appropriate
-   Configure row heights / column widths
-   Save workbook

ClosedXML-specific types must remain inside the Excel renderer layer.

The core project must not require ClosedXML.

Conceptually:

``` text
PrintSharp.Core
       │
       ▼
PrintSharp.Excel
       │
       ▼
ClosedXML
```

------------------------------------------------------------------------

# 10. PDF Renderer

The first PDF renderer will use:

**PDFsharp**

Responsibilities:

-   Create PDF document
-   Create pages
-   Draw text
-   Draw images
-   Draw borders / shapes
-   Convert common coordinates and units
-   Handle fonts
-   Handle page boundaries

PDFsharp-specific types must remain inside the PDF renderer layer.

Conceptually:

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

# 11. Windows Printer Renderer

The printer renderer will use:

**System.Drawing / GDI+ / PrintDocument**

This renderer is Windows-specific.

Responsibilities:

-   Select printer
-   Create `PrintDocument`
-   Handle `PrintPage`
-   Convert document coordinates to printer graphics coordinates
-   Draw text
-   Draw images
-   Draw borders / shapes
-   Handle multiple pages
-   Respect printer margins / printable area

Conceptually:

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

The Windows printer renderer must be isolated from the portable core.

The library must explicitly document that this renderer is Windows-only.

------------------------------------------------------------------------

# 12. Recommended Project Structure

The initial repository should be structured so that the core library is
not coupled to output libraries.

Recommended:

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

The core package should have no dependency on ClosedXML, PDFsharp, or
Windows-only APIs.

------------------------------------------------------------------------

# 13. NuGet Packaging

The project should eventually publish separate packages.

Initial package candidates:

``` text
PrintSharp
PrintSharp.Excel
PrintSharp.Pdf
PrintSharp.Windows
```

Applications that only need the common document model should not have to
install Excel/PDF/Windows dependencies.

Example:

``` xml
<PackageReference Include="PrintSharp" Version="x.y.z" />
<PackageReference Include="PrintSharp.Excel" Version="x.y.z" />
```

or:

``` xml
<PackageReference Include="PrintSharp.Pdf" Version="x.y.z" />
```

The package dependency graph should remain one-directional:

``` text
PrintSharp
   ▲
   │
   ├── PrintSharp.Excel
   ├── PrintSharp.Pdf
   └── PrintSharp.Windows
```

Never:

``` text
PrintSharp -> ClosedXML -> PDFsharp -> GDI+
```

------------------------------------------------------------------------

# 14. Template Engine Architecture

Template support should be isolated from the core renderer.

Conceptually:

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

An Excel template renderer has two possible modes.

## Mode A: Excel-native output

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

This preserves Excel-specific features as much as possible.

## Mode B: Portable document output

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

Mode B should only support features that can be represented by the
common document model.

------------------------------------------------------------------------

# 15. Data Binding

Data binding must support:

``` text
DataSet
DataTable
object
T
IEnumerable<T>
```

A generic abstraction may eventually look like:

``` csharp
public interface IDataSource
{
    object? Data { get; }
}
```

But avoid building an overly generic reflection framework in the first
version.

The first implementation should favor explicit and predictable mapping.

Potential future API:

``` csharp
document.Bind(data);
```

or:

``` csharp
template.Render(data);
```

The binding system should support:

-   scalar fields
-   nested properties
-   collections
-   repeating rows
-   repeating sections
-   conditional sections
-   formatting
-   null handling

------------------------------------------------------------------------

# 16. Template Placeholder Concept

An initial template syntax may use a simple placeholder format such as:

``` text
{{Customer.Name}}
{{Invoice.Number}}
{{Invoice.Total}}
```

For repeating data:

``` text
{{#Items}}
{{Code}} | {{Name}} | {{Quantity}} | {{Amount}}
{{/Items}}
```

The exact syntax should be treated as an implementation decision.

Do not tightly couple the template syntax to ClosedXML internals.

The template engine should conceptually perform:

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
Document Model or Excel Workbook
```

------------------------------------------------------------------------

# 17. JSON Document Model for Future Microservice

One of the long-term goals is to expose PrintSharp as a
document-generation microservice.

Clients could send:

``` text
Excel Template + JSON Data
```

or:

``` text
List<Cell> JSON
```

to a server.

Example conceptual request:

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

The service would return:

``` text
application/pdf
```

or:

``` text
application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
```

For printing, the service may target a configured server-side Windows
printer.

------------------------------------------------------------------------

# 18. Microservice Architecture

Future architecture:

``` text
Client
  │
  │ HTTP/JSON
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

Potential endpoints:

``` text
POST /api/render/excel
POST /api/render/pdf
POST /api/print
POST /api/template/excel
```

The exact API is not part of the first library implementation.

The server must reuse the same core `PrintSharp` document model rather
than implementing a second rendering engine.

------------------------------------------------------------------------

# 19. Local Library and Microservice Must Share the Same Core

This is a major architectural requirement.

The desired relationship is:

``` text
                  PrintSharp Core
                       │
            ┌──────────┴──────────┐
            │                     │
       Local Library         PrintSharp Service
            │                     │
       ┌────┼────┐          HTTP/JSON API
       ▼    ▼    ▼
    Excel PDF Printer
```

The microservice is only a transport/API layer.

It must not contain a completely separate document engine.

This allows:

``` text
Application A
   -> PrintSharp library

Application B
   -> HTTP PrintSharp Service

Application C
   -> Excel template + HTTP PrintSharp Service
```

while all three use the same document model and rendering concepts.

------------------------------------------------------------------------

# 20. Design Principles

## 20.1 Core must be renderer-independent

Never put:

``` csharp
XLWorkbook
PdfDocument
Graphics
PrintDocument
```

inside the core `PrintSharp` project.

------------------------------------------------------------------------

## 20.2 Output format is not the document model

Do not model the entire document as:

``` text
Excel cells
```

because PDF and printing do not fundamentally work that way.

`Cell` is a logical positioned element.

Excel rows/columns are only one renderer's representation.

------------------------------------------------------------------------

## 20.3 Do not over-engineer the first release

The first release should support:

``` text
Text
Image
Rectangle
Border
Font
Color
Alignment
RectF
Pages
```

and:

``` text
Excel
PDF
Windows PrintDocument
```

Do not initially implement every possible document feature.

------------------------------------------------------------------------

## 20.4 Preserve extensibility

Future elements may include:

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

The model should allow these without breaking existing APIs.

------------------------------------------------------------------------

# 21. Rendering Capability Matrix

Not every renderer can support every feature equally.

The project should document capability differences.

Example:

  Feature                        Excel      PDF   Windows Print
  --------------------------- -------- -------- ---------------
  Text                             Yes      Yes             Yes
  Image                            Yes      Yes             Yes
  Font                             Yes      Yes             Yes
  Color                            Yes      Yes             Yes
  Border                           Yes      Yes             Yes
  Alignment                        Yes      Yes             Yes
  Rotation                       Maybe      Yes             Yes
  Barcode                       Future   Future          Future
  Excel Formula                    Yes       No              No
  Excel Chart                      Yes       No              No
  Excel-specific formatting        Yes       No              No

The renderer must not silently claim support for features it cannot
faithfully render.

------------------------------------------------------------------------

# 22. Error Handling

The library should use explicit exceptions / result types for:

-   invalid document geometry
-   unsupported element type
-   missing template field
-   invalid template syntax
-   missing data property
-   unsupported renderer capability
-   invalid printer
-   printer unavailable
-   invalid image data

Template errors should contain enough information to identify:

-   template file
-   worksheet
-   cell address
-   placeholder
-   data path

where applicable.

------------------------------------------------------------------------

# 23. Thread Safety

The core document model should preferably be independent and immutable
after construction where practical.

Renderers should not rely on global mutable state.

A renderer instance should be reusable if its underlying library is
thread-safe.

Printer rendering may require stricter serialization because physical
printer resources are inherently stateful.

------------------------------------------------------------------------

# 24. First Implementation Milestone

Do NOT implement the entire architecture at once.

Milestone 1:

``` text
PrintSharp
 ├── Cell
 ├── CellType
 ├── RectF
 ├── FontSpec
 ├── BorderSpec
 ├── alignment
 └── Document/Page
```

Milestone 2:

``` text
PrintSharp.Excel
 └── ClosedXML renderer
```

Milestone 3:

``` text
PrintSharp.Pdf
 └── PDFsharp renderer
```

Milestone 4:

``` text
PrintSharp.Windows
 └── PrintDocument renderer
```

Milestone 5:

``` text
Excel Template
 └── simple placeholder binding
```

Milestone 6:

``` text
Excel Template
 └── DataSet / T binding
 └── repeating rows
```

Milestone 7:

``` text
Template -> Cell/Document Model
```

Milestone 8:

``` text
PrintSharp HTTP service
```

------------------------------------------------------------------------

# 25. Agent Implementation Rules

When implementing PrintSharp, an AI coding agent must follow these
rules:

1.  Read this architecture document before modifying the project.
2.  Do not introduce external rendering dependencies into the core
    `PrintSharp` project.
3.  Keep ClosedXML code inside `PrintSharp.Excel`.
4.  Keep PDFsharp code inside `PrintSharp.Pdf`.
5.  Keep `PrintDocument` / GDI+ code inside `PrintSharp.Windows`.
6.  Prefer small, composable types.
7.  Do not create large "Helper" classes containing unrelated
    functionality.
8.  Do not introduce ORM dependencies.
9.  Do not introduce unnecessary dependency injection frameworks.
10. Keep the core document model serializable in the future.
11. Avoid APIs that cannot reasonably be represented as JSON.
12. Do not design the core around Excel's row/column model.
13. Do not make PDF or printer output depend on Excel.
14. Keep renderer-specific behavior in renderer projects.
15. Add tests for core document-model behavior before renderer-specific
    features.
16. Keep public APIs documented with XML documentation.
17. Prefer breaking changes during the `0.x` phase over preserving a
    poor abstraction.
18. Do not implement future microservice APIs until the local library
    model is stable.

------------------------------------------------------------------------

# 26. Target Architecture

The long-term PrintSharp architecture is:

``` text
                         ┌───────────────────────┐
                         │       Data Source     │
                         │ DataSet / DataTable   │
                         │ T / IEnumerable<T>    │
                         └───────────┬───────────┘
                                     │
                                     ▼
                         ┌───────────────────────┐
                         │    Data Binding       │
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
               .xlsx                .pdf             Printer
```

Future:

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

The central architectural principle is:

> **PrintSharp is not an Excel library, PDF library, or printer library.
> It is a common document/layout model plus multiple output adapters.**

Excel, PDF, and printing are renderers of that model.

That separation is what makes the same document definition usable
locally, from Excel templates, and eventually through a microservice.
