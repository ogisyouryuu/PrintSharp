# Multilingual PDF third-party notices

These texts ship with the service alongside the font licenses in `Fonts/`.
Preserve them when redistributing a published package. This list records the
PDF integration dependencies, not a complete SBOM for every .NET dependency.

| Component | Pinned version | License | Source |
|---|---|---|---|
| PDFsharp | 6.2.4 | MIT | https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE |
| Topten.RichTextKit | 0.4.167 | Apache-2.0 | https://github.com/toptensoftware/RichTextKit/blob/ae434c82d8a197e3d259761879fb3f862f12efd8/license.txt |
| SkiaSharp / native Skia assets | 2.88.9 | MIT plus third-party notices | NuGet package `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt` |
| HarfBuzzSharp / native assets | 7.3.0.3 | MIT plus third-party notices | NuGet package `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt` |
| HarfBuzz (native shaping engine) | 7.3.0 | Old MIT | https://github.com/harfbuzz/harfbuzz/blob/7.3.0/COPYING |
| Noto fonts | Unmodified files recorded in `Fonts/SHA256SUMS` | SIL OFL 1.1 | `Fonts/README.md` |

The bundled RichTextKit license notice points to Apache 2.0; its full text is
also included as `Apache-2.0.txt`. Native Skia/HarfBuzz dependencies have their
own notices in the unmodified `*-THIRD-PARTY-NOTICES.txt` files.

PDFsharp 6.5.0 and newer use a different licensing model. Do not update the
version assuming that it is still MIT licensed.
