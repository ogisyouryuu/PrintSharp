# Multilingual PDF third-party notices

These texts ship with PrintSharp.Pdf and its consuming applications alongside the font licenses in `Fonts/`.
Preserve them when redistributing a published package. This list records the
PDF integration dependencies, not a complete SBOM for every .NET dependency.

| Component | Pinned version | License | Source |
|---|---|---|---|
| Topten.RichTextKit | 0.6.170 | Apache-2.0 | https://github.com/toptensoftware/RichTextKit/blob/main/license.txt |
| SkiaSharp / native Skia assets | 4.155.0 | MIT plus third-party notices | NuGet package `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt` |
| HarfBuzzSharp / native assets | 14.4.0 | MIT plus third-party notices | NuGet package `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt` |
| HarfBuzz (native shaping engine) | Bundled with HarfBuzzSharp 14.4.0 | Old MIT | `HarfBuzzSharp-THIRD-PARTY-NOTICES.txt` |
| Noto fonts | Unmodified files recorded in `Fonts/SHA256SUMS` | SIL OFL 1.1 | `Fonts/README.md` |

The bundled RichTextKit license notice points to Apache 2.0; its full text is
also included as `Apache-2.0.txt`. Native Skia/HarfBuzz dependencies have their
own notices in the unmodified `*-THIRD-PARTY-NOTICES.txt` files.

The PDF backend is SkiaSharp. No PDFsharp binaries or source are included.
PdfPig 0.1.16 (Apache-2.0) is used only by test projects as an independent
reader and is not a dependency of the production packages.
