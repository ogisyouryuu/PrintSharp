using PrintSharp.Layout;
using PrintSharp.Styles;
using SkiaSharp;

namespace PrintSharp.Pdf;

/// <summary>PDF ポイント単位のセル領域に文字を組版します。</summary>
public interface IPdfTextRenderer
{
    /// <summary>呼び出し元が所有するキャンバスに描画します。破棄やページ終了は行いません。</summary>
    void DrawText(SKCanvas canvas, string text, CellStyle style, LayoutRect content, float scale);
}
