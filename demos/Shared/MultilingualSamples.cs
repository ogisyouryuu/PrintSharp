namespace PrintSharp.Demos;

/// <summary>コンソールと WinForms で共通の多言語サンプルを使用します。</summary>
internal static class MultilingualSamples
{
    public static IReadOnlyList<string> Memos { get; } = Array.AsReadOnly<string>(
    [
        "中文：欢迎使用打印服务，预订成功。繁體中文：歡迎光臨，謝謝。",
        "日本語：ご予約ありがとうございます。領収書をご確認ください。",
        "한국어: 예약해 주셔서 감사합니다. 영수증을 확인해 주세요.",
        "العربية: مَرْحَبًا بِكُمْ، شُكْرًا لِحَجْزِكُمْ. رقم الحجز 12345، المبلغ 250.00 USD.",
        "Memo: 中文欢迎 / 日本語ようこそ / 한국어 환영합니다 / العربية مرحبًا بكم / English ABC-123.",
        "中文和العربية مَرْحَبًا、日本語と한국어 혼합. Invoice (INV-2026-001): 123.45 USD.\n第二行：لا إله إلا الله — السَّلَامُ عَلَيْكُمْ (Room 305).",
        "العربية أولاً: الحجز (ABC-123) بقيمة 250.00 USD؛ 中文确认、日本語確認、한국어 확인.",
        string.Concat(Enumerable.Repeat("混合换行：中文、日本語、한국어、العربية مرحبًا بكم، ABC-123. ", 4))
    ]);
}
