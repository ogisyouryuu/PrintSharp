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

## 用紙サイズとページ数

`page.Settings = new PageSettings { PaperKind = PaperKind.A4 }` で用紙サイズを選択できます。
Fluent API では `p.Settings(s => s.PaperKind(PaperKind.A4))` を使用します。
A5、B5、A4、B4、A3、Customer に対応しています。B 系列は ISO 規格の寸法を使用します。
標準用紙の寸法はポイント（1 インチ = 72 ポイント）で表し、横向きのレイアウトでは幅と高さを自動的に入れ替えます。
任意の寸法には `s.Size(300, 400)` を使用します。この呼び出しは Customer を選択します。
既定の Customer は、内容に応じてサイズを調整する従来の動作を維持します。
`page.CurrentPageNumber` は `page.PageNumber` と同期し、`document.PageCount` は現在のページ数／ワークシート数を返します。
この数には、プリンターがワークシートを自動的に分割して生成する追加の印刷ページは含まれません。

## 帳票の自動改ページ

自動改ページは明示的に有効化する追加機能です。既存の `Table()`、テンプレートの `Render()`、手動で作成したページは従来どおり動作します。
用紙、余白、列幅をページに設定し、繰り返すヘッダー、明細、フッターを定義します。

```csharp
using PrintSharp.Documents;
using PrintSharp.Fluent;

int[] items = Enumerable.Range(1, 100).ToArray();
var document = Document.Create(d => d.Page(p => p
    .Settings(s => s.PaperKind(PaperKind.A4).Margins(24))
    .Columns(100)
    .Report(r => r
        .Header(h => h.Row(30, row => row.Cell("番号")))
        .Body(items, 24, (row, item) => row.Cell(item))
        .Footer(f => f.Row(30, row => row.Cell(new PageValue(ctx =>
            $"{ctx.CurrentPageNumber} / {ctx.PageCount}")))))));

int logicalPages = document.PageCount;
int physicalPages = document.CalculateLayout().PageCount;
Document physicalDocument = document.Paginate();
```

各領域の行インデックスは 0 から始まります。`Body(Action<PageBuilder>)` では異なる行高や結合セルも定義できます。
既存のグリッドには `p.Paginate(headerRowCount: 3, footerRowCount: 1)` で自動改ページを追加できます。
PDF、Windows 印刷、Excel 出力時に自動的に改ページし、Excel では物理ページごとにワークシートを作成します。
元のドキュメントは変更しません。`PageValue` は全ページ確定後に評価されます。
`BodyRowOffset` と `BodyRowCount` はそのページの明細グリッドの行範囲を示し、1 レコード = 1 行の場合は小計の対象レコード範囲として使用できます。

Excel テンプレートは `ExcelTemplateParser.Instance.RenderReport(path, data, settings)` で有効化できます。
各シートに `Items`（または指定したコレクション名）の繰り返し行を一行定義し、前後の行をヘッダーとフッターとして繰り返します。
`PageValue` は単独のプレースホルダー、文章内のプレースホルダー、DataSet の集計テーブル内の値に対応します。

定義済みの行高を使用し、ヘッダーとフッターの高さを先に確保してから明細を行単位で配置します。
縦結合した行は一緒に改ページします。行グループが印刷領域に収まらない場合、結合が領域境界を跨ぐ場合、列幅が印刷領域を超える場合はエラーになります。
任意の用紙には正の幅と高さが必要です。折り返した文字に応じた行高の自動測定は行いません。フッターは明細の直後に配置し、最終ページの行高は引き伸ばしません。


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
