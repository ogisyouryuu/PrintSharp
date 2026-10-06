using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Fluent;
using PrintSharp.Grid;
using PrintSharp.Layout;
using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Tests;

public class DocumentPaginatorTests
{
    private static Document Report(int count = 7) => Document.Create(d => d.Page("Report", p => p
        .Settings(s => s.Size(100, 100).Margins(10))
        .Columns(50)
        .Report(r => r
            .Header(h => h.Row(10, row => row.Cell("Header")))
            .Body(Enumerable.Range(1, count), 20, (row, n) => row.Cell(n))
            .Footer(f => f.Row(10, row => row.Cell(new PageValue(ctx =>
                $"{ctx.CurrentPageNumber}/{ctx.PageCount}:{ctx.BodyRowOffset}+{ctx.BodyRowCount}")))))));

    [Fact]
    public void Report_FillsWholeRowsAndRepeatsSectionsWithoutChangingSource()
    {
        var source = Report();
        var result = source.Paginate();
        Assert.Equal(1, source.PageCount);
        Assert.Equal(3, result.PageCount);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, result.Pages.SelectMany(p => p.Cells)
            .Where(c => c.Value is int).Select(c => (int)c.Value!));
        Assert.All(result.Pages, p => Assert.Equal("Header", p.FindCell(0, 0)!.Value));
        Assert.Equal("1/3:0+3", result.Pages[0].Cells.Last().Value);
        Assert.Equal("2/3:3+3", result.Pages[1].Cells.Last().Value);
        Assert.Equal("3/3:6+1", result.Pages[2].Cells.Last().Value);
        Assert.IsType<PageValue>(source.DefaultPage.Cells.Last().Value);
        Assert.Equal(3, source.CalculateLayout().PageCount);
        Assert.Equal(result.Pages.Select(p => p.Cells.Last().Value), source.Paginate().Pages.Select(p => p.Cells.Last().Value));
        Assert.All(result.Pages, p => Assert.Null(p.Pagination));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(6, 2)]
    public void EmptyBodyAndExactFit_DoNotProduceExtraPages(int rows, int expected)
    {
        Assert.Equal(expected, Report(rows).Paginate().PageCount);
    }

    [Fact]
    public void ExistingUnpaginatedPagesKeepIdentityAndPageNumbers()
    {
        var source = Report();
        source.DefaultPage.Pagination = null;
        source.DefaultPage.Cells.Remove(source.DefaultPage.Cells.Last());
        source.DefaultPage.PageNumber = 9;
        var layout = source.CalculateLayout();
        Assert.Single(layout.Pages);
        Assert.Same(source.DefaultPage, layout.DefaultPage.Page);
        Assert.Equal(9, layout.DefaultPage.Page.PageNumber);
    }

    [Fact]
    public void MixedDocuments_ResolveGlobalPhysicalPageNumbers()
    {
        var source = Report(4);
        var cover = new Page("Cover");
        cover.SetCell(new Cell(0, 0, new PageValue(c => $"{c.CurrentPageNumber}/{c.PageCount}")));
        source.Pages.Insert(0, cover);
        var result = source.Paginate();
        Assert.Equal("1/3", result.Pages[0].Cells[0].Value);
        Assert.Equal("2/3:0+3", result.Pages[1].Cells.Last().Value);
        Assert.Equal("3/3:3+1", result.Pages[2].Cells.Last().Value);
    }

    [Fact]
    public void VariableHeightsAndHiddenRows_UseActualHeight()
    {
        var source = Report(4);
        source.DefaultPage.Rows.Single(r => r.Index == 1).Height = 40;
        source.DefaultPage.Rows.Single(r => r.Index == 2).IsHidden = true;
        var result = source.Paginate();
        Assert.Equal(2, result.PageCount);
        Assert.Equal(new[] { 1, 2, 3 }, result.Pages[0].Cells.Where(c => c.Value is int).Select(c => (int)c.Value!));
        Assert.True(result.Pages[0].Rows.Single(r => r.Index == 2).IsHidden);
    }

    [Fact]
    public void MergedRowGroupsMoveTogetherAndPreserveStyles()
    {
        var source = Report(4);
        var page = source.DefaultPage;
        page.Columns[0].Width = 20;
        page.Columns.Add(new Column(1, 20));
        page.SetCell(new Cell(3, 1, "Merged", 2, 1, style: new CellStyle { BackColor = ColorSpec.Black }));
        var result = source.Paginate();
        Assert.Equal(new[] { 1, 2 }, result.Pages[0].Cells.Where(c => c.Value is int).Select(c => (int)c.Value!));
        var merged = result.Pages[1].Cells.Single(c => Equals(c.Value, "Merged"));
        Assert.Equal(1, merged.Row);
        Assert.Equal(2, merged.RowSpan);
        Assert.Equal(ColorSpec.Black, merged.Style!.BackColor);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 0)]
    public void AutomaticPaginationRequiresFixedPaper(float width, float height)
    {
        var source = Report();
        source.DefaultPage.Settings = source.DefaultPage.Settings with { Width = width, Height = height };
        Assert.Throws<InvalidOperationException>(() => source.Paginate());
    }

    [Fact]
    public void OversizedRowsAndSectionMergesAreRejected()
    {
        var source = Report();
        source.DefaultPage.Rows.Single(r => r.Index == 1).Height = 61;
        Assert.Throws<InvalidOperationException>(() => source.Paginate());
        source.DefaultPage.Rows.Single(r => r.Index == 1).Height = 20;
        source.DefaultPage.SetCell(new Cell(0, 0, "Invalid", 2));
        Assert.Throws<InvalidOperationException>(() => source.Paginate());
    }

    [Fact]
    public void LandscapeUsesPrintableStandardPaperHeight()
    {
        var source = Report(30);
        source.DefaultPage.Settings = source.DefaultPage.Settings with { PaperKind = PaperKind.A5, Orientation = PageOrientation.Landscape };
        // A5 landscape height 419.53; margins 20; repeated rows 20; 18 body rows per page.
        var result = source.Paginate();
        Assert.Equal(2, result.PageCount);
        Assert.Equal(18, result.Pages[0].Cells.Count(c => c.Value is int));
    }

    [Fact]
    public void ExistingGridCanOptInWithHeaderAndFooterCounts()
    {
        var source = Report();
        source.DefaultPage.Pagination = null;
        new PageBuilder(source.DefaultPage).Paginate(1, 1);
        Assert.Equal(3, source.Paginate().PageCount);
    }

    [Fact]
    public void InvalidGeometryIsRejectedInsteadOfProducingInvalidPages()
    {
        var source = Report();
        source.DefaultPage.Rows.Single(r => r.Index == 1).Height = float.PositiveInfinity;
        Assert.Throws<InvalidOperationException>(() => source.Paginate());
        source.DefaultPage.Rows.Single(r => r.Index == 1).Height = 20;
        source.DefaultPage.Pagination = new PaginationSettings { HeaderRowCount = 100 };
        Assert.Throws<InvalidOperationException>(() => source.Paginate());
        source.DefaultPage.Pagination = new PaginationSettings { HeaderRowCount = 1, FooterRowCount = 1 };
        source.DefaultPage.Columns[0].Width = 81;
        Assert.Throws<InvalidOperationException>(() => source.Paginate());
    }
}
