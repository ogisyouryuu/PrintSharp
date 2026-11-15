using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using PrintSharp.Cells;
using PrintSharp.Documents;

namespace PrintSharp.Excel;

// ClosedXML の公開 API で設定できない数式キャッシュを Open XML に書き込みます。
internal static class FormulaCache
{
    public static void Write(Stream stream, Document document)
    {
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        for (int i = 0; i < document.Pages.Count; i++)
        {
            var values = document.Pages[i].Cells.Where(c => c.Type == CellType.Formula && c.Value is FormulaValue)
                .ToDictionary(c => CellReference.FormatCell(c.Row, c.Column), c => ((FormulaValue)c.Value!).Value);
            if (values.Count == 0) continue;
            string path = $"xl/worksheets/sheet{i + 1}.xml";
            var entry = archive.GetEntry(path)!;
            XDocument xml;
            using (var input = entry.Open()) xml = XDocument.Load(input);
            foreach (var cell in xml.Descendants(ns + "c"))
            {
                if (!values.TryGetValue((string?)cell.Attribute("r") ?? "", out var value)) continue;
                var (type, text) = Serialize(value);
                cell.SetAttributeValue("t", type);
                cell.Elements(ns + "v").Remove();
                cell.Elements(ns + "is").Remove();
                cell.Add(new XElement(ns + "v", text));
            }
            entry.Delete();
            using var output = archive.CreateEntry(path).Open();
            xml.Save(output);
        }
    }
    private static (string Type, string Text) Serialize(object? value) => value switch
    {
        null => ("str", ""),
        string text => ("str", text),
        bool boolean => ("b", boolean ? "1" : "0"),
        DateOnly date => ("n", date.ToDateTime(TimeOnly.MinValue).ToOADate().ToString(CultureInfo.InvariantCulture)),
        DateTime date => ("n", date.ToOADate().ToString(CultureInfo.InvariantCulture)),
        TimeOnly time => ("n", time.ToTimeSpan().TotalDays.ToString(CultureInfo.InvariantCulture)),
        TimeSpan time => ("n", time.TotalDays.ToString(CultureInfo.InvariantCulture)),
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            => ("n", Convert.ToString(value, CultureInfo.InvariantCulture)!),
        _ => throw new ArgumentException($"Unsupported formula cache type: {value.GetType().Name}")
    };
}
