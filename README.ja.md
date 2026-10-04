<div align="center">

# PrintSharp

**.NET 向け Grid-first ドキュメント生成ライブラリ**

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[English](README.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md)

</div>

1 つの Excel ライクなグリッドドキュメントから、**Excel**、**PDF**、または **Windows プリンター**へ一貫して出力します。PrintSharp は自由な `X` / `Y` 座標ではなく、行・列・セル結合でレイアウトを保持します。Excel グリッドが常にレイアウトの基準です。

> **ステータス:** 初期段階のライブラリ（`0.1.0`）です。安定版リリースまで、公開 API とレンダリング動作は変更される可能性があります。

## PrintSharp を使う理由

従来の帳票システムは、絶対座標でレイアウトしてから Excel に変換することが少なくありません。わずかな座標差でも余分な Excel 列が作られ、意図したレイアウトが崩れることがあります。

PrintSharp は Excel と互換性のあるグリッドから開始します。

```text
グリッドドキュメント（行 + 列 + スパン + スタイル）
                    │
                    ▼
              レイアウトエンジン
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
      Excel        PDF       Windows
    ClosedXML    PDFsharp  PrintDocument
```

コアモデルはレンダラーに依存しません。論理サイズを出力先固有の物理単位に変換するのは、出力境界のレンダラーだけです。

## 機能

- Grid-first の `Document` / `Page` / `Row` / `Column` / `Cell` モデル
- `A1:C1` のような Excel A1 参照とセル結合
- ドキュメント、行、テーブル、セル、スタイルの Fluent API
- 物理セル境界を共有して算出するレイアウト計算
- ClosedXML による Excel の出力と読み込み
- Excel テンプレートのプレースホルダー束縛と繰り返し行の展開
- PDFsharp による PDF 出力
- Windows `PrintDocument` のレンダリングと印刷
- テキスト、数値、日付、数式、画像バイト配列のセル値
- フォント、色、罫線、余白、配置、折り返し、数値書式

## インストール

アプリケーションで必要な出力先のパッケージを追加します。

```bash
dotnet add package PrintSharp
dotnet add package PrintSharp.Excel
dotnet add package PrintSharp.Pdf
dotnet add package PrintSharp.Windows
```

`PrintSharp.Windows` は Windows を対象とし、Windows Forms / `PrintDocument` を使用します。コア、Excel、PDF プロジェクトは .NET 10 を対象としています。

## クイックスタート

Fluent API で 1 つのドキュメントを作成し、必要な出力先へ保存します。

```csharp
using PrintSharp.Documents;
using PrintSharp.Excel;
using PrintSharp.Fluent;
using PrintSharp.Pdf;

var document = Document.Create(d =>
{
    d.Title("売上レポート")
     .Author("Contoso")
     .Page("集計", p =>
     {
         p.Columns(180f, 70f, 110f)
          .Rows(32f, 24f, 24f, 24f)
          .Cell("A1:C1", c => c.Value("売上レポート")
                              .Font("Yu Gothic", 16f, bold: true)
                              .AlignCenter()
                              .Background("#F3F4F6"))
          .Cell("A2", c => c.Value("商品").Bold())
          .Cell("B2", c => c.Value("数量").Bold().AlignRight())
          .Cell("C2", c => c.Value("金額").Bold().AlignRight())
          .Cell("A3", "ノート")
          .Cell("B3", c => c.Value(2).AlignRight())
          .Cell("C3", c => c.Value(19.98m).Format("¥#,##0.00").AlignRight())
          .Cell("A4", "合計")
          .Cell("C4", c => c.Formula("=SUM(C3:C3)")
                              .Format("¥#,##0.00")
                              .AlignRight());
     });
});

document.SaveAsExcel("sales-report.xlsx");
document.SaveAsPdf("sales-report.pdf");
```

Windows で印刷する場合は、`PrintSharp.Windows` を参照して次を呼び出します。

```csharp
using PrintSharp.Windows;

document.Print();
```

## Excel をテンプレートデザイナーとして使う

既存のワークブックを同じグリッドモデルに解析できます。値には `{{Property}}` を、テンプレート行の繰り返しには `{{Collection.Property}}` を使います。

```csharp
using PrintSharp.Excel;
using PrintSharp.Pdf;

var invoice = new
{
    Customer = new { Name = "Ada Lovelace" },
    InvoiceNo = "INV-001",
    Items = new[]
    {
        new { Name = "ノート", Quantity = 2, Price = 9.99m },
        new { Name = "ペン", Quantity = 3, Price = 1.50m }
    }
};

// invoice-template.xlsx に {{Customer.Name}}、{{InvoiceNo}}、
// {{Items.Name}}、{{Items.Quantity}}、{{Items.Price}} を含むテンプレート行を記述します。
var document = ExcelTemplateParser.Instance.Render("invoice-template.xlsx", invoice);

document.SaveAsExcel("invoice.xlsx");
document.SaveAsPdf("invoice.pdf");
```

## パッケージ

| パッケージ | 目的 | 主な依存関係 |
| --- | --- | --- |
| `PrintSharp` | グリッドドキュメントモデル、スタイル、Fluent API、レイアウトエンジン | — |
| `PrintSharp.Excel` | `.xlsx` レンダラーとテンプレートパーサー | ClosedXML |
| `PrintSharp.Pdf` | PDF レンダラー | PDFsharp |
| `PrintSharp.Windows` | Windows 印刷レンダラー | `PrintDocument` / GDI+ |

## 設計原則

1. **Grid-first レイアウト。** セルは行、列、`RowSpan`、`ColumnSpan` で位置付けます。自由座標は主モデルではありません。
2. **単一のレイアウト計算。** レイアウトエンジンがグリッドから物理境界を計算し、PDF と Windows 出力は同じ幾何情報を共有します。
3. **レンダラー非依存のコア。** コアは論理サイズと `FontSpec` を使用します。Excel 列幅、PDF point、GDI+ 単位、具体的なフォントはコアの外に置きます。
4. **Excel を第一級のデザイン面に。** Excel ワークブックは出力先であると同時に、PDF や印刷の視覚テンプレートにもなります。

詳細なアーキテクチャの背景（中国語）は、[PrintSharp Architecture: Excel Grid-first](src/PrintSharp/PrintSharp_Architecture_ExcelGridFirst_v2_中文版.md) を参照してください。

## 開発

```bash
dotnet test PrintSharp.slnx
```

ソリューションには、コアのグリッド／レイアウト、Fluent API、スタイル、Excel レンダリングとテンプレート、PDF レンダリング、Windows 印刷アダプターの単体テストが含まれます。

## ライセンス

[MIT License](LICENSE) の下で配布されています。
