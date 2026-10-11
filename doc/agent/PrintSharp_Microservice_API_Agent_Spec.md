# PrintSharp 微服务 API — Claude Code Agent 作业依赖书

> 文档目的：指导 Claude Code 在现有 PrintSharp 仓库中实现一个独立的 HTTP 微服务，将 Excel 模板与通用 JSON 数据绑定后输出 PDF 或 Excel。
>
> 实施原则：先检查现有代码和架构，再做最小必要改动；复用 PrintSharp 已有能力，不要重写核心渲染引擎。

## 1. 项目目标

为 PrintSharp 增加一个独立的 ASP.NET Core Web API 服务，使任意语言的客户端都可以通过 HTTP 请求：

1. 上传 Excel 模板（第一版使用 Base64）。
2. 发送字段定义、类型、格式和数据行组成的通用 JSON 数据集。
3. 指定输出格式 `pdf` 或 `xlsx`。
4. 接收生成文件的二进制响应。

目标链路：

```text
HTTP JSON Request
  -> Request validation
  -> Base64 Excel template decoding
  -> Generic JSON data contract
  -> Data adapter (DataSet/DataTable and/or existing binding API)
  -> Existing PrintSharp template binding / renderer
  -> PDF or XLSX bytes
  -> HTTP binary response
```

**不要**把 HTTP、ASP.NET Core 或 JSON 协议依赖引入现有核心类库。微服务应引用 PrintSharp 核心项目，并将协议转换放在独立项目中。

## 2. 开工前必须检查

在修改代码前：

1. 检查仓库目录、解决方案文件（包括 `.slnx`）、所有 `.csproj`、目标框架及现有测试。
2. 阅读 README、架构文档、Excel 模板绑定相关代码和 DataSet/DataTable 支持代码。
3. 找出当前真实的 Excel 模板绑定入口、PDF 输出入口、Excel 输出入口及其参数类型。
4. 运行现有测试，记录基线结果。
5. 根据仓库现状决定 Web API 项目应放置的位置及命名。建议名称 `PrintSharp.Api`，但如果仓库已有命名规则，应遵从现有规则。
6. 不要假设类、方法或命名空间一定存在；使用实际代码中的 API。
7. 不要擅自升级依赖版本、重构核心架构或更改已有公共 API。

若某条输出链路在现有核心中尚未具备可调用入口，应先报告具体缺口，再实现最小必要的适配，不要伪造 API。

## 3. 第一版范围

### 必须实现

- 独立 ASP.NET Core Web API 项目。
- 版本化 API 路由。
- 通用 JSON 数据契约：表定义、列定义、行数据。
- Excel 模板 Base64 解码与基础校验。
- `pdf` 和 `xlsx` 两种输出格式。
- 以二进制文件响应返回结果，不将输出文件再编码成 Base64。
- 输入验证、统一错误响应、日志和健康检查。
- 自动化单元测试及至少一组端到端 API 测试。
- README：如何运行、请求示例、错误说明及限制。

### 第一版不要求

- 模板持久化、`templateId` 注册服务。
- 数据库访问。
- 用户界面。
- Redis、消息队列或后台任务系统。
- 任意客户端指定服务端 .NET 类型并动态实例化。
- 任意文件路径读取。
- 每个单元格独立携带列名、类型和格式的冗余协议。
- 改写 PrintSharp 既有的 PDF、Excel 渲染核心。

## 4. API 设计

### 4.1 路由

建议：

| Method | Route | 功能 |
|---|---|---|
| `POST` | `/api/v1/print/pdf` | 生成 PDF |
| `POST` | `/api/v1/print/excel` | 生成 XLSX |
| `GET` | `/api/v1/health` | 健康检查 |

`/api/v1/print/pdf` 固定输出 PDF，`/api/v1/print/excel` 固定输出 XLSX。可以共用同一个内部请求模型和生成服务，避免重复实现。不要在第一版同时设计多套互相重叠的路由。

### 4.2 请求 DTO 建议

字段名称和实际实现可以依照项目风格调整，但语义应保持稳定。

```csharp
public sealed record PrintRequest(
    TemplateRequest Template,
    PrintDataSet Data);

public sealed record TemplateRequest(
    string ContentBase64,
    string? FileName = null);

public sealed record PrintDataSet(
    IReadOnlyList<PrintDataTable> Tables);

public sealed record PrintDataTable(
    string Name,
    IReadOnlyList<PrintColumn> Columns,
    IReadOnlyList<JsonElement> Rows);

public sealed record PrintColumn(
    string Name,
    string Type,
    string? Format = null,
    bool Nullable = true);
```

以上为设计示例，不要求逐字照抄。可以用 class、record 或适合项目的 DTO 形式。重点是 JSON 契约，不是 DTO 的语法形式。

### 4.3 完整请求示例

```json
{
  "template": {
    "contentBase64": "UEsDBBQAAAAI...",
    "fileName": "invoice-template.xlsx"
  },
  "data": {
    "tables": [
      {
        "name": "Invoice",
        "columns": [
          { "name": "GuestName", "type": "string", "nullable": false },
          { "name": "TotalAmount", "type": "decimal", "format": "#,##0.00", "nullable": false },
          { "name": "CheckIn", "type": "date", "format": "yyyy/MM/dd" }
        ],
        "rows": [
          {
            "GuestName": "山田太郎",
            "TotalAmount": 25000.00,
            "CheckIn": "2026-10-10"
          }
        ]
      },
      {
        "name": "Details",
        "columns": [
          { "name": "Description", "type": "string" },
          { "name": "Quantity", "type": "int", "nullable": false },
          { "name": "UnitPrice", "type": "decimal", "format": "#,##0.00" }
        ],
        "rows": [
          {
            "Description": "宿泊料金",
            "Quantity": 2,
            "UnitPrice": 12500.00
          },
          {
            "Description": "追加料金",
            "Quantity": 1,
            "UnitPrice": 500.00
          }
        ]
      }
    ]
  }
}
```

模板 Base64 内容必须是有效的 `.xlsx` 文件字节。上述短字符串仅作结构示例，不能作为真实模板测试数据。

## 5. 数据契约规范

### 5.1 总体规则

- 一个请求可以包含任意数量的数据表（受服务端限制约束）。
- 每张表有唯一的 `name`、列定义 `columns` 和数据行 `rows`。
- 不同表可以有完全不同的列。
- 列定义只需发送一次；行对象使用列名对应值，避免每个值重复携带 `columnName/type/format`。
- 行对象的键必须与定义的列名匹配（默认严格验证）；未知列和缺失列的处理策略必须有测试并在文档中明确。
- `null` 只允许出现在 `nullable: true` 的列中。
- 空表允许 `rows: []`，但必须仍可由 `columns` 确定表结构。
- 表名和列名不能为空或全空白；同一张表中列名不得重复；表名不得重复。
- 列名、表名应采用稳定的 Ordinal 比较规则，不受当前操作系统区域设置影响。是否区分大小写必须固定并写入文档；推荐大小写敏感，避免跨数据库或跨语言歧义。
- 第一版不自动推断列类型。`type` 是必填项，避免空表或全 `null` 数据造成类型不确定。
- JSON 数值、日期等必须经过受控转换，不得对客户端输入执行任意类型反射或任意对象实例化。

### 5.2 第一版支持的类型

采用稳定、与 .NET 类型名无关的协议名称：

| JSON `type` | 目标 .NET 类型 | 规则 |
|---|---|---|
| `string` | `string` | JSON 字符串 |
| `bool` | `bool` | JSON `true` / `false` |
| `int` | `int` | 32 位整数，拒绝小数及溢出 |
| `long` | `long` | 64 位整数，拒绝小数及溢出 |
| `decimal` | `decimal` | 精确十进制数值，拒绝溢出 |
| `double` | `double` | 浮点数；金额不建议使用 |
| `date` | `DateOnly` 或适合现有绑定能力的日期类型 | 推荐 ISO `yyyy-MM-dd` |
| `datetime` | `DateTimeOffset` 或现有 API 所需的明确类型 | ISO 8601；必须明确时区语义 |
| `guid` | `Guid` | 标准 GUID 字符串 |
| `bytes` | `byte[]` | Base64 字符串，限制解码后长度 |

实现时先确认现有 DataSet/模板绑定对 `DateOnly`、`DateTimeOffset` 等类型的支持。如果现有核心要求 `DateTime`，适配器应显式转换并通过测试，不得默默丢弃时区信息。

可以在后续版本增加 `image` 等专用数据类型，但不要把所有二进制数据都自动解释成图片。图片数据应通过明确约定和大小限制处理。

### 5.3 `format` 语义

- `format` 是可选的显示格式提示，不是数据类型。
- `type` 决定如何解析值；`format` 决定输出时的默认格式。
- 例如 `decimal` + `#,##0.00`，或 `date` + `yyyy/MM/dd`。
- `format` 应传给现有模板绑定/格式化能力；如果当前核心不能应用该格式，必须记录限制并提供明确行为，不得悄悄忽略。
- 模板中已存在的格式、样式和布局应优先保留。`format` 的优先级必须根据当前 PrintSharp 实际实现确定，并通过测试固定下来。
- 不允许客户端通过 `format` 注入表达式、脚本或代码；它只能作为受控的格式字符串使用。

### 5.4 `rows` 的 JSON 表示

建议使用 `IReadOnlyList<JsonElement>` 或等效的 JSON DOM 表示行数据。每行应是对象：

```json
{
  "GuestName": "山田太郎",
  "TotalAmount": 25000.00
}
```

转换时按 `columns` 定义读取值并生成 `DataColumn` / `DataRow`，或映射到现有模板绑定输入。不得仅依赖第一行推断列结构。

## 6. 数据适配层

建议建立独立适配器，例如 `PrintDataSetAdapter`。具体目录和命名以仓库现状为准。

职责：

1. 验证表、列、行的结构。
2. 把协议中的 `type` 映射为允许的 CLR 类型。
3. 创建 `DataSet` 和 `DataTable`，设置表名、列名、数据类型及允许空值属性。
4. 将每个 JSON 值转换为列类型，并填充 `DataRow`。
5. 将 JSON `null` 转换为 `DBNull.Value`。
6. 对无效类型、格式错误、溢出、缺少必需值等返回可定位的验证错误。
7. 不负责 PDF/Excel 渲染。
8. 不负责 HTTP 状态码或 Controller/Endpoint 逻辑。

如果现有 PrintSharp 绑定入口可以直接消费通用数据结构，应优先复用；但第一版可以通过 `DataSet` 作为通用桥梁，因为项目已有 DataSet/DataTable 能力。

避免为每张表生成运行时 C# 类。也不要让客户端传入程序集名、完整类型名或 CLR 类型名称。

### 6.1 AOT 与性能要求

- 使用 `System.Text.Json` 和显式类型映射。
- 避免不必要的反射、`dynamic` 和运行时编译。
- 不要为了通用 JSON 数据契约引入第三方 ORM。
- 在合理范围内避免重复解析同一 JSON 值。
- 不要为了性能过早引入复杂缓存；先保证正确性和基准测试。
- 如果仓库已经有 AOT 兼容性要求，新增 API 项目应遵循已有规则；ASP.NET Core 发布方式应以项目实际支持情况为准，不要声称所有 Web API 功能都天然适合 Native AOT。

## 7. 模板与输出处理

### 7.1 模板处理

- `ContentBase64` 必须非空。
- 使用受控的 Base64 解码；捕获格式错误。
- 解码后验证文件大小上限。
- 检查文件是否为受支持的 Excel 工作簿格式；不能只信任 `FileName` 扩展名。
- 不要将用户提供的 `FileName` 拼接为磁盘路径。
- 不要把用户上传的文件写到任意路径。
- 对压缩文件展开后的大小、工作表数量等资源风险，应尽可能使用现有库的安全检查，并设置合理限制。
- 将解码后的字节以流或现有 API 支持的方式交给模板绑定层，避免不必要的多次复制。

### 7.2 输出处理

PDF 端点：

```http
Content-Type: application/pdf
Content-Disposition: attachment; filename="document.pdf"
```

Excel 端点：

```http
Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
Content-Disposition: attachment; filename="document.xlsx"
```

- 直接返回二进制内容，不将输出再次 Base64 编码。
- 使用安全的默认文件名；对 `FileName` 做净化，防止响应头注入。
- 正确释放流、工作簿、文档及临时资源。
- 每个请求应使用独立的可变渲染对象，不能并发共享可变工作簿或文档。
- 通过现有 PrintSharp API 实现输出，不要在 API 项目中复制一套 PDF/Excel 渲染代码。

## 8. 错误处理

统一采用 `application/problem+json`（或仓库已采用的等价标准错误格式）。不要把堆栈、服务器路径或敏感数据返回给客户端。

建议状态码：

| HTTP | 场景 |
|---|---|
| `400 Bad Request` | JSON 无法解析、请求结构错误、列/行验证失败、Base64 非法 |
| `413 Payload Too Large` | 请求体、模板或数据规模超过限制 |
| `415 Unsupported Media Type` | Content-Type 不支持 |
| `422 Unprocessable Content` | 数据类型转换失败或模板与数据无法绑定（若项目约定统一使用 400，也可以采用 400，但必须一致） |
| `500 Internal Server Error` | 未预期的服务端错误 |

错误应包含稳定的错误代码、可读消息和必要时的 JSON 路径，例如：

```json
{
  "type": "https://example.invalid/problems/invalid-print-data",
  "title": "Invalid print data",
  "status": 400,
  "detail": "A value cannot be converted to the declared column type.",
  "errors": [
    {
      "path": "data.tables[1].rows[0].Quantity",
      "code": "invalid_integer",
      "message": "Expected a 32-bit integer."
    }
  ],
  "traceId": "..."
}
```

以上域名仅为示例；生产环境不要使用虚假的可访问性承诺。不要在响应中回显完整模板或全部业务数据。

## 9. 安全与资源限制

使用配置项集中管理限制，避免散落在代码中。初始值可以先采用保守默认值，再通过配置调整：

- 最大 HTTP 请求体字节数。
- 最大 Base64 模板解码后字节数。
- 最大表数。
- 每张表最大列数。
- 每张表最大行数。
- 单元格字符串最大长度。
- `bytes` 解码后的最大字节数。
- 单请求最大处理时间或可取消操作。
- 最大并发生成数（根据实际渲染器线程安全性与内存使用情况决定）。

注意 Base64 请求体会比原始文件更大，因此请求体上限应高于模板解码后上限，并考虑 JSON 及业务数据的额外体积。

- 支持 `CancellationToken`，在请求取消时尽可能停止后续处理。
- 对上传文件进行格式和资源限制校验。
- 对外部部署应配置认证、授权、HTTPS 和速率限制；不要因为服务当前部署在内网就把它设计成永久无认证。
- 日志记录 trace ID、输出格式、耗时、表数/行数、成功或错误代码；默认不记录姓名、身份证、整份 JSON 或模板内容。
- 不要在日志或错误中输出 Base64 文件内容。

## 10. 健康检查与可观测性

`GET /api/v1/health` 返回简单健康状态，例如：

```json
{
  "status": "ok"
}
```

健康检查不应生成真实报表或返回服务器内部细节。若仓库已有标准健康检查机制，优先复用。

记录必要的结构化日志：

- 请求追踪 ID。
- 输出格式。
- 处理耗时。
- 表数量、总行数（可配置是否记录）。
- 生成结果及稳定错误代码。

不要默认记录实际单元格内容。

## 11. 测试要求

### 11.1 数据契约/适配器单元测试

至少覆盖：

- 单表、多表及不同表具有不同列。
- 空表 `rows: []`，仍保留列结构。
- 空字符串、`null`、`nullable` 规则。
- 每一种受支持的数据类型。
- 无效类型名称。
- 整数、长整数和 decimal 溢出。
- 小数输入到整数列。
- 日期格式错误和日期时间时区处理。
- 缺失列、额外列、重复列名、重复表名。
- 列名大小写策略。
- 非对象行、数组或标量行。
- `format` 为空或非法的处理策略。

### 11.2 API 集成测试

- 无效 JSON 返回 400。
- 无效 Base64 返回 400。
- 超限请求返回 413。
- 不支持的输出格式不会误调用渲染器。
- 合法请求生成 PDF，响应 MIME 正确且文件签名为 `%PDF-`。
- 合法请求生成 XLSX，响应 MIME 正确且文件是有效的 XLSX ZIP 包。
- 渲染失败时返回统一错误，不泄漏堆栈。
- 请求取消和资源释放行为合理。
- 多个并发请求不会互相污染数据或模板。

使用仓库现有测试框架（例如已使用 xUnit 就继续使用 xUnit）。测试应尽可能使用小型、可重复生成或仓库中已有的有效模板 fixture；不要提交包含真实个人信息的模板。

### 11.3 运行验证

完成后至少执行：

1. `dotnet build` 相关解决方案/项目。
2. 全部相关单元测试。
3. API 集成测试。
4. 如仓库有格式检查、分析器或 AOT 检查，执行适用项。

在最终报告中列出实际执行的命令及结果。未执行的测试必须明确说明，不得声称全部通过。

## 12. 建议的代码职责

具体目录结构以仓库现状为准，可参考：

```text
PrintSharp.Api/
  Endpoints/
    PrintEndpoints.cs
    HealthEndpoints.cs
  Contracts/
    PrintRequest.cs
    PrintDataSet.cs
    PrintDataTable.cs
    PrintColumn.cs
  Services/
    IPrintGenerationService.cs
    PrintGenerationService.cs
  Data/
    PrintDataSetAdapter.cs
    PrintDataValidationException.cs
  Configuration/
    PrintApiOptions.cs
  Program.cs
```

职责划分：

- **Contracts**：只定义 HTTP JSON 契约，不包含渲染逻辑。
- **Data adapter**：验证数据并转换为 DataSet/DataTable。
- **Generation service**：调用现有 PrintSharp 模板绑定和输出 API。
- **Endpoints**：绑定请求、调用服务、返回文件和错误状态。
- **Configuration**：管理资源上限及其他可配置行为。

不要为了遵循这份示例而强行拆分过多小类；以可测试、职责清晰为准。

## 13. README 内容

新增或更新文档，至少包括：

1. API 项目用途及与 PrintSharp 核心类库的关系。
2. 启动方式与配置方式。
3. PDF 请求的 `curl` 示例。
4. Excel 请求的 `curl` 示例。
5. JSON 数据契约说明和完整示例。
6. 支持的 `type` 列表与日期/时区规则。
7. 文件响应和错误响应说明。
8. 大小限制、认证及生产环境部署注意事项。
9. 当前不支持的能力及已知限制。

不要把真实 Base64 模板或真实业务个人信息放进 README。可以在文档中说明如何把模板文件编码成 Base64。

## 14. 实施步骤

按以下顺序实施，保持每一步可构建、可测试：

1. 检查仓库与现有实现，先给出简短的实现计划和现有入口清单。
2. 定义 JSON 契约及验证规则。
3. 实现并测试 JSON → DataSet/DataTable 适配器。
4. 新建独立 API 项目并引用正确的 PrintSharp 项目。
5. 实现 PDF、XLSX 两条生成端点。
6. 实现统一错误处理、健康检查、配置化资源限制和结构化日志。
7. 添加集成测试。
8. 更新 README。
9. 运行构建与测试，修复回归。
10. 汇报文件变更、API 示例、测试结果、未完成事项和后续建议。

如果过程中发现现有 PrintSharp API 与本作业书假设不一致，应优先适配现有 API，并说明必要的最小改动。不得为了让示例代码直接编译而破坏现有公共 API。

## 15. 验收标准

只有满足以下条件才算完成：

- [ ] 独立 API 项目可构建，并正确引用 PrintSharp 核心。
- [ ] 请求协议支持任意多张、不同列结构的数据表。
- [ ] 每张表通过 `columns` 声明列名、类型、格式和 nullable。
- [ ] `rows` 数据可以正确转换为现有模板绑定能力可消费的数据结构。
- [ ] 支持合法 Excel 模板 Base64 输入。
- [ ] PDF 端点返回有效 PDF 二进制文件。
- [ ] Excel 端点返回有效 XLSX 二进制文件。
- [ ] 输入验证、错误状态码及错误格式一致。
- [ ] 有文件/请求规模限制，且不允许路径穿越或任意类型实例化。
- [ ] 单元测试与 API 集成测试覆盖主要成功和失败路径。
- [ ] README 文档齐全。
- [ ] 构建和测试结果如实汇报。

## 16. 给 Agent 的最终指令

请直接在当前仓库中实施这项工作，而不只是输出建议或示例代码。先检查现有代码、给出简短计划，然后逐步修改、构建、测试。

优先复用现有 PrintSharp 的 Excel 模板绑定、DataSet/DataTable、PDF 和 Excel 输出能力。所有新增代码都应保持职责清晰、可测试，并避免无必要的第三方依赖。

**本作业书的核心决定：采用“列定义 + 行数据”的通用 JSON 数据契约；列定义是每张表的元数据，行数据只承载值；服务端把该契约转换为现有 PrintSharp 能力可消费的数据结构。**
