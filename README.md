<div align="center">

# PrintSharp

**Grid-first document generation for .NET**

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PrintSharp](https://img.shields.io/nuget/v/PrintSharp.svg?label=PrintSharp)](https://www.nuget.org/packages/PrintSharp/)
[![PrintSharp.Excel](https://img.shields.io/nuget/v/PrintSharp.Excel.svg?label=PrintSharp.Excel)](https://www.nuget.org/packages/PrintSharp.Excel/)
[![PrintSharp.Pdf](https://img.shields.io/nuget/v/PrintSharp.Pdf.svg?label=PrintSharp.Pdf)](https://www.nuget.org/packages/PrintSharp.Pdf/)
[![PrintSharp.Windows](https://img.shields.io/nuget/v/PrintSharp.Windows.svg?label=PrintSharp.Windows)](https://www.nuget.org/packages/PrintSharp.Windows/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ogisyouryuu/PrintSharp/blob/main/LICENSE)

[English](https://github.com/ogisyouryuu/PrintSharp/blob/main/README.md) · [简体中文](https://github.com/ogisyouryuu/PrintSharp/blob/main/README.zh-CN.md) · [日本語](https://github.com/ogisyouryuu/PrintSharp/blob/main/README.ja.md)

</div>

Create one Excel-like grid document, then render it consistently to **Excel**, **PDF**, or a **Windows printer**. PrintSharp keeps layout in rows, columns, and cell spans—not free-form `X`/`Y` coordinates—so the Excel grid remains the source of truth.

> **Status:** early-stage library (`0.1.0`). The public API and renderer behavior may evolve before a stable release.

## Why PrintSharp?

Traditional reporting systems often start with absolute coordinates and try to convert them to Excel later. Small coordinate differences can create extra Excel columns and break the intended layout.

PrintSharp starts with an Excel-compatible grid instead:

```text
Grid document (rows + columns + spans + styles)
                    │
                    ▼
             Layout engine
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
      Excel        PDF       Windows
    ClosedXML    PDFsharp  PrintDocument
```

The core model stays renderer-neutral. Renderers map logical dimensions to their own units only at the output boundary.

## Features

- Grid-first `Document` / `Page` / `Row` / `Column` / `Cell` model
- Excel A1 references and merged ranges, such as `A1:C1`
- Fluent builders for documents, rows, tables, cells, and styles
- Shared layout calculation for physical cell bounds
- Excel export and import through ClosedXML
- Excel-template placeholder binding, including repeated rows
- PDF export through PDFsharp
- Windows `PrintDocument` rendering and printing
- Cell values for text, numbers, dates, formulas, and image byte arrays
- Fonts, colors, borders, padding, alignment, wrapping, and number formats

## Installation

Install the packages for the output targets your application needs:

```bash
dotnet add package PrintSharp
dotnet add package PrintSharp.Excel
dotnet add package PrintSharp.Pdf
dotnet add package PrintSharp.Windows
```

`PrintSharp.Windows` targets Windows and uses Windows Forms / `PrintDocument`. The core, Excel, and PDF projects target .NET 10.

## Quick start

Create a single document with the fluent API, then write it to the desired output:

```csharp
using PrintSharp.Documents;
using PrintSharp.Excel;
using PrintSharp.Fluent;
using PrintSharp.Pdf;

var document = Document.Create(d =>
{
    d.Title("Sales report")
     .Author("Contoso")
     .Page("Summary", p =>
     {
         p.Columns(180f, 70f, 110f)
          .Rows(32f, 24f, 24f, 24f)
          .Cell("A1:C1", c => c.Value("Sales report")
                              .Font("Arial", 16f, bold: true)
                              .AlignCenter()
                              .Background("#F3F4F6"))
          .Cell("A2", c => c.Value("Product").Bold())
          .Cell("B2", c => c.Value("Quantity").Bold().AlignRight())
          .Cell("C2", c => c.Value("Amount").Bold().AlignRight())
          .Cell("A3", "Notebook")
          .Cell("B3", c => c.Value(2).AlignRight())
          .Cell("C3", c => c.Value(19.98m).Format("$#,##0.00").AlignRight())
          .Cell("A4", "Total")
          .Cell("C4", c => c.Formula("=SUM(C3:C3)")
                              .Format("$#,##0.00")
                              .AlignRight());
     });
});

document.SaveAsExcel("sales-report.xlsx");
document.SaveAsPdf("sales-report.pdf");
```

For Windows printing, reference `PrintSharp.Windows` and call:

```csharp
using PrintSharp.Windows;

document.Print();
```

## Excel as a template designer

An existing workbook can be parsed into the same grid model. Use `{{Property}}` placeholders for values and `{{Collection.Property}}` in a template row to repeat that row for each collection item.

```csharp
using PrintSharp.Excel;
using PrintSharp.Pdf;

var invoice = new
{
    Customer = new { Name = "Ada Lovelace" },
    InvoiceNo = "INV-001",
    Items = new[]
    {
        new { Name = "Notebook", Quantity = 2, Price = 9.99m },
        new { Name = "Pen", Quantity = 3, Price = 1.50m }
    }
};

// In invoice-template.xlsx, use {{Customer.Name}}, {{InvoiceNo}},
// and a row containing {{Items.Name}}, {{Items.Quantity}}, {{Items.Price}}.
var document = ExcelTemplateParser.Instance.Render("invoice-template.xlsx", invoice);

document.SaveAsExcel("invoice.xlsx");
document.SaveAsPdf("invoice.pdf");
```

## Paper sizes and page count

Select a paper size with `page.Settings = new PageSettings { PaperKind = PaperKind.A4 }`,
or use `p.Settings(s => s.PaperKind(PaperKind.A4))` in the fluent API.
Supported types are A5, B5, A4, B4, A3, and Customer; the B series uses ISO dimensions.
Standard dimensions are expressed in points (72 points per inch), and landscape layout automatically swaps width and height.
For custom dimensions, use `s.Size(300, 400)`, which selects Customer. The default Customer type preserves content-based sizing.
`page.CurrentPageNumber` stays synchronized with `page.PageNumber`, and `document.PageCount` returns the current number of pages or worksheets.
This count does not include additional physical pages created when a printer automatically paginates a worksheet.

## Packages

| Package | Purpose | Primary dependency |
| --- | --- | --- |
| `PrintSharp` | Grid document model, styles, fluent API, and layout engine | — |
| `PrintSharp.Excel` | `.xlsx` renderer and template parser | ClosedXML |
| `PrintSharp.Pdf` | PDF renderer | PDFsharp |
| `PrintSharp.Windows` | Windows printing renderer | `PrintDocument` / GDI+ |

## Design principles

1. **Grid-first layout.** A cell is addressed by row, column, `RowSpan`, and `ColumnSpan`; free coordinates are not the primary model.
2. **One layout calculation.** The layout engine calculates physical bounds from the grid so PDF and Windows rendering share the same geometry.
3. **Renderer-neutral core.** The core uses logical dimensions and `FontSpec`; Excel widths, PDF points, GDI+ units, and concrete fonts stay outside the core.
4. **Excel is a first-class design surface.** An Excel workbook can be both an output target and a visual template for PDF or print output.

For the complete architecture rationale (Chinese), see [PrintSharp Architecture: Excel Grid-first](https://github.com/ogisyouryuu/PrintSharp/blob/main/src/PrintSharp/PrintSharp_Architecture_ExcelGridFirst_v2_%E4%B8%AD%E6%96%87%E7%89%88.md).

## Development

```bash
dotnet test PrintSharp.slnx
```

The solution includes unit tests for the core grid/layout model, fluent API, styles, Excel rendering and templates, PDF rendering, and the Windows print-document adapter.

## License

Distributed under the [MIT License](https://github.com/ogisyouryuu/PrintSharp/blob/main/LICENSE).

## Windows Forms demo

The [WinForms demo](https://github.com/ogisyouryuu/PrintSharp/tree/main/demos/PrintSharp.WinForms.Demo) includes an editable 100-row DataGridView, code-built and Excel-template reports, Japanese hotel receipts and invoices, Excel/PDF export, and an embedded Windows print preview. Run it on Windows with:

```bash
dotnet run --project demos/PrintSharp.WinForms.Demo
```

## NuGet publishing

Create a version tag such as `v0.1.0` to run the release workflow. It builds and tests the solution, packs the four library packages, validates their contents, builds a local package consumer, and publishes to NuGet.org through Trusted Publishing.

Configure a GitHub Actions repository variable named `NUGET_USER` with the NuGet.org profile name. On NuGet.org, add a Trusted Publishing policy for repository owner `ogisyouryuu`, repository `PrintSharp`, and workflow file `nuget.yml`. Grant the policy permission to publish new package versions for the four `PrintSharp*` packages.
