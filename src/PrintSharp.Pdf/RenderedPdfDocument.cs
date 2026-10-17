namespace PrintSharp.Pdf;

/// <summary>描画済み PDF とページ情報の読み取り専用スナップショットです。PDF 編集機能は提供しません。</summary>
public sealed class RenderedPdfDocument : IDisposable
{
    private byte[]? _bytes;

    internal RenderedPdfDocument(byte[] bytes, IReadOnlyList<RenderedPdfPage> pages, RenderedPdfMetadata info)
    {
        _bytes = bytes;
        Pages = pages;
        Info = info;
    }

    /// <summary>出力済みの物理ページ数。</summary>
    public int PageCount => Pages.Count;

    /// <summary>PDF ポイント単位のページ寸法。</summary>
    public IReadOnlyList<RenderedPdfPage> Pages { get; }

    /// <summary>PDF に書き込まれたメタデータ。</summary>
    public RenderedPdfMetadata Info { get; }

    /// <summary>PDF データのコピーを返します。</summary>
    public byte[] ToArray() => (byte[])Bytes.Clone();

    /// <summary>指定したファイルに PDF を保存します。</summary>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, Bytes);
    }

    /// <summary>PDF を書き込みます。既定では呼び出し元のストリームを閉じません。</summary>
    public void Save(Stream stream, bool closeStream = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try { stream.Write(Bytes); }
        finally { if (closeStream) stream.Dispose(); }
    }

    private byte[] Bytes => _bytes ?? throw new ObjectDisposedException(nameof(RenderedPdfDocument));

    /// <summary>保持している PDF データを解放します。</summary>
    public void Dispose() => _bytes = null;
}

/// <summary>描画済みページの幅と高さ。単位は PDF ポイントです。</summary>
public sealed record RenderedPdfPage(float Width, float Height);

/// <summary>描画時点の文書メタデータ。</summary>
public sealed record RenderedPdfMetadata(string? Title, string? Author, string? Subject, string? Keywords);
