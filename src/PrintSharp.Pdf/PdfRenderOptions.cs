namespace PrintSharp.Pdf;

/// <summary>未評価の数式の出力方法です。</summary>
public enum FormulaFallback
{
    /// <summary>空白として表示します。</summary>
    Empty,
    /// <summary>数式の文字列を表示します。</summary>
    Text,
    /// <summary>セルの位置を含む例外を送出します。</summary>
    Throw
}


/// <summary>PDF 输出尺寸与字体解析选项。</summary>
public sealed record PdfRenderOptions
{
    /// <summary>文書の指定より優先する書式カルチャです。</summary>
    public System.Globalization.CultureInfo? Culture { get; init; }
    /// <summary>評価できずキャッシュ値もない数式の処理方法です。</summary>
    public FormulaFallback FormulaWithoutValue { get; init; } = FormulaFallback.Empty;
    /// <summary>セルに Padding が指定されていない場合の文字内余白。論理単位で指定します。</summary>
    public PrintSharp.Styles.PaddingSpec DefaultTextPadding { get; init; } = new(3f, 2f);

    /// <summary>每个 Core 逻辑尺寸单位对应的 PDF 点数。</summary>
    public float PointsPerLogicalUnit { get; init; } = 1f;

    /// <summary>Core 字体解析器；未指定时使用默认解析器。</summary>
    public PrintSharp.Layout.Fonts.IFontResolver? FontResolver { get; init; }

    /// <summary>任意の文字レンダラー。未指定時は同梱 Noto フォントと HarfBuzz で描画します。</summary>
    public IPdfTextRenderer? TextRenderer { get; init; }

    /// <summary>默认 PDF 渲染选项。</summary>
    public static PdfRenderOptions Default { get; } = new();
}
