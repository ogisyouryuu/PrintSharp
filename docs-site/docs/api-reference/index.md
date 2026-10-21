# API 参考

API Reference 用于快速查找实际 Endpoint、请求/响应结构和错误码。当前 HTTP API 位于 `src/PrintSharp.Api`，详细请求示例、数据类型、资源限制和错误说明见仓库的 [API README](https://github.com/ogisyouryuu/PrintSharp/blob/main/src/PrintSharp.Api/README.md)。

## 当前 Endpoint

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/v1/health` | 健康状态 |
| POST | `/api/v1/print/pdf` | Base64 JSON 模板输入，生成 PDF |
| POST | `/api/v1/print/excel` | Base64 JSON 模板输入，生成 XLSX |
| POST | `/api/v1/print/pdf/upload` | multipart 模板上传，生成 PDF |
| POST | `/api/v1/print/excel/upload` | multipart 模板上传，生成 XLSX |

以上路由由 `src/PrintSharp.Api/Program.cs` 映射。API 项目未配置 OpenAPI/Swagger 生成入口，因此这里不伪造机器生成的模式或交互式 API 页面。

## 后续接入

如需正式的 OpenAPI 参考，应先为真实 Endpoint 增加并验证 OpenAPI 文档生成方式，再将产物接入此分区。请求契约应与 `src/PrintSharp.Api/README.md`、API DTO 和集成测试保持同步。
