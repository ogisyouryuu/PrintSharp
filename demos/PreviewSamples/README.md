# 共用预览验收样例

`template.xlsx` 包含两张 A4 工作表，分别成为两个预览页。涵盖中日韩阿拉伯文、数据绑定入口、金额格式、合并区域、背景、边框、斜体、下划线和删除线。绑定字段为 `{{T.Value}}`，对应 `data.json`。

文件已经生成并提交，两个 demo 可以直接使用。若需修改模板生成规则，从仓库根目录执行：

```powershell
dotnet run --file demos/PreviewSamples/Generate.cs
```

生成器使用 .NET 11 文件型应用及与仓库相同的 ClosedXML 版本。两页来自两个工作表，用于稳定验证翻页；核心自动分页另由 .NET 测试覆盖。
