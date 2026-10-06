namespace PrintSharp.Documents;

/// <summary>分页完成后求值的单元格内容，用于页码、小计等。</summary>
public sealed class PageValue
{
    private readonly Func<PageContext, object?> _resolve;
    /// <summary>创建延迟值；委托应保持纯粹，不改变页面布局。</summary>
    public PageValue(Func<PageContext, object?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        _resolve = resolve;
    }
    /// <summary>根据最终物理页上下文计算值。</summary>
    public object? Resolve(PageContext context) => _resolve(context);
}
