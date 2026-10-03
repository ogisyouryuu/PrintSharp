using PrintSharp.Documents;

namespace PrintSharp.Fluent;

/// <summary>
/// 文档元数据 Fluent 构建器。
/// </summary>
public sealed class MetadataBuilder
{
    private readonly DocumentMetadata _metadata = new();

    /// <summary>
    /// 设置文档标题。
    /// </summary>
    public MetadataBuilder Title(string title)
    {
        _metadata.Title = title;
        return this;
    }

    /// <summary>
    /// 设置文档作者。
    /// </summary>
    public MetadataBuilder Author(string author)
    {
        _metadata.Author = author;
        return this;
    }

    /// <summary>
    /// 设置文档主题。
    /// </summary>
    public MetadataBuilder Subject(string subject)
    {
        _metadata.Subject = subject;
        return this;
    }

    /// <summary>
    /// 设置文档关键词。
    /// </summary>
    public MetadataBuilder Keywords(string keywords)
    {
        _metadata.Keywords = keywords;
        return this;
    }

    /// <summary>
    /// 设置创建时间。
    /// </summary>
    public MetadataBuilder CreatedAt(DateTime createdAt)
    {
        _metadata.CreatedAt = createdAt;
        return this;
    }

    /// <summary>
    /// 构建 <see cref="DocumentMetadata"/> 实例。
    /// </summary>
    public DocumentMetadata Build() => _metadata;
}
