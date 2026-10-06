namespace PrintSharp.Documents;

/// <summary>每页重复的页头、页尾行数；中间明细按可打印高度自动分页。</summary>
public sealed record PaginationSettings
{
    /// <summary>逻辑页面开头的重复行数。</summary>
    public int HeaderRowCount { get; init; }
    /// <summary>逻辑页面末尾的重复行数。</summary>
    public int FooterRowCount { get; init; }
}
