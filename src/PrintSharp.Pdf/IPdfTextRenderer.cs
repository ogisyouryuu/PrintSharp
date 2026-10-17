using PdfSharp.Drawing;
using PrintSharp.Layout;
using PrintSharp.Styles;

namespace PrintSharp.Pdf;

/// <summary>ページ単位で文字組版を拡張します。</summary>
public interface IPdfTextRenderer
{
    /// <summary>PDF ポイント単位の文字レイヤーを作成します。</summary>
    IPdfTextPage BeginPage(float width, float height);
}

/// <summary>文字レイヤーの描画とリソース管理を行います。</summary>
public interface IPdfTextPage : IDisposable
{
    /// <summary>セル内に文字を描画します。座標は PDF ポイント単位です。</summary>
    void DrawText(string text, CellStyle style, LayoutRect content, float scale);

    /// <summary>組版した文字を PDFsharp ページに合成します。</summary>
    void Complete(XGraphics graphics);
}
