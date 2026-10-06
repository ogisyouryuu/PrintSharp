namespace PrintSharp.Documents;

/// <summary>整个文档分页完成后的物理页信息。</summary>
public sealed record PageContext
{
    /// <summary>原始逻辑页面。</summary>
    public required Page SourcePage { get; init; }
    /// <summary>文档物理页码，从 1 开始。</summary>
    public required int CurrentPageNumber { get; init; }
    /// <summary>文档物理总页数。</summary>
    public required int PageCount { get; init; }
    /// <summary>本页明细相对于逻辑明细首行的偏移，从 0 开始。</summary>
    public required int BodyRowOffset { get; init; }
    /// <summary>本页的明细网格行数。</summary>
    public required int BodyRowCount { get; init; }
}
