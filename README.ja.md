<div align="center">

# PrintSharp

**.NET 向け Grid-first ドキュメント生成ライブラリ**

[![.NET 11](https://img.shields.io/badge/.NET-11.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PrintSharp](https://img.shields.io/nuget/v/PrintSharp.svg?label=PrintSharp)](https://www.nuget.org/packages/PrintSharp/)
[![PrintSharp.Excel](https://img.shields.io/nuget/v/PrintSharp.Excel.svg?label=PrintSharp.Excel)](https://www.nuget.org/packages/PrintSharp.Excel/)
[![PrintSharp.Pdf](https://img.shields.io/nuget/v/PrintSharp.Pdf.svg?label=PrintSharp.Pdf)](https://www.nuget.org/packages/PrintSharp.Pdf/)
[![PrintSharp.Html](https://img.shields.io/nuget/v/PrintSharp.Html.svg?label=PrintSharp.Html)](https://www.nuget.org/packages/PrintSharp.Html/)
[![PrintSharp.Image](https://img.shields.io/nuget/v/PrintSharp.Image.svg?label=PrintSharp.Image)](https://www.nuget.org/packages/PrintSharp.Image/)
[![PrintSharp.Word](https://img.shields.io/nuget/v/PrintSharp.Word.svg?label=PrintSharp.Word)](https://www.nuget.org/packages/PrintSharp.Word/)
[![PrintSharp.Windows](https://img.shields.io/nuget/v/PrintSharp.Windows.svg?label=PrintSharp.Windows)](https://www.nuget.org/packages/PrintSharp.Windows/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[English](README.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md)

</div>

1 つの Excel ライクなグリッドドキュメントから、**Excel**、**PDF**、**HTML**、**PNG**、**可逆 WebP**、**編集可能な Word（DOCX）**を出力できます。ブラウザーでのプレビューと Windows 印刷も同じグリッドを使用します。

> **ステータス:** 初期段階のライブラリ（`0.5.0`）です。安定版リリースまで、公開 API とレンダリング動作は変更される可能性があります。

HTTP マイクロサービス：[PrintSharp.Api](src/PrintSharp.Api/README.md) は Base64 または multipart で Excel テンプレートを受け取り、型付き JSON テーブルを使って PDF / XLSX ファイルを返します。起動コマンド：`dotnet run --project src/PrintSharp.Api --urls http://127.0.0.1:5080`。

Canvas プレビュー：[プレビュー用パッケージ README](packages/preview/README.md)に TypeScript / Vue / Blazor の API を記載しています。ページごとの JSON Display List と同じ描画計画から PDF を生成します。[ブラウザーデモ](packages/preview/README.md)でページ移動と拡大縮小を確認できます。

## TypeScript・Vue・Blazor のプレビュー

PDF と Canvas は、物理改ページ、折り返し、余白、字形位置を含む不変の描画計画を共有します。Canvas は文字を再組版せず、配置済みの字形輪郭を再生します。アンチエイリアスには差が生じる場合があります。

- **連続スクロール：** 単一ページと連続表示を切り替えられます。表示ページと隣接ページだけを仮想化し、ページキャッシュと画布のピクセル数を制限します。拡大縮小時は閲覧位置と描画済みの画面を維持します。
- **サムネイル：** オプションの仮想化サイドバーでページ移動と現在ページの強調表示ができます。読み込みと描画では本文を優先します。
- **検索：** 必要時に `/text` 索引を取得し、大文字小文字を区別しないリテラル検索、件数表示、前後の結果への移動、文字矩形のハイライトに対応します。検索語は UTF-16 コード単位で 256、結果は 1,000 件が上限です。
- **選択とコピー：** ドラッグして Ctrl+C / Command+C またはコピーボタンを使うと、多言語の論理順で原文をコピーできます。マウスの始点と終点は実際の文字矩形から求めます。デモの選択状態欄は高さを固定し、ドラッグ中に用紙が動かないようにしています。

プレビューは `npm i @printsharp/preview`、Blazor は `dotnet add package PrintSharp.Preview.Blazor` で導入できます。Vue は `@printsharp/preview/vue`、ASP.NET Core ホストは `PrintSharp.Preview.AspNetCore` を使用します。API と配置方法は[パッケージ README](packages/preview/README.md)を参照してください。

API をポート 5080 で起動し、別のターミナルでデモを実行します。

| デモ | 起動・確認手順 | 既定の URL |
| --- | --- | --- |
| Vue 3 + TypeScript | [Vue デモ](demos/PrintSharp.Preview.Vue/README.md) | http://127.0.0.1:5199 |
| Blazor Interactive Server | [Blazor デモ](demos/PrintSharp.Preview.Blazor.Demo/README.md) | http://127.0.0.1:5200 |

両デモには 2 ページのサンプル、Excel アップロード、JSON 編集、同一スナップショットの PDF ダウンロードが含まれます。依存関係と Chromium を導入後、`npm --prefix packages/preview test` でコントロールを検証し、`npm --prefix packages/preview run test:demos` で実際の API と両デモを検証します。マウスのドラッグとクリップボード内容も確認します。既存サーバーの再利用や Blazor のポート変更は各デモの README を参照してください。

選択対象は現在読み込まれているページに限られ、仮想化でページを解放すると選択が消える場合があります。文書全体を対象とした永続的な選択には未対応です。完全にクリップされた原文も検索できますが、表示可能な文字ハイライトや選択矩形はありません。コピーボタンには HTTPS/localhost とブラウザーのクリップボード許可が必要です。デスクトップ Chromium を検証済みで、タッチ選択、Firefox/WebKit、完全な意味構造のアクセシビリティは未検証です。サーバーのスナップショットは現在、単一プロセスのメモリに保存します。

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
    ClosedXML    SkiaSharp  PrintDocument
```

コアモデルはレンダラーに依存しません。論理サイズを出力先固有の物理単位に変換するのは、出力境界のレンダラーだけです。

## 機能

- Grid-first の `Document` / `Page` / `Row` / `Column` / `Cell` モデル
- `A1:C1` のような Excel A1 参照とセル結合
- ドキュメント、行、テーブル、セル、スタイルの Fluent API
- 物理セル境界を共有して算出するレイアウト計算
- ClosedXML による Excel の出力と読み込み
- Excel テンプレートのプレースホルダー束縛と繰り返し行の展開
- SkiaSharp による PDF 出力
- ブラウザーで表示・印刷できる独立した HTML
- ページ単位の PNG と可逆 WebP の出力
- 編集可能な DOCX テーブル
- Windows `PrintDocument` のレンダリングと印刷
- テキスト、数値、日付、数式、画像バイト配列のセル値
- フォント、色、罫線、余白、配置、折り返し、数値書式

## インストール

アプリケーションで必要な出力先のパッケージを追加します。

```bash
dotnet add package PrintSharp
dotnet add package PrintSharp.Excel
dotnet add package PrintSharp.Pdf
dotnet add package PrintSharp.Html
dotnet add package PrintSharp.Image
dotnet add package PrintSharp.Word
dotnet add package PrintSharp.Fonts.NotoSansCjk
dotnet add package PrintSharp.Windows
```

`PrintSharp.Windows` は Windows を対象とし、Windows Forms / `PrintDocument` を使用します。コア、Excel、PDF プロジェクトは .NET 11 を対象としています。

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

`PrintSharp.Html` を参照すると、ブラウザーで表示・印刷できる単独の HTML 文書を出力できます。既定の文字余白は PDF と共通です。

```csharp
using PrintSharp.Html;

string html = document.ToHtml();
document.SaveAsHtml("sales-report.html");
```

`PrintSharp.Image` を参照すると、PDF と同じ描画計画とフォントで、物理ページごとに画像を出力できます。ファイル名には `sales-report-1.webp` のようにページ番号が付きます。WebP は可逆圧縮で、既定の解像度は 144 DPI です。

```csharp
using PrintSharp.Image;

document.SavePageImages("sales-report.png");
document.SavePageImages("sales-report.webp", dpi: 300);
```

`PrintSharp.Word` を参照すると、編集可能な Word の表を出力できます。文字の配置は Word が行い、編集後に改ページ位置が変わる場合があります。

```csharp
using PrintSharp.Word;

document.SaveAsDocx("sales-report.docx");
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
PDF、HTML、PNG、WebP、DOCX、Windows 印刷では、計算済みの物理ページを使用します。Excel では既定で物理ページごとにワークシートを作成します。
元のドキュメントは変更しません。`PageValue` は全ページ確定後に評価されます。
`BodyRowOffset` と `BodyRowCount` はそのページの明細グリッドの行範囲を示し、1 レコード = 1 行の場合は小計の対象レコード範囲として使用できます。

Excel テンプレートは `ExcelTemplateParser.Instance.RenderReport(path, data, settings)` で有効化できます。
各シートに `Items`（または指定したコレクション名）の繰り返し行を一行定義し、前後の行をヘッダーとフッターとして繰り返します。
`PageValue` は単独のプレースホルダー、文章内のプレースホルダー、DataSet の集計テーブル内の値に対応します。

定義済みの行高を使用し、ヘッダーとフッターの高さを先に確保してから明細を行単位で配置します。
縦結合した行は一緒に改ページします。行グループが印刷領域に収まらない場合、結合が領域境界を跨ぐ場合、列幅が印刷領域を超える場合はエラーになります。
任意の用紙には正の幅と高さが必要です。折り返した文字に応じた行高の自動測定は行いません。フッターは明細の直後に配置し、最終ページの行高は引き伸ばしません。

## Excel 帳票出力と数式

`ExcelReportLayout.SingleSheet` は論理 Report ごとに連続したシートを作成し、印刷タイトルと任意のヘッダー固定に対応します。既定の `PerPhysicalPage` は物理ページごとにシートを作成します。

`SumBodyColumn(column, value)` は 0 基準の列番号から出力シートの実際の明細範囲を生成します。帳票のヘッダーまたはフッターに配置してください。空の明細は `=0` となり、キャッシュ値と印刷値も 0 です。ページ小計には `PageValue` でキャッシュを指定します。既存の `Formula(...)` は変更せず、手書きの空範囲や逆順範囲は呼び出し側で処理します。

`PageContext.IsContinuous` は SingleSheet で true、改ページ出力では 1 ページの PDF でも false です。空文字を返すとページ専用の文言を非表示にできます。フッター行自体は残ります。

```csharp
var items = Enumerable.Range(1, 50).ToArray();
var document = Document.Create(d => d.Page(p => p
    .Settings(s => s.PaperKind(PaperKind.A4).Margins(24))
    .Columns(100, 120)
    .Report(r => r
        .Header(h => h.Row(24, row => { row.Cell("No"); row.Cell("Amount"); }))
        .Body(items, 20, (row, item) => { row.Cell(item); row.Cell(item * 1000); })
        .Footer(f => f.Row(24, row =>
        {
            row.Cell(new PageValue(page => page.IsContinuous ? "" : $"Page: {page.BodyRowCount}"));
            row.Cell(c => c.SumBodyColumn(1, value: new PageValue(page =>
                items.Skip(page.BodyRowOffset).Take(page.BodyRowCount).Sum(x => x * 1000)))
                .Format("#,##0"));
        })))));

var excel = document.ToExcelBytes(new ExcelRenderOptions
{
    ReportLayout = ExcelReportLayout.SingleSheet,
    FreezeHeaderRows = true
});
var pdf = document.ToPdfBytes();
```

数式キャッシュは Excel・PDF・Canvas で共有します。キャッシュのない未対応数式は `FormulaWithoutValue` に従います。数値・日付書式には `Document.Culture` を指定します。プレビュー v2 はページ間で字形データを共有し、多言語 UI、PDF ファイル名、サーバーセッションの明示的な解放に対応します。[統合ガイド](doc/canvas-preview.zh-CN.md)を参照してください。

PDF は欧文用の `PrintSharp.Fonts.NotoSans` に依存します。日中韓には `PrintSharp.Fonts.NotoSansCjk`、明朝体には `PrintSharp.Fonts.NotoSerifCjk`、アラビア語には `PrintSharp.Fonts.NotoSansArabic` を追加します。`PrintSharp.Fonts.Noto` は全 4 種を導入します。

## パッケージ

PDF 出力は SkiaSharp、同梱 Noto フォント、HarfBuzz を使用します。公開時は `Fonts/`、`Licenses/`、ネイティブライブラリも配置してください。`SaveAsPdf()` と `ToPdfBytes()` の呼び出し方は維持し、`ToPdfDocument()` は読み取り専用の `RenderedPdfDocument` を返します。

| パッケージ | 目的 | 主な依存関係 |
| --- | --- | --- |
| `PrintSharp` | グリッドドキュメントモデル、スタイル、Fluent API、レイアウトエンジン | — |
| `PrintSharp.Excel` | `.xlsx` レンダラーとテンプレートパーサー | ClosedXML |
| `PrintSharp.Pdf` | PDF レンダラー | SkiaSharp |
| `PrintSharp.Windows` | Windows 印刷レンダラー | `PrintDocument` / GDI+ |
| `PrintSharp.Html` | 単独の HTML 出力 | HTML / CSS |
| `PrintSharp.Image` | PNG と可逆 WebP のページ出力 | SkiaSharp |
| `PrintSharp.Word` | 編集可能な DOCX 出力 | Open XML SDK |
| `PrintSharp.Preview.AspNetCore` | プレビュー HTTP API とセッション | ASP.NET Core |
| `PrintSharp.Preview.Blazor` | Blazor プレビューコンポーネント | Blazor |
| `PrintSharp.Fonts.NotoSans` / `NotoSansCjk` / `NotoSerifCjk` / `NotoSansArabic` | 選択式フォント（共通接頭辞は `PrintSharp.Fonts.`） | Noto fonts |
| `PrintSharp.Fonts.Noto` | 全 4 種のフォントパッケージ | Font packages |

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

ソリューションには、グリッドモデル、レイアウト、Excel テンプレート、PDF、HTML、PNG、WebP、DOCX、Windows 印刷のテストが含まれます。

## ライセンス

[MIT License](LICENSE) の下で配布されています。

## Windows Forms デモ

[WinForms デモ](demos/PrintSharp.WinForms.Demo)には、編集可能な 100 行の DataGridView、コードで定義した帳票と Excel テンプレートを使った帳票、日本のホテルを想定した領収書と請求書、Excel、PDF、HTML、PNG、WebP、DOCX の出力、埋め込み印刷プレビューが含まれます。Windows で次のコマンドを実行します。

```bash
dotnet run --project demos/PrintSharp.WinForms.Demo
```

## NuGet と npm の公開

未使用の `vMAJOR.MINOR.PATCH` タグを push すると、14 個の NuGet パッケージと `@printsharp/preview` を同じバージョンで公開します。NuGet ワークフローはビルド・テスト、パッケージ内容と利用側プロジェクトの検証後、NuGet.org と GitHub Packages に公開します。npm ワークフローはビルドし、provenance 付きで公開します。

```bash
git tag v0.5.0
git push origin v0.5.0
```

両レジストリとも Trusted Publishing を使用します。`NUGET_USER` と NuGet 側の `ogisyouryuu/PrintSharp` / `nuget.yml` ポリシー、npm 側の `npm.yml` trusted publisher を設定してください。ローカル API Key は不要です。公開済みバージョンは再利用しないでください。
