using PrintSharp.Layout;
using PrintSharp.Styles;

namespace PrintSharp.Pdf.Preview;

/// <summary>プレビューと PDF で共有する文字描画計画を生成します。</summary>
public interface IDisplayListTextRenderer : IPdfTextRenderer
{
    /// <summary>文字を組版し、共通描画命令を追加します。</summary>
    void AppendText(DisplayListBuilder builder, string text, CellStyle style, LayoutRect content, float scale);
}
