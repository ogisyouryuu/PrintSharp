# PrintSharp.Api

独立的 .NET 10 ASP.NET Core HTTP 服务。协议、校验和资源限制都放在本项目中；引用现有 PrintSharp.Excel / PrintSharp.Pdf，不改动核心公共 API。

## 运行

Visual Studio 中将 `PrintSharp.Api` 设为启动项目，选择 `PrintSharp.Api` 启动配置，然后按 F5。浏览器默认打开 `/api/v1/health`，应显示 `{"status":"ok"}`。当前 VS 配置为 `https://localhost:14880` 和 `http://localhost:14881`；若开发证书未配置，可访问 `http://localhost:14881/api/v1/health`。根路径 `/` 没有页面，返回 404 不表示服务启动失败。生成文件需按下方示例发送 POST JSON 请求。

在仓库根目录执行：

```powershell
dotnet run --project src/PrintSharp.Api --urls http://127.0.0.1:5080
```

配置见 `appsettings.json` 的 `PrintApi` 节。环境变量可覆盖，例如 `PrintApi__MaxConcurrency=1`。PDF 默认使用随服务发布的 `Fonts/` 中的 Noto 字体，不需要安装 Windows 字体或单独配置字体路径。旧的 `PrintApi__PdfFontPath` 配置已移除；迁移时删除该环境变量。字体文件名区分大小写，缺失或损坏时启动失败。

## Linux 多语言 PDF

启动时 `NotoFontRegistry` 按 `AppContext.BaseDirectory/Fonts` 注册字体，沿用 UniGridPro.Report 的“随应用携带字体、一次加载、使用逻辑字体名”方式。字体文件直接复制自该项目。`NotoSansCJKjp` / `NotoSerifCJKjp` 提供中日韩字符和拉丁文，`NotoSansArabic` 提供阿拉伯文，`NotoSans` 作为额外回退；每组包含 Regular / Bold。

单个单元格（例如 `{{Details.Memo}}`）按 Unicode 书写素的实际字形覆盖拆分字体段，不拆开基础字符和组合标记。RichTextKit / HarfBuzz 处理阿拉伯文上下文字形、双向文本、数字以及换行。`PrintSharp.Pdf` 使用 SkiaSharp 直接生成整份 PDF，绘制页面、背景、边框、图片和文字，不再引用 PDFsharp。Noto 注册、字体资产和许可声明均位于 `src/PrintSharp.Pdf`，类库与 API 共用实现。CJK OTF/CFF 在此路径中以 Type 3 矢量字形写入，阿拉伯文字体以 TrueType 嵌入；接收端无需安装字体，不会把整页变成位图。

模板中的 `NotoSansCJKjp`、`Noto Sans CJK JP` 以及未知/系统字体统一映射到 Noto Sans CJK JP。`NotoSerifCJKjp`、`MS Mincho`、`Yu Mincho`、`ＭＳ 明朝`、`游明朝`、`Times New Roman` 映射到 Noto Serif CJK JP。`NotoSansArabic` 和 `NotoSans` 可显式指定。默认 CJK 使用日文字形习惯，支持简繁中文及韩文字符，但不按语言自动切换地区字形。粗体使用真实 Bold 文件；斜体使用字形倾斜模拟，无单独的 Italic 文件。WrapText 下支持词间空格两端对齐，显式换行及段落末行不拉伸。

混合语言示例：`Memo: 中文欢迎 / 日本語ようこそ / 한국어 환영합니다 / العربية مرحبًا بكم / English ABC-123.`，无需客户端拆分字段或设置字体段。纯阿拉伯段落的 General 对齐按首个强方向字符自动右对齐；显式 Left / Center / Right 保留模板对齐。

固定行高不自动扩展；请为 memo 设置 WrapText 和足够的行高。超出的文字裁剪在单元格范围内。字体覆盖之外的字符会明确报错，不会静默替换成方框；本方案不承诺 emoji 或全部 Unicode 字符。此版本优先保证打印，复杂文本的搜索、复制、提取顺序可能因查看器而异，不保证原始字符串逐字往返。

### 混合语言报表代码演示

完整可编译项目见 `demos/PrintSharp.Multilingual.Demo/Program.cs`：它用 ClosedXML 创建含 `{{Details.Memo}}` 的模板，将八条中日韩阿拉伯样例（含混排、音标、括号数字和长文本）送入与 API 相同的生成服务，也支持向真实 HTTP 接口发送请求。

```powershell
# API と同じ処理をプロセス内で実行します。
dotnet run --project demos/PrintSharp.Multilingual.Demo -- output/pdf/local

# 起動済み API に multipart で送信します。
dotnet run --project demos/PrintSharp.Multilingual.Demo -- output/pdf/http http://127.0.0.1:5080/
```

输出包含 `multilingual.pdf`、可复用的 `template.xlsx`、`data.json` 及环境记录。其他项目引用 `PrintSharp.Pdf` 后可以直接调用 `document.SaveAsPdf(...)` / `document.ToPdfBytes()`，默认启用同一套 Noto 多语言排版。通过 `PdfRenderOptions.TextRenderer` 可替换文字绘制；接口直接接收 Skia 的画布，不再包含 PDFsharp 类型。类库接口迁移说明见 `doc/pdf-backend-migration.md`。

### Rocky Linux 9.6 / 10 部署

在 Windows 或 Linux 发布自包含的 x64 包：

```powershell
dotnet publish src/PrintSharp.Api -c Release -r linux-x64 --self-contained true -o artifacts/publish/skia-linux-x64
```

发布到全新目录，再将整个发布目录（包含 `Fonts/`、`Licenses/`、`libSkiaSharp.so` 和 `libHarfBuzzSharp.so`）复制到 VPS，例如 `/opt/printsharp`。不能只复制 DLL，也不要将新包覆盖在旧部署目录后留下旧程序集。

```bash
sudo dnf install -y fontconfig libicu openssl-libs
cd /opt/printsharp
chmod +x PrintSharp.Api
./PrintSharp.Api --urls http://127.0.0.1:5080
```

自包含包携带 .NET Runtime，系统仍需上述原生依赖。此命令不安装中日韩/阿拉伯系统字体。以普通服务账号运行，通过现有反向代理提供外部访问。ARM64 VPS 需改为 `linux-arm64` 并在目标架构另行验证。

当前生产环境为 `23.238.1.37`（AlmaLinux 9.6 x64），由 `printsharp-api.service` 以 `printsharp` 系统账号运行，Nginx 将 HTTPS 请求代理到 `127.0.0.1:5080`。生产 API 地址为 `https://printsharp.ginbow.eu.org`。从仓库根目录运行 `eng/deploy.ps1 -Target api` 可自动构建、上传版本目录、切换当前版本、重启服务并检查 `/api/v1/health`。TLS 证书覆盖 API 与文档子域名，并由 Certbot 自动续期。

Nginx 已限制 API 请求速率和请求体大小。API 暂无内置身份认证，生成接口可被公网访问；若业务数据只应由受限用户提交，应增加 API Key 或其他身份认证。

仓库根目录提供 `bash eng/verify-multilingual-pdf.sh`，用于在装有 .NET 10 SDK、上述运行依赖和 `poppler-utils` 的 Rocky 主机上发布、启动本机 API、发送真实请求及检查 PDF 字体/图像。验证记录见 `doc/multilingual-pdf-validation.md`。

### 免费商用与闭源许可

生产项目及测试项目均已移除 PDFsharp；PDF 写入由 SkiaSharp 完成。用于独立读取验证的 PdfPig（Apache-2.0）仅为测试依赖，不随 API 发布。

新增文字链路采用 RichTextKit 0.4.167（Apache-2.0）、SkiaSharp / HarfBuzzSharp（MIT，原生组件另附第三方声明）、HarfBuzz（Old MIT）、Noto（SIL OFL 1.1）。这些许可允许免费商业和闭源使用，但分发时仍需保留相应版权、许可及第三方声明；OFL 不要求嵌入字体的报表使用 OFL。发布目录保留 `Fonts/OFL-*.txt` 和 `Licenses/`。详见其中的来源清单与原始许可文本。

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

默认用于本机开发；对外发布前必须由网关或宿主配置认证/授权、HTTPS、速率限制及网络访问策略。当前服务没有内置身份认证，不应直接暴露公网。字体固定于进程生命周期，替换字体文件后重启。

当前不支持模板注册/持久化、数据库、上传路径、图片协议、任意 CLR 类型、Native AOT 发布保证。只保留现有核心支持的 Excel 样式/布局；宏、复杂 Excel 图表及原始 PDF 字体还原不在第一版范围。PDF 中公式不负责计算，按现有核心行为输出公式文本；不要依赖此服务计算上传公式。

## 验证

```powershell
dotnet build PrintSharp.slnx --nologo
dotnet test PrintSharp.slnx --nologo
dotnet run --project tests/PrintSharp.Excel.AotSmoke
```

API 单元测试和 WebApplicationFactory HTTP 集成测试位于 `tests/PrintSharp.Api.Tests`，模板在测试中生成，不包含个人信息。
