namespace PrintSharp.Documents;

/// <summary>
/// 表示文档的元数据信息。
/// </summary>
public sealed record DocumentMetadata
{
    /// <summary>
    /// 获取或设置文档标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 获取或设置文档作者。
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// 获取或设置文档主题。
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// 获取或设置文档关键词。
    /// </summary>
    public string? Keywords { get; set; }

    /// <summary>
    /// 获取或设置创建时间。
    /// </summary>
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 获取或设置修改时间。
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
