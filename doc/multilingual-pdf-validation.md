# 多语言 PDF 验证记录

验证日期：2026-10-17。目标：.NET 10、Rocky Linux、PrintSharp.Api 的真实 HTTP PDF 上传接口。

## 环境

| 项目 | 环境 |
|---|---|
| Windows | .NET SDK 10.0.402 / Runtime 10.0.13，x64 |
| 目标版本 | WSL2 `RockyLinux-9.6`，Rocky Linux 9.6 (Blue Onyx)，glibc 2.34，x64 |
| 最新版本 | WSL2 `RockyLinux-10`，Rocky Linux 10.2 (Red Quartz)，x64 |
| 发布方式 | Release、linux-x64、自包含 |
| 文字链路 | Noto → RichTextKit 0.4.167 → HarfBuzzSharp 7.3.0.3 / SkiaSharp 2.88.9 → PDFsharp 6.2.4 |

两个 WSL 均使用 Rocky 官方 WSL 镜像独立导入。镜像下载后核对官方 SHA-256：

| 镜像 | SHA-256 |
|---|---|
| Rocky 9.6 WSL | `9ce7566838dbd4166942d8976c328e03ec05370d9f04006ec4327b61bf04131a` |
| Rocky 10.2 WSL | `9494fe2249a4c5a6629c847a603c0bbc222da5e70906e53de737a3022a794b30` |

9.6 的软件源固定到官方 vault/9.6，安装验证依赖时没有升级到更新的 Rocky 9 小版本。安装了 fontconfig、libicu、openssl-libs、poppler-utils 和 .NET 10 SDK。未安装系统 CJK / 阿拉伯字体；报表只从发布目录读取 Noto 文件。

官方镜像来源：

- https://dl.rockylinux.org/vault/rocky/9.6/images/x86_64/
- https://dl.rockylinux.org/pub/rocky/10/images/x86_64/

## 结果

| 检查 | Windows | Rocky 9.6 | Rocky 10.2 |
|---|---|---|---|
| API 测试，包含多语言和并发 | 90/90 | 90/90 | 90/90 |
| PDF 库测试 | 8/8 | 8/8 | 8/8 |
| Excel 测试 | 19/19 | — | — |
| Core 测试 | 55/55 | — | — |
| 真实 HTTP 上传并生成混合语言 PDF | — | 通过 | 通过 |
| Poppler 字体检查 | — | 字形全部嵌入 | 字形全部嵌入 |
| Poppler 图像检查 | — | 0 个位图图像 | 0 个位图图像 |
| PNG 目视检查 | — | 通过 | 通过 |

Windows 整个 `PrintSharp.slnx` 构建成功，0 警告、0 错误。
`dotnet list src/PrintSharp.Api package --include-transitive --vulnerable` 未报告当前 NuGet 源中的已知漏洞。

新增测试不仅检查 PDF 文件头：还检查混合 memo 的字体回退覆盖、组合标记、RTL / LTR 排版段、上下文字形替换、换行、无缺失字形、字体缺失时启动失败、未知字形明确失败，以及并发真实 HTTP 请求返回包含字体和 Unicode 映射的矢量 PDF。

两个环境均执行：

```bash
bash eng/verify-multilingual-pdf.sh output/pdf/rocky-verification
```

该脚本从源码发布 API 和演示程序，启动本机服务，调用 `/api/v1/print/pdf/upload`，再执行 `pdffonts`、`pdfimages`、`pdfinfo`、`pdftotext` 和 `pdftoppm`。脚本结束会停止它启动的 API 进程。

## 样例

- `output/pdf/rocky-9.6/multilingual.pdf`
- `output/pdf/rocky-10.2/multilingual.pdf`

各目录同时保存 XLSX 模板、JSON 数据、环境记录、字体/图像检查结果以及 PNG 预览。样例包含简体/繁体中文、日本语、韩语、阿拉伯语、拉丁文；同一 memo 中混排五种语言，并覆盖阿拉伯音标、数字、括号、显式换行和长文本自动换行。标题使用真实 Bold 字体，内容使用 Regular 字体。

## 适用范围

验证环境为真实 Rocky 用户空间和 WSL2 Linux 内核，尚未连接用户的 VPS；x64 结果不能替代 ARM64 的原生依赖验证。PDFsharp 固定 6.2.4（MIT），避免 6.5.0 的 AGPL / 商业许可证加载要求；相关许可文本随发布包提供。

这次验收针对显示与打印。当前 CJK CFF 字体由 Skia 以 Type 3 矢量字形输出，阿拉伯文使用嵌入的 TrueType；复杂文本在不同阅读器中复制或提取时可能产生字符归一化、重复字形映射或方向差异，不保证原始字符串无损往返。未承诺全部 Unicode、emoji、地区字形自动切换、斜体字形或超出固定行高后的自动增高。
