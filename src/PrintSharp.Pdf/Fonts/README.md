# Bundled Noto fonts

The eight unmodified font files were copied on 2026-10-17 from
`C:/Src/UniGridPro/UniGridPro.Report/Fonts/`. `SHA256SUMS` records their hashes.
These are application assets, not downloads performed at runtime.

| Family | Files | Upstream |
|---|---|---|
| Noto Sans | `NotoSans-{Regular,Bold}.ttf` | https://github.com/notofonts/noto-fonts |
| Noto Sans Arabic | `NotoSansArabic-{Regular,Bold}.ttf` | https://github.com/notofonts/noto-fonts |
| Noto Sans CJK JP | `NotoSansCJKjp-{Regular,Bold}.otf` | https://github.com/notofonts/noto-cjk/tree/main/Sans |
| Noto Serif CJK JP | `NotoSerifCJKjp-{Regular,Bold}.otf` | https://github.com/notofonts/noto-cjk/tree/main/Serif |

All fonts use SIL Open Font License 1.1. Retain `OFL-Noto.txt`, `OFL-CJK.txt`
and the copyright notices inside each font when redistributing these assets.
The CJK JP files cover common simplified/traditional Chinese, Japanese,
Korean and Latin characters using Japanese regional glyph variants.
They do not cover every Unicode character or emoji.

License sources:

- https://github.com/notofonts/noto-fonts/blob/main/LICENSE
- https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE
- https://github.com/notofonts/noto-cjk/blob/main/Serif/LICENSE

Validate on Linux with `sha256sum -c SHA256SUMS` in this directory.
