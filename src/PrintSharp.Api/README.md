# PrintSharp.Api

独立的 .NET 10 ASP.NET Core HTTP 服务。协议、校验和资源限制都放在本项目中；引用现有 PrintSharp.Excel / PrintSharp.Pdf，不改动核心公共 API。

## 运行

Visual Studio 中将 `PrintSharp.Api` 设为启动项目，选择 `PrintSharp.Api` 启动配置，然后按 F5。浏览器默认打开 `/api/v1/health`，应显示 `{"status":"ok"}`。当前 VS 配置为 `https://localhost:14880` 和 `http://localhost:14881`；若开发证书未配置，可访问 `http://localhost:14881/api/v1/health`。根路径 `/` 没有页面，返回 404 不表示服务启动失败。生成文件需按下方示例发送 POST JSON 请求。

在仓库根目录执行：

```powershell
dotnet run --project src/PrintSharp.Api --urls http://127.0.0.1:5080
```

配置见 `appsettings.json` 的 `PrintApi` 节。环境变量可覆盖，例如 `PrintApi__MaxConcurrency=1`、`PrintApi__PdfFontPath=C:\fonts\unicode.ttf`。字体路径只能由服务端配置，不能由请求指定。Windows 默认读取 Arial TTF；Linux 部署必须配置可用 TTF。当前 PDF 使用该字体替代模板字体，粗体/斜体由 PDFsharp 模拟；中文、日文需要覆盖相应字符的字体及合法授权。

## 路由与请求

| 方法 | 路径 | 返回 |
|---|---|---|
| POST | `/api/v1/print/pdf` | `application/pdf`，附件 `document.pdf` |
| POST | `/api/v1/print/excel` | XLSX MIME，附件 `document.xlsx` |
| POST | `/api/v1/print/pdf/upload` | 文件上传，返回 PDF 附件 |
| POST | `/api/v1/print/excel/upload` | 文件上传，返回 XLSX 附件 |
| GET | `/api/v1/health` | `{"status":"ok"}` |

原有 `/pdf` 和 `/excel` 接口继续使用下方 Base64 JSON 请求，兼容现有客户端。新增 `/pdf/upload` 和 `/excel/upload` 接口使用 multipart 文件上传。所有生成接口直接返回二进制，不返回 Base64。

### 文件上传接口

Content-Type 为 `multipart/form-data`，只接受两个部分，名称区分大小写、不能重复、顺序不限：

- `template`：一个非空 XLSX 文件，必须为带文件名的文件部分。实际工作簿内容校验与 Base64 接口相同，不信任扩展名或 MIME，上传文件名不用于服务器路径或响应文件名。
- `data`：JSON 文本或 JSON 文件，内容是原有请求的 `data` 对象，即 `{"tables":[...]}`，不包含外层 `template` / `data` 包装。

例如保存如下内容为 `data.json`，Excel 模板中使用 `{{Invoice.TotalAmount}}`：

```json
{
  "tables": [{
    "name": "Invoice",
    "columns": [{ "name": "TotalAmount", "type": "decimal", "format": "#,##0.00", "nullable": false }],
    "rows": [{ "TotalAmount": 25000.00 }]
  }]
}
```

```powershell
curl.exe --fail-with-body http://localhost:14881/api/v1/print/pdf/upload -F "template=@invoice.xlsx" -F "data=<data.json" --output document.pdf
curl.exe --fail-with-body http://localhost:14881/api/v1/print/excel/upload -F "template=@invoice.xlsx" -F "data=<data.json" --output document.xlsx
```

不要手动设置 multipart Content-Type，客户端会自动生成 boundary。Postman 选择 Body → form-data，`template` 设置为 File，`data` 设置为 Text 并填写 JSON。也可用 `-F "data=@data.json;type=application/json"` 上传 JSON 文件。

接收层使用 MultipartReader 逐段读取，按实际字节限制整个请求和模板，支持无 Content-Length 的请求，不经过 Base64 编解码，也不写临时文件。模板和 JSON 数据仍在受限内存中缓冲，现有渲染器需要完整工作簿；这不是端到端零缓冲渲染。沿用请求 20 MiB、模板 5 MiB 等现有配置。

缺失部分、空模板、重复/未知部分或 multipart 格式错误返回 400；非 multipart Content-Type 返回 415；资源超限返回 413，错误响应与原接口一致。手动 multipart 解析未使用 ASP.NET Core 的 IFormFile 自动表单绑定；当前不使用 Cookie 身份认证，认证/CSRF 策略仍由部署配置负责，若以后启用 Cookie 认证，应同时增加相应防伪保护。

### Base64 JSON 接口

模板应包含 `{{Invoice.GuestName}}`、`{{Invoice.TotalAmount}}`、`{{Invoice.CheckIn}}` 和明细行 `{{Details.Description}}`、`{{Details.Quantity}}`。现有核心会按照表的行数展开占位符所在行；同一行应只包含同一表的重复字段，避免把单行汇总表和明细表混在一行。单行表用于头部值，空表保留结构。所有占位符必须引用已声明的 `Table.Column`。

完整 JSON 结构（`REPLACE_WITH_BASE64` 必须替换成真实 XLSX 的 Base64）：

```json
{
  "template": { "contentBase64": "REPLACE_WITH_BASE64", "fileName": "invoice.xlsx" },
  "data": {
    "tables": [
      {
        "name": "Invoice",
        "columns": [
          { "name": "GuestName", "type": "string", "nullable": false },
          { "name": "TotalAmount", "type": "decimal", "format": "#,##0.00", "nullable": false },
          { "name": "CheckIn", "type": "date", "format": "yyyy/MM/dd" }
        ],
        "rows": [{ "GuestName": "Example", "TotalAmount": 25000.00, "CheckIn": "2026-10-10" }]
      },
      {
        "name": "Details",
        "columns": [
          { "name": "Description", "type": "string" },
          { "name": "Quantity", "type": "int", "nullable": false }
        ],
        "rows": [{ "Description": "Room", "Quantity": 2 }, { "Description": "Breakfast", "Quantity": 1 }]
      }
    ]
  }
}
```

将上述结构保存为 `request.json` 后，通过 PowerShell 填入真实模板：

```powershell
$request = Get-Content request.json -Raw | ConvertFrom-Json
$request.template.contentBase64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path invoice.xlsx)))
$request | ConvertTo-Json -Depth 20 | Set-Content request.json -Encoding utf8NoBOM
curl.exe --fail-with-body -X POST http://127.0.0.1:5080/api/v1/print/pdf -H "Content-Type: application/json" --data-binary "@request.json" --output document.pdf
curl.exe --fail-with-body -X POST http://127.0.0.1:5080/api/v1/print/excel -H "Content-Type: application/json" --data-binary "@request.json" --output document.xlsx
```

## C# HttpClient 客户端示例

以下代码可放入 .NET 10 控制台项目的 `Program.cs`，不需要引用 PrintSharp 服务端项目或安装额外 NuGet 包。准备 `invoice.xlsx`，其中一个单元格填写 `{{Invoice.TotalAmount}}`；文件路径相对于客户端运行目录。

示例分别调用文件上传和原有 Base64 接口生成 PDF、Excel。业务数据结构相同，上传时 `data` 部分只发送数据集 JSON，Base64 时则包装成 `template` + `data` 请求。

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

// 应用中复用 HttpClient；ASP.NET Core 客户端可通过 IHttpClientFactory 获取。
using var http = new HttpClient
{
    BaseAddress = new Uri("http://localhost:14881/"),
    Timeout = TimeSpan.FromSeconds(90)
};

var data = new
{
    tables = new[]
    {
        new
        {
            name = "Invoice",
            columns = new[]
            {
                new
                {
                    name = "TotalAmount", type = "decimal",
                    format = "#,##0.00", nullable = false
                }
            },
            rows = new[] { new { TotalAmount = 25000.00m } }
        }
    }
};

// 此 token 同时控制请求及响应文件复制；也可传入 UI/宿主提供的取消 token。
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var token = cancellation.Token;

// 新增文件上传接口。
await UploadAsync(http, "api/v1/print/pdf/upload", "invoice.xlsx", data, "upload.pdf", token);
await UploadAsync(http, "api/v1/print/excel/upload", "invoice.xlsx", data, "upload.xlsx", token);

// 原有 Base64 JSON 接口。
await Base64Async(http, "api/v1/print/pdf", "invoice.xlsx", data, "base64.pdf", token);
await Base64Async(http, "api/v1/print/excel", "invoice.xlsx", data, "base64.xlsx", token);

static async Task UploadAsync(
    HttpClient http, string route, string templatePath, object data,
    string outputPath, CancellationToken token)
{
    using var form = new MultipartFormDataContent();
    // form 释放时会一并释放文件内容及其底层流。
    var file = new StreamContent(File.OpenRead(templatePath));
    file.Headers.ContentType = new MediaTypeHeaderValue(
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    form.Add(file, "template", Path.GetFileName(templatePath));
    form.Add(new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json"), "data");

    // 不手动设置 multipart Content-Type，MultipartFormDataContent 会生成 boundary。
    using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = form };
    await SendAndSaveAsync(http, request, outputPath, token);
}

static async Task Base64Async(
    HttpClient http, string route, string templatePath, object data,
    string outputPath, CancellationToken token)
{
    var bytes = await File.ReadAllBytesAsync(templatePath, token);
    var payload = new
    {
        template = new
        {
            contentBase64 = Convert.ToBase64String(bytes),
            fileName = Path.GetFileName(templatePath)
        },
        data
    };
    using var request = new HttpRequestMessage(HttpMethod.Post, route)
    {
        Content = JsonContent.Create(payload)
    };
    await SendAndSaveAsync(http, request, outputPath, token);
}

static async Task SendAndSaveAsync(
    HttpClient http, HttpRequestMessage request, string outputPath,
    CancellationToken token)
{
    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
    if (!response.IsSuccessStatusCode)
    {
        // 保留 problem+json 中的 errors/path/code 和 traceId，方便定位请求错误。
        var problem = await response.Content.ReadAsStringAsync(token);
        throw new HttpRequestException(
            $"PrintSharp API returned HTTP {(int)response.StatusCode}: {problem}",
            inner: null, statusCode: response.StatusCode);
    }

    // 只有成功后才创建文件，避免把错误 JSON 保存成 PDF/XLSX。
    // outputPath 由客户端自行指定；File.Create 会覆盖同名文件。
    await using var output = File.Create(outputPath);
    await response.Content.CopyToAsync(output, token);
    Console.WriteLine($"Saved: {Path.GetFullPath(outputPath)}");
}
```

若按命令行示例启动服务，将 `BaseAddress` 改为 `http://127.0.0.1:5080/`。仅需要一种输入方式时，保留对应调用即可。接口失败抛出 `HttpRequestException`；取消或超时抛出 `OperationCanceledException`。保存过程中取消可能留下不完整的输出文件，客户端应按业务需要清理或采用临时文件保存后再重命名。

## 数据规则

表名、列名以 Ordinal 大小写敏感方式比较，不允许同名重复；不能空白，不能含 `.` 或花括号，最大 128 字符。列结构不从第一行推断，允许 `rows: []`。每行必须是对象，所有列都必须出现；即使 nullable 为 true，缺失列也返回 400。多余键、重复键、大小写不匹配均返回 400。`nullable` 默认 true，JSON null 转成 DBNull。

| type | 输入与适配 |
|---|---|
| string | JSON 字符串；允许空字符串 |
| bool | JSON true/false |
| int / long | JSON 32/64 位整数，拒绝小数及溢出 |
| decimal | JSON decimal 范围内数值；JSON 到 DataSet 保留 decimal 精度 |
| double | 有限 JSON 浮点数 |
| date | 严格 `yyyy-MM-dd`，适配为无时区 DateTime |
| datetime | `yyyy-MM-ddTHH:mm:ss[.fffffff]Z` 或显式 `±HH:mm`；适配为 UTC DateTime，Excel 不保存原始 offset |
| guid | 标准带连字符 GUID；绑定时转为文本 |
| bytes | Base64 字符串；适配为 byte[]，绑定时输出 Base64 文本，不自动转图片 |

`type` 区分大小写，不接受 CLR 类型名称。Excel 数值存储有约 15 位有效数字限制，极大 long/decimal 在 XLSX/PDF 输出时可能损失精度；如需完整显示标识符，应声明 string。

`format` 缺省为 null。空白、超过 128 字符和超出基本数字/日期格式字符集合的字符串拒绝。支持 `#,##0.00`、`0.00%`、`yyyy/MM/dd` 等，不支持条件格式、颜色、货币符号、脚本或表达式。模板中已有自定义或内置格式优先；只有纯 `{{Table.Column}}` 单元格且模板为 General 时才应用列 format，混合文本占位符不应用列 format。XLSX 保留值类型；PDF 通过现有 Excel 渲染器取得格式化文本，使用 invariant culture，再调用现有 PDF 输出。

## 错误与日志

错误统一 `application/problem+json`，含 `status`、`title`、`detail`、`errors: [{path, code, message}]` 和 `traceId`。不回显模板、业务行或堆栈。

| HTTP | 场景/示例 code |
|---|---|
| 400 | 非法 JSON、Base64、结构、类型、模板或绑定：`invalid_json`、`invalid_base64`、`invalid_int`、`invalid_binding` |
| 413 | 请求/模板/数据/展开规模超限：`limit_exceeded` |
| 415 | 非 JSON Content-Type：`unsupported_media_type` |
| 500 | 意外渲染失败：`generation_failed` |
| 504 | 处理超时：`cancelled` |

所有输入转换错误统一使用 400，不使用 422。客户端断开时不尝试写错误正文。日志记录 traceId、路由、耗时、状态和稳定错误码；成功时记录格式、表数和行数，不记录值或 Base64。

## 资源与部署

默认：请求 20 MiB，模板 5 MiB，ZIP 实际解压总量 40 MiB，2000 ZIP 项，20 工作表，模板网格总面积 100000，预计生成网格面积 500000，20 表、每表 100 列/10000 行、总数据单元格 200000、字符串 32767 字符、bytes 64 KiB、并发生成 2、处理超时 60 秒。上传不写磁盘，忽略 fileName，下载使用固定文件名。校验 XLSX content types、工作簿项、重复 ZIP 项及稀疏单元格坐标；拒绝宏工作簿。

超时和取消是阶段间及数据/ZIP 遍历中的协作取消。现有同步核心的单次解析/渲染无法强制中断；正在执行时保留并发槽位，完成后检查取消并丢弃结果。需要硬性执行期限时应增加独立工作进程隔离。

默认用于本机开发；对外发布前必须由网关或宿主配置认证/授权、HTTPS、速率限制及网络访问策略。当前服务没有内置身份认证，不应直接暴露公网。首次启动的字体配置固定于进程生命周期，修改后重启。

当前不支持模板注册/持久化、数据库、上传路径、图片协议、任意 CLR 类型、Native AOT 发布保证。只保留现有核心支持的 Excel 样式/布局；宏、复杂 Excel 图表及原始 PDF 字体还原不在第一版范围。PDF 中公式不负责计算，按现有核心行为输出公式文本；不要依赖此服务计算上传公式。

## 验证

```powershell
dotnet build PrintSharp.slnx --nologo
dotnet test PrintSharp.slnx --nologo
dotnet run --project tests/PrintSharp.Excel.AotSmoke
```

API 单元测试和 WebApplicationFactory HTTP 集成测试位于 `tests/PrintSharp.Api.Tests`，模板在测试中生成，不包含个人信息。
