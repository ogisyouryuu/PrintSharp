namespace PrintSharp.Documents;

/// <summary>分页完成后求值的单元格内容，用于页码、小计等。</summary>
public sealed class PageValue
{
    private readonly Func<PageContext, object?> _resolve;
    /// <summary>Excel の印刷フッター表現です。通常の遅延値では null です。</summary>
    public string? ExcelFooterText { get; init; }
    /// <summary>印刷ページ番号です。</summary>
    public static PageValue PageNumber { get; } = new(c => c.CurrentPageNumber) { ExcelFooterText = "&P" };
    /// <summary>印刷総ページ数です。</summary>
    public static PageValue PageCount { get; } = new(c => c.PageCount) { ExcelFooterText = "&N" };
    /// <summary>現在ページと総ページ数です。</summary>
    public static PageValue PageNumbers { get; } = new(c => $"{c.CurrentPageNumber} / {c.PageCount}") { ExcelFooterText = "&P / &N" };
    /// <summary>创建延迟值；委托应保持纯粹，不改变页面布局。</summary>
    public PageValue(Func<PageContext, object?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        _resolve = resolve;
    }
    /// <summary>根据最终物理页上下文计算值。</summary>
    public object? Resolve(PageContext context) => _resolve(context);
}
