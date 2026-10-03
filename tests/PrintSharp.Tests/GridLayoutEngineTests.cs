using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Grid;
using PrintSharp.Layout;
using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Tests;

public class GridLayoutEngineTests
{
    [Fact]
    public void CalculatePage_ShouldComputeCorrectCellRectangles()
    {
        var page = new Page("Sheet1");
        page.Settings = new PageSettings
        {
            Margins = new PaddingSpec(0f), // 0 margin for simple calculation
            DefaultColumnWidth = 50f,
            DefaultRowHeight = 20f
        };

        // Col 0: 100, Col 1: 60, Col 2: 100
        page.Columns.Add(new Column(0, 100f));
        page.Columns.Add(new Column(1, 60f));
        page.Columns.Add(new Column(2, 100f));

        // Row 0: 30, Row 1: 24, Row 2: 24
        page.Rows.Add(new Row(0, 30f));
        page.Rows.Add(new Row(1, 24f));
        page.Rows.Add(new Row(2, 24f));

        // Cell A1: merged A1:C1 (row 0, col 0, rowSpan 1, colSpan 3)
        var cellTitle = new Cell(0, 0, "Title", rowSpan: 1, columnSpan: 3);
        page.SetCell(cellTitle);

        // Cell B2 (row 1, col 1)
        var cellB2 = new Cell(1, 1, "Qty");
        page.SetCell(cellB2);

        // Cell C3 (row 2, col 2)
        var cellC3 = new Cell(2, 2, 1000m);
        page.SetCell(cellC3);

        var engine = new GridLayoutEngine();
        var layout = engine.CalculatePage(page);

        // Verify Title bounds (X=0, Y=0, Width=100+60+100=260, Height=30)
        var titleLayout = layout.FindCell(0, 0);
        Assert.NotNull(titleLayout);
        Assert.Equal(0f, titleLayout.Bounds.X);
        Assert.Equal(0f, titleLayout.Bounds.Y);
        Assert.Equal(260f, titleLayout.Bounds.Width);
        Assert.Equal(30f, titleLayout.Bounds.Height);

        // Verify Cell B2 bounds: X = Col 0 width = 100, Y = Row 0 height = 30, Width = Col 1 width = 60, Height = Row 1 height = 24
        var b2Layout = layout.FindCell(1, 1);
        Assert.NotNull(b2Layout);
        Assert.Equal(100f, b2Layout.Bounds.X);
        Assert.Equal(30f, b2Layout.Bounds.Y);
        Assert.Equal(60f, b2Layout.Bounds.Width);
        Assert.Equal(24f, b2Layout.Bounds.Height);

        // Verify Cell C3 bounds: X = 100+60 = 160, Y = 30+24 = 54, Width = 100, Height = 24
        var c3Layout = layout.FindCell(2, 2);
        Assert.NotNull(c3Layout);
        Assert.Equal(160f, c3Layout.Bounds.X);
        Assert.Equal(54f, c3Layout.Bounds.Y);
        Assert.Equal(100f, c3Layout.Bounds.Width);
        Assert.Equal(24f, c3Layout.Bounds.Height);

        // Verify content dimensions
        Assert.Equal(260f, layout.ContentWidth);
        Assert.Equal(78f, layout.ContentHeight);
    }

    [Fact]
    public void CalculatePage_WithPadding_ShouldComputeCorrectContentBounds()
    {
        var page = new Page("Sheet1");
        page.Settings = new PageSettings { Margins = PaddingSpec.Zero, DefaultColumnWidth = 100f, DefaultRowHeight = 40f };

        var style = new CellStyle { Padding = new PaddingSpec(5f, 10f, 5f, 10f) };
        var cell = new Cell(0, 0, "Hello", style: style);
        page.SetCell(cell);

        var engine = new GridLayoutEngine();
        var layout = engine.CalculatePage(page);

        var cellLayout = layout.FindCell(0, 0);
        Assert.NotNull(cellLayout);
        Assert.Equal(0f, cellLayout.Bounds.X);
        Assert.Equal(0f, cellLayout.Bounds.Y);
        Assert.Equal(100f, cellLayout.Bounds.Width);
        Assert.Equal(40f, cellLayout.Bounds.Height);

        // ContentBounds should subtract padding
        Assert.Equal(5f, cellLayout.ContentBounds.X);
        Assert.Equal(10f, cellLayout.ContentBounds.Y);
        Assert.Equal(90f, cellLayout.ContentBounds.Width); // 100 - 5 - 5
        Assert.Equal(20f, cellLayout.ContentBounds.Height); // 40 - 10 - 10
    }
}
