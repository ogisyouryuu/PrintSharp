# PrintSharp 0.4.1

## 改进

- 新增 `CellBuilder.SumBodyColumn(column, value)`，按输出工作表的实际明细范围生成合计公式，支持页头增行和分页。空明细输出 `=0`，缓存值和打印值均为 0。
- 新增 `PageContext.IsContinuous`，区分 Excel SingleSheet 与 PDF、Canvas、PerPhysicalPage，使同一份文档可以按输出方式调整页尾文字。
- 同步中文、日文、英文 README，更新安装、字体包、单工作表输出、公式及 NuGet/npm 发布说明。

## 升级

将手动拼接明细行号的 `Formula(...)` 改为 `SumBodyColumn(...)`，列索引从 0 开始，放在 Report 页头或页尾。分页小计使用 `PageValue` 提供缓存值。原有手写公式仍按原样输出。

在页尾回调中使用 `page.IsContinuous ? "" : ...` 可隐藏单工作表中的“本页”文字，页尾行本身仍保留。

NuGet 包与 `@printsharp/preview` 版本统一为 0.4.1；本次 npm 发布保持前端 API 不变。完整示例见[集成指南](canvas-preview.zh-CN.md)。
