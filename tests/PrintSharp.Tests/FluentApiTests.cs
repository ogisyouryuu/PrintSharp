using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Fluent;
using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Tests;

public class FluentApiTests
{
    private record Product(string Name, int Quantity, decimal Price);

    [Fact]
    public void Document_Create_FluentBuilding_ShouldPopulateDocumentCorrectly()
    {
        var doc = Document.Create(d =>
        {
            d.Title("Sales Report")
             .Author("PrintSharp Team")
             .Page(p =>
             {
                 p.Name("Invoice")
                  .Columns(120f, 60f, 100f)
                  .Rows(30f, 25f, 25f)
                  // Merged title header
                  .Cell("A1:C1", c =>
                  {
                      c.Value("销售明细表")
                       .Font("Yu Gothic", 16, bold: true)
                       .AlignCenter()
                       .AlignMiddle()
                       .Background("#F0F0F0")
                       .BorderBottom(BorderStyle.Medium, ColorSpec.Black);
                  })
                  // Headers
                  .Cell("A2", c => c.Value("商品名称").Bold().AlignCenter())
                  .Cell("B2", c => c.Value("数量").Bold().AlignRight())
                  .Cell("C2", c => c.Value("金额").Bold().AlignRight())
                  // Row 2 data
                  .Cell("A3", "苹果")
                  .Cell("B3", c => c.Value(10).AlignRight())
                  .Cell("C3", c => c.Value(1000m).Format("#,##0.00").AlignRight());
             });
        });

        Assert.Equal("Sales Report", doc.Metadata.Title);
        Assert.Equal("PrintSharp Team", doc.Metadata.Author);

        var page = doc.DefaultPage;
        Assert.Equal("Invoice", page.Name);
        Assert.Equal(3, page.Columns.Count);
        Assert.Equal(120f, page.Columns[0].Width);
        Assert.Equal(60f, page.Columns[1].Width);
        Assert.Equal(100f, page.Columns[2].Width);

        // Check merged title cell
        var titleCell = page.FindCell("A1");
        Assert.NotNull(titleCell);
        Assert.Equal("销售明细表", titleCell.Value);
        Assert.Equal(1, titleCell.RowSpan);
        Assert.Equal(3, titleCell.ColumnSpan);
        Assert.Equal(HorizontalAlignment.Center, titleCell.Style?.HorizontalAlignment);
        Assert.Equal("Yu Gothic", titleCell.Style?.Font?.Family);
        Assert.Equal(16f, titleCell.Style?.Font?.Size);
        Assert.True(titleCell.Style?.Font?.Bold);
        Assert.Equal(BorderStyle.Medium, titleCell.Style?.Border?.Bottom?.Style);

        // Check data cells
        var appleCell = page.FindCell(2, 0);
        Assert.NotNull(appleCell);
        Assert.Equal("苹果", appleCell.Value);

        var amountCell = page.FindCell("C3");
        Assert.NotNull(amountCell);
        Assert.Equal(1000m, amountCell.Value);
        Assert.Equal("#,##0.00", amountCell.Style?.Format);
        Assert.Equal(HorizontalAlignment.Right, amountCell.Style?.HorizontalAlignment);
    }

    [Fact]
    public void Document_RowBuilder_ShouldSequentiallyAssignColumns()
    {
        var doc = Document.Create(d =>
        {
            d.Page(p =>
            {
                p.Row(30f, r =>
                {
                    r.Cell("Item").Bold();
                    r.Cell("Qty").AlignCenter();
                    r.Cell("Price").AlignRight();
                });

                p.Row(24f, r =>
                {
                    r.Cell("Banana");
                    r.Cell(5);
                    r.Cell(250m);
                });
            });
        });

        var page = doc.DefaultPage;
        Assert.Equal(6, page.Cells.Count);

        // Row 0
        Assert.Equal("Item", page.FindCell(0, 0)?.Value);
        Assert.Equal("Qty", page.FindCell(0, 1)?.Value);
        Assert.Equal("Price", page.FindCell(0, 2)?.Value);

        // Row 1
        Assert.Equal("Banana", page.FindCell(1, 0)?.Value);
        Assert.Equal(5, page.FindCell(1, 1)?.Value);
        Assert.Equal(250m, page.FindCell(1, 2)?.Value);
    }

    [Fact]
    public void Document_TableBuilder_ShouldGenerateTableRows()
    {
        var products = new List<Product>
        {
            new("Laptop", 1, 1200m),
            new("Mouse", 2, 25m),
            new("Keyboard", 1, 75m)
        };

        var doc = Document.Create(d =>
        {
            d.Page(p =>
            {
                p.Table(t =>
                {
                    t.StartAt(row: 1, column: 0)
                     .Columns(150f, 60f, 90f)
                     .DefaultHeaderStyle(s => s.Bold().Background("#DDDDDD"))
                     .Header(h =>
                     {
                         h.Cell("Product");
                         h.Cell("Qty").AlignCenter();
                         h.Cell("Price").AlignRight();
                     })
                     .Rows(products, (r, p) =>
                     {
                         r.Cell(p.Name);
                         r.Cell(p.Quantity).AlignCenter();
                         r.Cell(p.Price).Format("$#,##0.00").AlignRight();
                     });
                });
            });
        });

        var page = doc.DefaultPage;

        // Check header at row 1
        Assert.Equal("Product", page.FindCell(1, 0)?.Value);
        Assert.Equal("Qty", page.FindCell(1, 1)?.Value);
        Assert.Equal("Price", page.FindCell(1, 2)?.Value);

        // Check data rows
        Assert.Equal("Laptop", page.FindCell(2, 0)?.Value);
        Assert.Equal(1, page.FindCell(2, 1)?.Value);
        Assert.Equal(1200m, page.FindCell(2, 2)?.Value);

        Assert.Equal("Mouse", page.FindCell(3, 0)?.Value);
        Assert.Equal(2, page.FindCell(3, 1)?.Value);

        Assert.Equal("Keyboard", page.FindCell(4, 0)?.Value);
        Assert.Equal(75m, page.FindCell(4, 2)?.Value);
    }

    [Fact]
    public void Document_CalculateLayout_ShouldWorkSeamlessly()
    {
        var doc = Document.Create(d =>
        {
            d.DefaultRowHeight(20f)
             .DefaultColumnWidth(100f)
             .Cell("A1:B2", c => c.Value("2x2 Block"));
        });

        var layout = doc.CalculateLayout();
        Assert.Single(layout.Pages);

        var pageLayout = layout.DefaultPage;
        var blockLayout = pageLayout.FindCell(0, 0);

        Assert.NotNull(blockLayout);
        Assert.Equal(200f, blockLayout.Bounds.Width); // 2 columns of 100
        Assert.Equal(40f, blockLayout.Bounds.Height);  // 2 rows of 20
    }

    [Fact]
    public void Document_MultiPage_ShouldSupportMultiplePages()
    {
        var doc = Document.Create(d =>
        {
            d.Page("Page 1", p => p.Cell("A1", "First Page"))
             .Page("Page 2", p => p.Cell("A1", "Second Page"));
        });

        Assert.Equal(2, doc.Pages.Count);
        Assert.Equal("Page 1", doc.Pages[0].Name);
        Assert.Equal("Page 2", doc.Pages[1].Name);
        Assert.Equal("First Page", doc.Pages[0].FindCell(0, 0)?.Value);
        Assert.Equal("Second Page", doc.Pages[1].FindCell(0, 0)?.Value);
    }
}
