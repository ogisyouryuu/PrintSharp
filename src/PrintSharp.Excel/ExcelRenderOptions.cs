namespace PrintSharp.Excel;

/// <summary>帳票の Excel シート構成です。</summary>
public enum ExcelReportLayout
{
    /// <summary>物理ページごとにシートを生成します。</summary>
    PerPhysicalPage,
    /// <summary>論理ページごとに明細を連続させます。</summary>
    SingleSheet
}


/// <summary>
/// Excel 渲染选项。
/// </summary>
public sealed record ExcelRenderOptions
{
    /// <summary>帳票を物理ページごと、または論理ページごとに出力します。</summary>
    public ExcelReportLayout ReportLayout { get; init; } = ExcelReportLayout.PerPhysicalPage;
    /// <summary>単一シート帳票の見出し行を固定します。</summary>
    public bool FreezeHeaderRows { get; init; }
    /// <summary>
    /// PrintSharp 逻辑列宽转换为 Excel 字符列宽的换算系数。
    /// 默认值为 0.125f（即 80 逻辑单位约为 10 字符宽度）。
    /// </summary>
    public float ColumnWidthScale { get; init; } = 0.125f;

    /// <summary>
    /// PrintSharp 逻辑行高转换为 Excel 磅值行高的换算系数。
    /// 默认值为 0.75f（即 20 逻辑单位约为 15 磅标准行高）。
    /// </summary>
    public float RowHeightScale { get; init; } = 0.75f;

    /// <summary>
    /// 获取或设置是否自动在未显式定义行列尺寸时根据内容自适应。
    /// </summary>
    public bool AutoFitColumns { get; init; }

    /// <summary>
    /// 获取默认渲染选项实例。
    /// </summary>
    public static ExcelRenderOptions Default => new();
}
