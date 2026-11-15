# PrintSharp 0.4.0

## 改进

- 公式可携带缓存值（含 PageValue）；Excel 导出保留真实公式与缓存，PDF/Canvas 使用格式化后的值。支持同一逻辑页 SUM、AVERAGE、COUNT、MIN、MAX，未支持公式默认留空，也可显式选择文本或异常。
- ExcelReportLayout.SingleSheet 将每个逻辑 Report 输出为一个工作表，保留打印标题、页设置、可选冻结窗格和整表合计。
- 文档可指定 Culture，PDF 选项可覆盖；默认使用 InvariantCulture。
- v2 预览协议共享文档字形，使用压缩缓存和精简文字索引；保留 v1 接口兼容。
- PDF 下载支持文件名参数与文档 Title 默认值。
- 新增韩文、繁体中文及强类型 PreviewLabels，未知语言回退英文。
- DeleteOnDispose 可在组件销毁或切换文档时释放旧会话，默认关闭。
- 字体拆分为四个可选包，PrintSharp.Fonts.Noto 提供完整字体集合。

## 从 0.3.0 升级

- 日文、中文、韩文应用增加 PrintSharp.Fonts.NotoSansCjk；需要原有全部字体时增加 PrintSharp.Fonts.Noto。Pdf 默认只依赖拉丁字体。
- 如依赖调用线程的格式语言，显式设置 Document.Culture 或 PdfRenderOptions.Culture。
- 如需继续输出未计算的公式文本，设置 FormulaWithoutValue = FormulaFallback.Text。
- SingleSheet 中页码使用 PageValue.PageNumbers，或给现有 PageValue 设置 ExcelFooterText。
- Labels 推荐使用属性初始化；旧的目标类型推断字典索引初始化需要改为 PreviewLabels 属性。
- 自定义存储若依赖 PreviewEntry 的 record with/解构语法，改用构造函数；其兼容 JSON 属性按需解压，Size 计算压缩后的存储字节。

完整示例与限制见 [集成指南](canvas-preview.zh-CN.md) 和 [协议说明](preview-protocol.md)。

## 验证

229 项 .NET 测试、22 项浏览器测试通过；11 个本地 NuGet 测试包通过打包与消费验证。四种字体配置（拉丁、中日韩、全部、关闭自动复制）均已验证。

九页日文合成报表的 JSON 传输约从 4.16 MB 降至 0.15 MB，含 PDF 的存储字节约从 4.20 MB 降至 0.19 MB；实际收益随文档内容变化。
