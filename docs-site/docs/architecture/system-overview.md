# 系统概览

本页描述当前解决方案的项目引用和可验证输出路径。它不把历史架构文档中的建议当作现行实现。

## 项目边界

```text
PrintSharp.Api ──> PrintSharp.Excel ──> PrintSharp Core
              └─> PrintSharp.Pdf   ──> PrintSharp Core

PrintSharp.Windows ─────────────────> PrintSharp Core
```

该关系依据各 `.csproj` 的 `ProjectReference`。Excel、PDF 和 Windows 是独立输出适配器；API 组合 Excel 与 PDF，不引用 Windows 打印项目。

## 请求与输出

API `Program.cs` 映射健康检查 `GET /api/v1/health`，以及 PDF/Excel 的 Base64 JSON 和 multipart 上传 POST 路由。请求交由 API 生成服务处理；API 引用对应的 Excel、PDF 项目。路由和请求格式见[API 参考](/api-reference/)及现有 [API 使用说明](https://github.com/ogisyouryuu/PrintSharp/blob/main/src/PrintSharp.Api/README.md)。

```text
HTTP 客户端 --模板与数据--> PrintSharp.Api
                              ├──> Excel 输出 --XLSX--> 客户端
                              └──> PDF 输出   --PDF--> 客户端
```

上图仅概括当前 HTTP 输出边界；具体请求限制和失败响应以 API README 与 `appsettings.json` 为准。

## 架构文档状态

`src/PrintSharp/PrintSharp_Architecture_ExcelGridFirst_v2_中文版.md` 保留了较早的设计构想，其中部分段落提及已替换的 PDFsharp。当前 PDF 后端迁移事实记录于 `doc/pdf-backend-migration.md`。更改架构说明前应以当前项目引用和实现代码复核，避免将未来目标或过时示意写成现状。
