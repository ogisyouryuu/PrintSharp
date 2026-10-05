using System.Globalization;

namespace PrintSharp.WinForms.Demo;

public sealed class StayRecord
{
    public int Number { get; set; }
    public string Guest { get; set; } = "";
    public string Room { get; set; } = "";
    public DateTime Date { get; set; }
    public int Nights { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount => Nights * UnitPrice;

    public static List<StayRecord> CreateSamples() => Enumerable.Range(1, 100).Select(i => new StayRecord
    {
        Number = i,
        Guest = new[] { "田中 太郎", "佐藤 花子", "鈴木 一郎", "高橋 美咲", "山本 健" }[(i - 1) % 5],
        Room = $"{201 + (i - 1) % 20}",
        Date = new DateTime(2026, 10, 1).AddDays((i - 1) % 25),
        Nights = 1 + (i - 1) % 3,
        UnitPrice = 8000 + (i - 1) % 4 * 1500
    }).ToList();

    // 三つのレンダラーで同じ金額・日付を表示するため、表示文字列を用意する。
    public object ToTemplateItem() => new
    {
        Number, Guest, Room,
        Date = Date.ToString("yyyy/MM/dd"), Nights,
        UnitPrice = Yen(UnitPrice), Amount = Yen(Amount)
    };

    public static string Yen(decimal value) => "¥" + value.ToString("N0", CultureInfo.GetCultureInfo("ja-JP"));
}
