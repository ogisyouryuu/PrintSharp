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
| 代码生成宿泊列表 | `Document.Create` / Fluent API，每页 25 行 | 4 |
| Excel 模板套用列表 | `Grid.xlsx`，每页 25 行 | 4 |
| 日本酒店领收书 | `HotelReceipt.xlsx`，每页 15 行 | 7 |
| 日本酒店请求书 | `HotelInvoice.xlsx`，每页 15 行 | 7 |

每个逻辑页对应一个 Excel 工作表、一页 PDF 和一页打印预览。酒店单据演示一张多页单据，包含抬头、编号、宿泊期间、全单金额、明细、每页小计、含税金额、内消费税和支付信息。总金额在各页重复显示，不能将这些总金额再次相加。所有酒店、客户、登记号码和银行信息均为虚构样例。

## Excel 模板

项目的 `Templates` 中包含三个可以直接用 Excel 编辑的实际 `.xlsx` 文件，构建时复制到输出目录。界面“打开模板目录”打开程序正在使用的模板目录。程序仅在模板缺失时重新创建，不覆盖已有模板。要长期保存修改，请将输出目录模板复制回项目 `Templates` 中。

单值使用 `{{Customer}}`、`{{PageLabel}}`、`{{GrandTotal}}` 等占位符；明细行使用 `{{Items.Number}}`、`{{Items.Guest}}`、`{{Items.Room}}`、`{{Items.Date}}`、`{{Items.Nights}}`、`{{Items.UnitPrice}}`、`{{Items.Amount}}`。同一明细行会按当前页的 Items 集合展开。

`ReportService.BuildTemplate` 对每页调用 `ExcelTemplateParser.Instance.Render`，再合并为同一 Document。分页在 Demo 中完成；当前 PrintSharp 不会把一个长工作表自动拆为物理页。编辑模板时请保留占位符，并确保展开后的布局适合 A4。

当前 PDF / Windows 渲染器不会应用 Excel 数字格式。Demo 为日期和金额预先生成显示字符串，保证三种输出一致；这些金额单元格在 Excel 中为文本。总额由 C# 计算，不依赖 Excel 公式。模板集合占位符的现有实现也会转换成字符串。

## 代码入口

- `MainForm.cs`：表格绑定、四种示例选择、文件导出、PrintPreviewControl 和 PrintDocument 生命周期。
- `StayRecord.cs`：100 行数据模型和展示值。
- `ReportService.cs`：代码构建与 Excel 模板绑定、主动分页。
- `TemplateFactory.cs`：缺失模板的生成示例。
- `Program.cs` / `TrueTypeCollection.cs`：日文字体解析和 TTC 到独立 SFNT 的转换，供 PDFsharp 嵌入字体。

## 验证

```powershell
dotnet run --project demos/PrintSharp.WinForms.Demo -- --smoke-test C:/Temp/PrintSharpDemoOutput
dotnet test PrintSharp.slnx
```

冒烟验证实例化主窗体，并对四种示例检查 100 行编号连续且无遗漏、模板占位符全部替换、A4 布局边界、Excel 工作表数、PDF 页数。在可用打印机驱动存在时，还通过 PreviewPrintController 实际生成所有预览页并核对页数，不发送打印任务。输出目录包含 Excel、PDF、首页预览 PNG 和完成标记；无打印机驱动时跳过预览验证。
