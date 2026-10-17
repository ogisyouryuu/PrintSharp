using PdfSharp.Drawing;
using PrintSharp.Layout;
using PrintSharp.Pdf;
using PrintSharp.Styles;
using SkiaSharp;
using Topten.RichTextKit;

namespace PrintSharp.Api;

/// <summary>HarfBuzz の組版結果をベクター PDF として PDFsharp に合成します。</summary>
public sealed class NotoPdfTextRenderer(NotoFontRegistry fonts) : IPdfTextRenderer
{
    public IPdfTextPage BeginPage(float width, float height) => new TextPage(fonts, width, height);

    private sealed class TextPage : IPdfTextPage
    {
        private readonly NotoFontRegistry _fonts;
        private readonly MemoryStream _stream = new();
        private readonly SKDocument _document;
        private readonly SKCanvas _canvas;
        private readonly float _width;
        private readonly float _height;
        private bool _hasText;

        public TextPage(NotoFontRegistry fonts, float width, float height)
        {
            _fonts = fonts;
            _width = width;
            _height = height;
            _document = SKDocument.CreatePdf(_stream);
            _canvas = _document.BeginPage(width, height);
        }

        public void DrawText(string text, CellStyle style, LayoutRect content, float scale)
        {
            var font = style.Font ?? FontSpec.Default;
            var color = style.ForeColor ?? ColorSpec.Black;
            var block = new TextBlock
            {
                FontMapper = _fonts,
                MaxWidth = style.WrapText ? content.Width : null,
                RenderWidth = content.Width,
                Alignment = style.HorizontalAlignment switch
                {
                    HorizontalAlignment.Center => TextAlignment.Center,
                    HorizontalAlignment.Right => TextAlignment.Right,
                    HorizontalAlignment.Left => TextAlignment.Left,
                    _ => TextAlignment.Auto
                }
            };
            foreach (var run in _fonts.ResolveRuns(text, font.Family, font.Bold))
                block.AddText(run.Text, new Style
                {
                    FontFamily = run.Family,
                    FontSize = Math.Max(1, font.Size * scale),
                    FontWeight = font.Bold ? 700 : 400,
                    FontItalic = font.Italic,
                    TextColor = new SKColor(color.R, color.G, color.B, color.A),
                    Underline = font.Underline ? UnderlineStyle.Solid : UnderlineStyle.None,
                    StrikeThrough = font.StrikeThrough ? StrikeThroughStyle.Solid : StrikeThroughStyle.None
                });
            float offset = style.VerticalAlignment switch
            {
                VerticalAlignment.Bottom => content.Height - block.MeasuredHeight,
                VerticalAlignment.Middle => (content.Height - block.MeasuredHeight) / 2,
                _ => 0
            };
            _canvas.Save();
            try
            {
                // 固定行高の帳票では隣接セルへのはみ出しを防止します。
                _canvas.ClipRect(new SKRect(content.X, content.Y, content.Right, content.Bottom));
                _canvas.Translate(content.X, content.Y + Math.Max(0, offset));
                block.Paint(_canvas);
                _hasText = true;
            }
            finally { _canvas.Restore(); }
        }

        public void Complete(XGraphics graphics)
        {
            _document.EndPage();
            _document.Close();
            if (!_hasText) return;
            _stream.Position = 0;
            using var form = XPdfForm.FromStream(_stream);
            graphics.DrawImage(form, 0, 0, _width, _height);
        }

        public void Dispose()
        {
            _document.Dispose();
            _stream.Dispose();
        }
    }
}
