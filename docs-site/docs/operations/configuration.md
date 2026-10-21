# 配置

当前 API 的默认限制定义在 `src/PrintSharp.Api/appsettings.json` 的 `PrintApi` 节。ASP.NET Core 配置可使用双下划线环境变量覆盖层级配置，例如 `PrintApi__MaxConcurrency=1`；不要把真实密钥或生产连接字符串写入仓库。

## 已提交的默认限制

| 配置 | 默认值 | 用途 |
| --- | ---: | --- |
| `MaxRequestBytes` | 20 MiB | 请求体上限 |
| `MaxTemplateBytes` | 5 MiB | 模板解码后上限 |
| `MaxExpandedBytes` | 40 MiB | ZIP 解压数据上限 |
| `MaxZipEntries` | 2000 | XLSX ZIP 项目数上限 |
| `MaxWorksheets` | 20 | 工作表上限 |
| `MaxTemplateCells` | 100000 | 模板网格上限 |
| `MaxGeneratedCells` | 500000 | 生成网格上限 |
| `MaxTables` / `MaxColumns` / `MaxRows` | 20 / 100 / 10000 | 数据表、列和行上限 |
| `MaxTotalCells` | 200000 | 数据单元格总数上限 |
| `MaxStringLength` / `MaxBytesLength` | 32767 / 65536 | 字符串和二进制值长度上限 |
| `MaxConcurrency` | 2 | 并发生成数 |
| `ProcessingTimeoutSeconds` | 60 | 处理超时 |

这些是仓库当前默认值，不代表适合所有负载。调整前需评估内存和服务容量，并在部署环境的安全配置渠道中设置。PDF 默认字体来自随包 Noto 文件；字体部署信息见[部署](/operations/deployment)。
