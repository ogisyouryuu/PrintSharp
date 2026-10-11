using System.Data;
using System.Globalization;
using System.Text.Json;

namespace PrintSharp.Api;

public sealed class PrintDataSetAdapter(PrintApiOptions limits)
{
    private static readonly Dictionary<string, Type> Types = new(StringComparer.Ordinal)
    {
        ["string"] = typeof(string), ["bool"] = typeof(bool), ["int"] = typeof(int),
        ["long"] = typeof(long), ["decimal"] = typeof(decimal), ["double"] = typeof(double),
        ["date"] = typeof(DateTime), ["datetime"] = typeof(DateTime),
        ["guid"] = typeof(Guid), ["bytes"] = typeof(byte[])
    };

    public DataSet Convert(PrintDataSet? input, CancellationToken cancellationToken = default)
    {
        if (input?.Tables is null) Fail("data.tables", "required", "Tables are required.");
        Limit(input!.Tables.Count, limits.MaxTables, "data.tables");
        var result = new DataSet { CaseSensitive = true, Locale = CultureInfo.InvariantCulture };
        try
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            long cells = 0;
            for (var t = 0; t < input.Tables.Count; t++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = input.Tables[t];
                var path = $"data.tables[{t}]";
                if (source is null) Fail(path, "required", "Table is required.");
                Name(source!.Name, path + ".name");
                if (!names.Add(source.Name)) Fail(path + ".name", "duplicate_table", "Duplicate table name.");
                if (source.Columns is null || source.Rows is null) Fail(path, "required", "Columns and rows are required.");
                Limit(source.Columns!.Count, limits.MaxColumns, path + ".columns");
                Limit(source.Rows!.Count, limits.MaxRows, path + ".rows");
                cells += (long)source.Columns.Count * source.Rows.Count;
                Limit(cells, limits.MaxTotalCells, "data");
                var table = new DataTable(source.Name) { CaseSensitive = true, Locale = CultureInfo.InvariantCulture };
                result.Tables.Add(table);
                var columns = new HashSet<string>(StringComparer.Ordinal);
                for (var c = 0; c < source.Columns.Count; c++)
                {
                    var column = source.Columns[c];
                    var cp = $"{path}.columns[{c}]";
                    if (column is null) Fail(cp, "required", "Column is required.");
                    Name(column!.Name, cp + ".name");
                    if (!columns.Add(column.Name)) Fail(cp + ".name", "duplicate_column", "Duplicate column name.");
                    if (column.Type is null || !Types.TryGetValue(column.Type, out _)) Fail(cp + ".type", "invalid_type", "Unsupported column type.");
                    ValidateFormat(column.Format, cp + ".format");
                    var dc = table.Columns.Add(column.Name, Types[column.Type!]);
                    if (column.Type == "datetime") dc.DateTimeMode = DataSetDateTime.Utc;
                    if (column.Type == "date") dc.DateTimeMode = DataSetDateTime.Unspecified;
                    dc.AllowDBNull = column.Nullable;
                    dc.ExtendedProperties["format"] = column.Format;
                }
                for (var r = 0; r < source.Rows.Count; r++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var row = source.Rows[r];
                    var rp = $"{path}.rows[{r}]";
                    if (row.ValueKind != JsonValueKind.Object) Fail(rp, "invalid_row", "Row must be an object.");
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var property in row.EnumerateObject())
                        if (!columns.Contains(property.Name) || !seen.Add(property.Name))
                            Fail(rp + "." + property.Name, "unknown_or_duplicate_column", "Unknown or duplicate row property.");
                    var values = new object[source.Columns.Count];
                    for (var c = 0; c < source.Columns.Count; c++)
                    {
                        var column = source.Columns[c];
                        var vp = rp + "." + column.Name;
                        if (!row.TryGetProperty(column.Name, out var value)) Fail(vp, "missing_column", "Every declared column must be present.");
                        if (value.ValueKind == JsonValueKind.Null)
                        {
                            if (!column.Nullable) Fail(vp, "null_not_allowed", "Null is not allowed.");
                            values[c] = DBNull.Value;
                        }
                        else values[c] = Parse(value, column.Type, vp);
                    }
                    table.Rows.Add(values);
                }
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private object Parse(JsonElement v, string type, string path)
    {
        try
        {
            switch (type)
            {
                case "string":
                    var text = v.GetString()!;
                    Limit(text.Length, limits.MaxStringLength, path);
                    return text;
                case "bool": return v.GetBoolean();
                case "int": return v.GetInt32();
                case "long": return v.GetInt64();
                case "decimal": return v.GetDecimal();
                case "double":
                    var number = v.GetDouble();
                    if (!double.IsFinite(number)) throw new FormatException();
                    return number;
                case "date": return DateTime.ParseExact(v.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None);
                case "datetime":
                    var date = v.GetString()!;
                    // Require an explicit offset or Z; normalize to UTC for the existing renderer.
                    if (!System.Text.RegularExpressions.Regex.IsMatch(date, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$")) throw new FormatException();
                    return DateTimeOffset.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.None).UtcDateTime;
                case "guid": return Guid.ParseExact(v.GetString()!, "D");
                case "bytes":
                    var encoded = v.GetString()!;
                    Limit(encoded.Length, ((long)limits.MaxBytesLength + 2) / 3 * 4, path);
                    var bytes = System.Convert.FromBase64String(encoded);
                    Limit(bytes.Length, limits.MaxBytesLength, path);
                    return bytes;
                default: throw new FormatException();
            }
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or OverflowException or ArgumentException)
        { throw new PrintValidationException(path, "invalid_" + type, "Value does not match the declared type."); }
    }

    public static void ValidateFormat(string? format, string path)
    {
        if (format is null) return;
        if (string.IsNullOrWhiteSpace(format) || format.Length > 128 ||
            format.Any(c => !"#0.,%+- /:;()yyyyMMddHhmsStTaApP\\\"'@".Contains(c)))
            Fail(path, "invalid_format", "Use a nonempty basic Excel number/date format (maximum 128 characters).");
    }
    private void Name(string? name, string path)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('.') || name.Contains('{') || name.Contains('}'))
            Fail(path, "invalid_name", "Name must be nonblank and cannot contain dots or braces.");
        Limit(name!.Length, Math.Min(limits.MaxStringLength, 128), path);
    }
    public static void Limit(long actual, long max, string path)
    { if (actual > max) throw new PrintValidationException(path, "limit_exceeded", "Configured resource limit exceeded.", 413); }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void Fail(string path, string code, string message) => throw new PrintValidationException(path, code, message);
}
