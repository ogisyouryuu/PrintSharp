using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Tests;

public class StyleTests
{
    [Fact]
    public void ColorSpec_FromHex_ShouldParseStandardFormats()
    {
        var black = ColorSpec.FromHex("#000000");
        Assert.Equal(0, black.R);
        Assert.Equal(0, black.G);
        Assert.Equal(0, black.B);
        Assert.Equal(255, black.A);

        var red = ColorSpec.FromHex("#FF0000");
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);

        var shortHex = ColorSpec.FromHex("#0F0");
        Assert.Equal(0, shortHex.R);
        Assert.Equal(255, shortHex.G);
        Assert.Equal(0, shortHex.B);

        var alphaHex = ColorSpec.FromHex("#800000FF");
        Assert.Equal(128, alphaHex.A);
        Assert.Equal(0, alphaHex.R);
        Assert.Equal(0, alphaHex.G);
        Assert.Equal(255, alphaHex.B);
    }

    [Fact]
    public void CellStyle_MergeWith_ShouldOverlayProperties()
    {
        var baseStyle = new CellStyle
        {
            Font = new FontSpec("Arial", 12),
            ForeColor = ColorSpec.Black,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var overlay = new CellStyle
        {
            Font = new FontSpec("Yu Gothic", 14, bold: true),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var merged = baseStyle.MergeWith(overlay);

        Assert.Equal("Yu Gothic", merged.Font?.Family);
        Assert.Equal(14, merged.Font?.Size);
        Assert.True(merged.Font?.Bold);
        Assert.Equal(ColorSpec.Black, merged.ForeColor); // kept from baseStyle
        Assert.Equal(HorizontalAlignment.Center, merged.HorizontalAlignment);
    }
}
