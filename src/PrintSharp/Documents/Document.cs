using PrintSharp.Cells;
using PrintSharp.Fluent;
using PrintSharp.Grid;

namespace PrintSharp.Documents;

/// <summary>
/// 表示以 Excel 网格（Grid-first）为母模型的通用文档定义。
/// 可统一导出为 Excel、PDF 或发送至 Windows 打印机。
/// </summary>
public sealed class Document
{
    private readonly List<Page> _pages = [];

    /// <summary>
    /// 获取或设置文档元数据信息。
    /// </summary>
    public DocumentMetadata Metadata { get; set; } = new();

    /// <summary>
    /// 获取文档中的页面集合。
    /// </summary>
    public IList<Page> Pages => _pages;

    /// <summary>获取当前文档的总页数（页面/工作表数量）。</summary>
    public int PageCount => _pages.Count;

    /// <summary>
    /// 获取默认页面（若不存在则自动创建首页）。
    /// </summary>
    public Page DefaultPage
    {
        get
        {
            if (_pages.Count == 0)
            {
                _pages.Add(new Page("Sheet1", 1));
            }
            return _pages[0];
        }
    }

    /// <summary>
    /// 便捷访问默认页面的行集合。
    /// </summary>
    public IList<Row> Rows => DefaultPage.Rows;

    /// <summary>
    /// 便捷访问默认页面的列集合。
    /// </summary>
    public IList<Column> Columns => DefaultPage.Columns;

    /// <summary>
    /// 便捷访问默认页面的单元格集合。
    /// </summary>
    public IList<Cell> Cells => DefaultPage.Cells;

    /// <summary>
    /// 初始化 <see cref="Document"/> 类的新实例。
    /// </summary>
    public Document()
    {
        _pages.Add(new Page("Sheet1", 1));
    }

    /// <summary>
    /// 添加一个新页面并返回。
    /// </summary>
    /// <param name="name">页面名称。</param>
    /// <returns>新增的 <see cref="Page"/> 实例。</returns>
    public Page AddPage(string? name = null)
    {
        int pageNumber = _pages.Count + 1;
        var page = new Page(name ?? $"Sheet{pageNumber}", pageNumber);
        _pages.Add(page);
        return page;
    }

    /// <summary>
    /// 使用 Fluent API 快速构建一个新文档。
    /// </summary>
    /// <param name="configure">配置委托。</param>
    /// <returns>构建完成的 <see cref="Document"/> 实例。</returns>
    public static Document Create(Action<DocumentBuilder>? configure = null)
    {
        var builder = new DocumentBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }
}
