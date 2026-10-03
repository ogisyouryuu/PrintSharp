using PrintSharp.Cells;
using Xunit;

namespace PrintSharp.Tests;

public class CellReferenceTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(1, "B")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void ToColumnLetter_ShouldConvertCorrectly(int colIndex, string expected)
    {
        Assert.Equal(expected, CellReference.ToColumnLetter(colIndex));
    }

    [Theory]
    [InlineData("A", 0)]
    [InlineData("B", 1)]
    [InlineData("Z", 25)]
    [InlineData("AA", 26)]
    [InlineData("AB", 27)]
    [InlineData("ZZ", 701)]
    [InlineData("AAA", 702)]
    public void ToColumnIndex_ShouldConvertCorrectly(string letter, int expected)
    {
        Assert.Equal(expected, CellReference.ToColumnIndex(letter));
    }

    [Theory]
    [InlineData("A1", 0, 0)]
    [InlineData("B2", 1, 1)]
    [InlineData("C5", 4, 2)]
    [InlineData("AA10", 9, 26)]
    public void ParseCell_ShouldParseRowAndColumn(string a1, int expectedRow, int expectedCol)
    {
        var (row, col) = CellReference.ParseCell(a1);
        Assert.Equal(expectedRow, row);
        Assert.Equal(expectedCol, col);
    }

    [Theory]
    [InlineData("A1", 0, 0, 1, 1)]
    [InlineData("A1:C1", 0, 0, 1, 3)]
    [InlineData("B2:D4", 1, 1, 3, 3)]
    [InlineData("C5:A1", 0, 0, 5, 3)] // inverted range should normalize
    public void ParseRange_ShouldParseRangeCorrectly(string range, int startRow, int startCol, int rowSpan, int colSpan)
    {
        var (r, c, rs, cs) = CellReference.ParseRange(range);
        Assert.Equal(startRow, r);
        Assert.Equal(startCol, c);
        Assert.Equal(rowSpan, rs);
        Assert.Equal(colSpan, cs);
    }

    [Fact]
    public void FormatCellAndRange_ShouldFormatCorrectly()
    {
        Assert.Equal("A1", CellReference.FormatCell(0, 0));
        Assert.Equal("C5", CellReference.FormatCell(4, 2));
        Assert.Equal("A1", CellReference.FormatRange(0, 0, 1, 1));
        Assert.Equal("B2:D4", CellReference.FormatRange(1, 1, 3, 3));
    }
}
