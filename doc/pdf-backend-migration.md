# PDF 后端迁移：PDFsharp → SkiaSharp

2026-10-17：生产代码、测试、演示及 NuGet 依赖均已移除 PDFsharp。PrintSharp 保持 MIT，使用 SkiaSharp 直接生成矢量 PDF；多语言使用既有的 Noto / RichTextKit / HarfBuzz 链路。

## 保持的功能

- Core 文档模型、Fluent 构建、Excel 模板绑定、合并单元格、分页、页头页尾和页小计保持原有实现。
- HTTP 路由、JSON / multipart 输入、下载名称、MIME、资源限制和 Excel 输出协议保持不变。
- `SaveAsPdf(path)`、`SaveAsPdf(stream)`、`ToPdfBytes()` 的签名保持不变。调用方提供的流默认保持打开。
- 页面尺寸和方向、缩放、元数据、背景、边框（含粗线、点线、虚线、双线和对角线）、图片及文字仍由共享网格坐标定位。
- 中日韩阿拉伯文字体回退、上下文字形、双向混排、换行、裁剪、粗体、下划线、删除线继续支持。补充了模拟斜体和词间空格两端对齐；显式换行和段落末行不拉伸。
- Windows `PrintDocument` 打印保持独立，不依赖 PDF 后端。

## C# 接口迁移

| 旧接口 / 用法 | 新接口 / 用法 |
|---|---|
| `PdfSharpRenderer.Instance` | `SkiaPdfRenderer.Instance` |
| `ToPdfDocument()` 返回第三方 `PdfDocument` | 返回 `PrintSharp.Pdf.RenderedPdfDocument` |
| `pdf.PageCount`、`pdf.Save(path)` | 保留这些常用调用 |
| `pdf.Pages[i].Width.Point` / `Height.Point` | `pdf.Pages[i].Width` / `Height`，单位直接为 PDF 点 |
| 可修改 PDFsharp 页面或元数据对象 | 只读的已生成结果；修改原始 `Document` 后重新生成 |
| `PdfRenderOptions.TextRenderer` 的页面层接口 | `IPdfTextRenderer.DrawText(SKCanvas, ...)`，直接在当前页绘制 |
| `PrintSharp.Api.NotoFontRegistry` / `NotoPdfTextRenderer` | 移至 `PrintSharp.Pdf` 命名空间 |
| PDFsharp 全局字体注册、WinForms TTC 转换 | 随包 Noto 默认注册；无需全局设置 |

`RenderedPdfDocument` 提供页数、只读页面尺寸和元数据快照、`Save`、`ToArray`、`Dispose`。它不是 PDF 读取器或编辑器，也不提供第三方 `PdfDocument` 的任意编辑能力；这是明确的源码不兼容变更。

```csharp
using PrintSharp.Pdf;

// 元の Document を変更してから、PDF を再生成します。
document.Metadata.Title = "Invoice";
using RenderedPdfDocument pdf = document.ToPdfDocument();
Console.WriteLine($"Pages: {pdf.PageCount}; width: {pdf.Pages[0].Width} pt");
pdf.Save("invoice.pdf");
```

默认字体解析仅使用同梱 Noto。`PdfRenderOptions.FontResolver` 仍支持 Core 逻辑字体名称映射，名称随后经过 Noto 别名和字形覆盖解析。需要其他字体或特殊绘制时实现 `IPdfTextRenderer`；不能继续调用 PDFsharp 的全局字体注册接口。

## 资产、部署和许可

字体和许可声明统一放在 `src/PrintSharp.Pdf/Fonts` / `Licenses`，项目引用及 NuGet 消费方会将它们复制到应用的输出和发布目录。没有在 `PrintSharp.Api` 保存第二套字体。新 NuGet 包随附字体，压缩包约 70 MB。

生产依赖为 SkiaSharp / HarfBuzzSharp（MIT 和原生第三方声明）、HarfBuzz（Old MIT）、RichTextKit（Apache-2.0）、Noto（SIL OFL 1.1），继续保留各许可要求的版权及声明。PdfPig 0.1.16（Apache-2.0）仅用于测试中的独立读取验证，不随 API 发布。已移除仅由旧 PDF 后端引入的 `System.Security.Cryptography.Pkcs` 直接引用。

Linux x64 发布应使用全新目录，避免覆盖旧目录后残留旧程序集：

```powershell
dotnet publish src/PrintSharp.Api -c Release -r linux-x64 --self-contained true -o artifacts/publish/skia-linux-x64
```

Rocky 安装 `fontconfig libicu openssl-libs`，保留完整发布内容，包含 `Fonts/`、`Licenses/` 和 Skia / HarfBuzz 原生库。无需安装系统中日韩/阿拉伯字体。

## 验证与差异

- Windows 全解决方案 191 项测试通过：Core 55、Excel 19、PDF 21、API 90、Windows 6。
- Rocky 9.6 / 10.2 各通过 API 90 项、PDF 21 项，并使用真实 HTTP 上传接口重新生成样例。
- PDF 独立读取检查覆盖元数据、分页、物理尺寸、图片及合并区域缩放、矢量边框、模拟斜体、两端对齐和结果对象的保存/释放。
- WinForms 六种报表冒烟通过，核对明细完整性、页数、分页区域和 Excel / PDF / Windows 预览；PDF 导出不再注册 Windows TTC 字体。
- 新建本地 NuGet 消费程序，仅引用打出的 `PrintSharp.Pdf` 包，在 Windows 和 Rocky 9.6 成功输出混合语言 PDF；验证了字体和原生库的输出及发布复制。
- 发布目录及 `.deps.json` 均无 PDFsharp / PdfPig；源码和项目文件无 PDFsharp 引用。

旧版 `b8923c9` 的两份 Rocky 样例作为对照。新旧均为 1 页、800 × 1146.67 点，PNG 渲染为 1047 × 1500，混合文字、连写和换行的目视检查通过。每通道像素绝对误差均值约 0.558/255，主要反映绘制和字形边缘差异，不表示逐像素一致。文件大小从 326,636 变为 335,516 字节，输出结构由“PDFsharp 页面加 Skia 文字层”改为单一 Skia PDF。

Skia 内部的页面设备使用整数坐标，当前将内部精度设为 0.01 PDF 点，避免默认整点取整改变用纸尺寸。此设置不将文字或边框变成位图；当前渲染器不使用会生成高分辨率中间图像的滤镜，自定义文字渲染器也应避免使用此类滤镜。超出原生坐标范围的页面会报错。

生成文件不保证与旧版逐字节一致。类库中原先使用系统字体 / 基础文字绘制的调用现统一采用 Noto / HarfBuzz，因此字形和换行可能变化；API 原有多语言排版链路继续沿用。复杂文本的复制与提取不保证逐字还原，CJK 日文字形习惯、固定行高、字体覆盖及 ARM64 尚未验证等限制继续适用。结果样例和完整环境记录见 `multilingual-pdf-validation.md`。
