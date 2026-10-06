# PrintSharp WinForms Demo

基于 .NET 10 Windows Forms，引用仓库内的 PrintSharp.Excel、PrintSharp.Pdf 和 PrintSharp.Windows。

## 运行

在仓库根目录执行：

```powershell
dotnet run --project demos/PrintSharp.WinForms.Demo
```

也可以在 Visual Studio 中将 `PrintSharp.WinForms.Demo` 设置为启动项目。需要 .NET 10 SDK、Windows，以及可用的 Windows 打印机驱动（例如 Microsoft Print to PDF）。PDF 日文输出使用 Windows Meiryo / Meiryo Bold 字体；缺少时安装日语补充字体。

## 操作

左侧 DataGridView 默认绑定 100 行虚构宿泊数据，可以修改姓名、客室、日期、泊数、单价，金额自动按泊数 × 单价计算。导出和预览读取当前表格的完整数据，包含尚未离开编辑框的有效输入。

选择示例后使用“导出 Excel”“导出 PDF”或“打印（生成预览）”。右侧是 Windows 原生 PrintPreviewControl，预览页码可以切换。打印按钮只生成预览，不发送实际打印任务。修改数据或切换示例后旧预览清除，重新点击按钮即可生成。

| 示例 | 数据构建方式 | 默认页数 |
| --- | --- | --- |
| 代码生成宿泊列表 | `Document.Create` / `Report()`，默认每页 28 行 | 4 |
| Excel 模板套用列表 | `Grid.xlsx`，默认每页 36 行 | 3 |
| 日本酒店领收书 | `HotelReceipt.xlsx`，默认每页 20 行 | 5 |
| 日本酒店请求书 | `HotelInvoice.xlsx`，默认每页 20 行 | 5 |
| DataSet 数据绑定 | `DataSet.xlsx`；`DataSet` 中的 `Report` 与 `Items` 两张表，默认每页 32 行 | 4 |
| 自动分页报表（不同明细行高） | 独立 `AutomaticPaginationReport`；24/36 点明细行高，由库自动换页 | 5 |

每个物理页对应一个 Excel 工作表、一页 PDF 和一页打印预览。酒店单据演示一张多页单据，包含抬头、编号、宿泊期间、全单金额、明细、每页小计、含税金额、内消费税和支付信息。总金额在各页重复显示，不能将这些总金额再次相加。所有酒店、客户、登记号码和银行信息均为虚构样例。

## 自动分页示例

选择“6. 自动分页报表（不同明细行高）”。独立源码 `AutomaticPaginationReport.cs` 使用 `Report().Header().Body().Footer()` 定义一个逻辑报表，无须计算每页容量。

用纸为 A4 纵向，上下左右余白均为 24 点。普通明细为 24 点，住宿 3 泊及以上的明细为 36 点，并在姓名下方显示“連泊プラン”。标题、表头和页尾每页重复，`PageValue` 显示当前页、总页数、本页条数、每页小计和末页说明。

默认 100 条数据自动分为 5 页，分别包含 22、22、21、21、14 条明细。可修改左侧“泊数”，再生成预览，观察明细行高、分页位置和金额变化。行高由示例明确指定；文本折行本身不会自动测量或增加行高。

## Excel 模板

项目的 `Templates` 中包含四个可以直接用 Excel 编辑的实际 `.xlsx` 文件，构建时复制到输出目录。界面“打开模板目录”打开程序正在使用的模板目录。程序仅在模板缺失时重新创建，不覆盖已有模板。要长期保存修改，请将输出目录模板复制回项目 `Templates` 中。

单值使用 `{{Customer}}`、`{{PageLabel}}`、`{{GrandTotal}}` 等占位符；明细行使用 `{{Items.Number}}`、`{{Items.Guest}}`、`{{Items.Room}}`、`{{Items.Date}}`、`{{Items.Nights}}`、`{{Items.UnitPrice}}`、`{{Items.Amount}}`。同一明细行会按当前页的 Items 集合展开。

PrintSharp 库根据 A4 纵向高度自动分页：扣除上下各 24 点余白、标题、表头、小计及其他固定行，再填入完整明细行。整行放不下时才换页，不拉伸行高；末页可以有空白。代码列表使用 24 点明细行高，模板读取实际行高，编辑模板后容量会随之调整。表中的页数针对默认模板与 100 行数据。

代码示例通过 `Report().Header().Body().Footer()` 声明一个逻辑报表；模板示例一次把完整数据传入新增的 `ExcelTemplateParser.Instance.RenderReport`。库在布局和导出时生成物理页，Demo 不再计算容量或调用 `Chunk()`。每个模板工作表须包含一行 `Items` 明细占位符，固定内容与至少一行明细须能放入打印区域。

`Document.PageCount` 是逻辑页数（这些示例均为 1）；`document.CalculateLayout().PageCount` 是物理页数，`document.Paginate()` 可获取物理页文档。`PageValue` 根据最终页码、总页数和明细行范围生成页标签、每页小计及末页说明。原有 `Render()` 和未显式启用分页的页面保持既有行为。

选择“5. DataSet 数据绑定”可查看原生 `DataSet` 用法。示例将汇总数据放在 `Report` 表，明细放在 `Items` 表，并直接把 `DataSet` 传给 `ExcelTemplateParser.RenderReport`。明细的日期、数量和金额列使用 `DateTime`、`int` 和 `decimal`；汇总表中的页标签和小计使用 `object` 列保存 `PageValue`，全体合计保持 `decimal`。模板以 `{{Report.PageLabel}}`、`{{Items.Date}}` 等路径绑定；`Items` 的 DataRow 会自动展开为多行。可在输出目录打开 `DataSet.xlsx` 查看模板。

当前 PDF / Windows 渲染器不会应用 Excel 数字格式。代码和 POCO 模板示例会预先生成日期、金额显示字符串；DataSet 示例则保留 `DateTime`、`int` 和 `decimal` 类型，并通过 Excel 模板格式化日期和金额。PDF / 打印预览仍按当前区域设置显示原始数值。总额由 C# 计算，不依赖 Excel 公式。

## 代码入口

- `MainForm.cs`：表格绑定、六种示例选择、文件导出、PrintPreviewControl 和 PrintDocument 生命周期。
- `StayRecord.cs`：100 行数据模型和展示值。
- `ReportService.cs`：报表区域声明、POCO 与 DataSet 模板绑定、分页后小计。
- `AutomaticPaginationReport.cs`：独立的不同明细行高自动分页示例。
- `TemplateFactory.cs`：缺失模板的生成示例，包含 DataSet 模板。
- `Program.cs` / `TrueTypeCollection.cs`：日文字体解析和 TTC 到独立 SFNT 的转换，供 PDFsharp 嵌入字体。

## 验证

```powershell
dotnet run --project demos/PrintSharp.WinForms.Demo -- --smoke-test C:/Temp/PrintSharpDemoOutput
dotnet test PrintSharp.slnx
```

冒烟验证实例化主窗体，并对六种示例检查 100 行编号连续且无遗漏、模板占位符全部替换、扣除余白后的 A4 布局边界、非末页剩余高度不足以放下下一条明细的实际行高、Excel 工作表数与 PDF 页数一致。新增示例还验证不同明细行高、每页条数、页码和每页小计。在可用打印机驱动存在时，还通过 PreviewPrintController 实际生成所有预览页并核对页数，不发送打印任务。输出目录包含 Excel、PDF、首页预览 PNG 和完成标记；无打印机驱动时跳过预览验证。
