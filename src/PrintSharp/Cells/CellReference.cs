using System.Text.RegularExpressions;

namespace PrintSharp.Cells;

/// <summary>
/// 提供 Excel A1 单元格命名法与 0 基行/列索引之间的转换工具。
/// </summary>
public static partial class CellReference
{
    private static readonly Regex CellRegex = CreateCellRegex();

    [GeneratedRegex(@"^([A-Za-z]+)(\d+)$")]
    private static partial Regex CreateCellRegex();

    /// <summary>
    /// 将列索引（0-based）转换为 Excel 列字母（例如 0 -> "A", 25 -> "Z", 26 -> "AA"）。
    /// </summary>
    /// <param name="columnIndex">0 基列索引。</param>
    /// <returns>Excel 列字母字符串。</returns>
    public static string ToColumnLetter(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);

        Span<char> buffer = stackalloc char[8];
        int i = buffer.Length;
        int current = columnIndex;

        do
        {
            buffer[--i] = (char)('A' + (current % 26));
            current = (current / 26) - 1;
        } while (current >= 0);

        return new string(buffer[i..]);
    }

    /// <summary>
    /// 将 Excel 列字母（例如 "A", "Z", "AA"）转换为 0 基列索引。
    /// </summary>
    /// <param name="columnLetter">Excel 列字母。</param>
    /// <returns>0 基列索引。</returns>
    public static int ToColumnIndex(string columnLetter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnLetter);

        int index = 0;
        foreach (char c in columnLetter.Trim().ToUpperInvariant())
        {
            if (c is < 'A' or > 'Z')
            {
                throw new ArgumentException($"Invalid column letter character '{c}' in '{columnLetter}'.", nameof(columnLetter));
            }

            index = index * 26 + (c - 'A' + 1);
        }

        return index - 1;
    }

    /// <summary>
    /// 解析单个 A1 格式的单元格引用（例如 "A1" -> (row: 0, col: 0)，"C5" -> (row: 4, col: 2)）。
    /// </summary>
    /// <param name="a1">Excel 单元格地址。</param>
    /// <returns>包含 0 基行索引和列索引的元组。</returns>
    public static (int Row, int Column) ParseCell(string a1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(a1);

        var match = CellRegex.Match(a1.Trim());
        if (!match.Success)
        {
            throw new FormatException($"Invalid A1 cell reference format: '{a1}'.");
        }

        string colPart = match.Groups[1].Value;
        int rowNumber = int.Parse(match.Groups[2].Value);

        if (rowNumber < 1)
        {
            throw new FormatException($"Row number in A1 reference must be >= 1, but got {rowNumber}.");
        }

        int col = ToColumnIndex(colPart);
        int row = rowNumber - 1;
        return (row, col);
    }

    /// <summary>
    /// 解析 A1 格式的单个单元格或区域（例如 "A1" 或 "B2:C3"）。
    /// </summary>
    /// <param name="reference">单元格或区域引用字符串。</param>
    /// <returns>起始行、起始列、跨行数及跨列数。</returns>
    public static (int StartRow, int StartColumn, int RowSpan, int ColumnSpan) ParseRange(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        string trimmed = reference.Trim();
        int colonIndex = trimmed.IndexOf(':');

        if (colonIndex < 0)
        {
            var (row, col) = ParseCell(trimmed);
            return (row, col, 1, 1);
        }

        string firstPart = trimmed[..colonIndex];
        string secondPart = trimmed[(colonIndex + 1)..];

        var (startRow, startCol) = ParseCell(firstPart);
        var (endRow, endCol) = ParseCell(secondPart);

        int minRow = Math.Min(startRow, endRow);
        int maxRow = Math.Max(startRow, endRow);
        int minCol = Math.Min(startCol, endCol);
        int maxCol = Math.Max(startCol, endCol);

        return (minRow, minCol, maxRow - minRow + 1, maxCol - minCol + 1);
    }

    /// <summary>
    /// 将 0 基行和列索引格式化为 A1 单元格名称（例如 (0, 0) -> "A1"）。
    /// </summary>
    public static string FormatCell(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        return $"{ToColumnLetter(column)}{row + 1}";
    }

    /// <summary>
    /// 将 0 基行、列索引及跨度格式化为 A1 区域名称（例如 (1, 1, 2, 2) -> "B2:C3"）。
    /// </summary>
    public static string FormatRange(int startRow, int startColumn, int rowSpan, int columnSpan)
    {
        if (rowSpan <= 1 && columnSpan <= 1)
        {
            return FormatCell(startRow, startColumn);
        }

        string start = FormatCell(startRow, startColumn);
        string end = FormatCell(startRow + rowSpan - 1, startColumn + columnSpan - 1);
        return $"{start}:{end}";
    }
}
