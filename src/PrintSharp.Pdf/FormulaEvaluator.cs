using System.Globalization;
using System.Text.RegularExpressions;
using PrintSharp.Cells;
using PrintSharp.Documents;

namespace PrintSharp.Pdf;

internal static partial class FormulaEvaluator
{
    [GeneratedRegex(@"^=?\s*(SUM|AVERAGE|COUNT|MIN|MAX)\s*\(([^()]*)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Aggregate();
    [GeneratedRegex(@"^\$?[A-Za-z]{1,3}\$?[1-9][0-9]{0,6}(?::\$?[A-Za-z]{1,3}\$?[1-9][0-9]{0,6})?$")]
    private static partial Regex Range();

    public static Document Resolve(Document source)
    {
        if (!source.Pages.Any(p => p.Cells.Any(c => c.Type == CellType.Formula && c.Value is not FormulaValue))) return source;
        var result = new Document { Metadata = source.Metadata with { }, Culture = source.Culture };
        result.Pages.Clear();
        foreach (var page in source.Pages)
        {
            var resolved = new Dictionary<Cell, object?>();
            bool Evaluate(Cell cell, HashSet<Cell> visiting, out object? value)
            {
                value = null;
                if (cell.Value is FormulaValue cached) { value = cached.Value; return value is not PageValue; }
                if (cell.Type != CellType.Formula) { value = cell.Value; return value is not PageValue; }
                if (resolved.TryGetValue(cell, out value)) return true;
                if (visiting.Count >= 256 || !visiting.Add(cell)) return false;
                try
                {
                    var match = Aggregate().Match(cell.Value?.ToString() ?? "");
                    if (!match.Success) return false;
                    var numbers = new List<decimal>();
                    foreach (var argument in match.Groups[2].Value.Split(','))
                    {
                        string reference = argument.Trim();
                        if (!Range().IsMatch(reference)) return false;
                        var range = CellReference.ParseRange(reference.Replace("$", ""));
                        if ((long)range.StartRow + range.RowSpan > 1048576 || range.StartColumn + range.ColumnSpan > 16384) return false;
                        foreach (var item in page.Cells.Where(c => c.Row >= range.StartRow && c.Row < (long)range.StartRow + range.RowSpan && c.Column >= range.StartColumn && c.Column < range.StartColumn + range.ColumnSpan))
                        {
                            if (!Evaluate(item, visiting, out var itemValue)) return false;
                            if (itemValue is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
                                numbers.Add(Convert.ToDecimal(itemValue, CultureInfo.InvariantCulture));
                        }
                    }
                    value = match.Groups[1].Value.ToUpperInvariant() switch
                    {
                        "SUM" => numbers.Sum(),
                        "COUNT" => (decimal)numbers.Count,
                        "AVERAGE" when numbers.Count > 0 => numbers.Average(),
                        "MIN" => numbers.Count > 0 ? numbers.Min() : 0m,
                        "MAX" => numbers.Count > 0 ? numbers.Max() : 0m,
                        _ => null
                    };
                    if (value is null) return false;
                    resolved[cell] = value;
                    return true;
                }
                catch (Exception error) when (error is OverflowException or FormatException) { return false; }
                finally { visiting.Remove(cell); }
            }
            var target = new Page(page.Name, page.PageNumber) { Settings = page.Settings, Pagination = page.Pagination };
            foreach (var row in page.Rows) target.Rows.Add(row);
            foreach (var column in page.Columns) target.Columns.Add(column);
            foreach (var cell in page.Cells)
            {
                object? value = cell.Value;
                if (cell.Type == CellType.Formula && value is not FormulaValue && Evaluate(cell, [], out var evaluated))
                    value = new FormulaValue(value?.ToString() ?? "", evaluated);
                target.Cells.Add(new Cell(cell.Row, cell.Column, value, cell.RowSpan, cell.ColumnSpan, cell.Type, cell.Style));
            }
            result.Pages.Add(target);
        }
        return result;
    }
}
