using System.Collections;
using System.Data;

namespace PrintSharp.Excel;

/// <summary>
/// DataSet、DataTable、DataRow と Dictionary を使用してテンプレート値を解決します。
/// </summary>
public sealed class DataSetTemplateValueResolver : ITemplateValueResolver, ICompiledTemplateValueResolver
{
    /// <inheritdoc />
    public object? Resolve(object? data, string path)
    {
        if (data is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Resolve(data, path.Split('.'));
    }

    object? ICompiledTemplateValueResolver.Resolve(object? data, TemplateBinding binding) =>
        Resolve(data, binding.Segments);

    bool ICompiledTemplateValueResolver.TryResolveCollection(
        object? data,
        TemplateBinding binding,
        out IEnumerable<object> items)
    {
        var value = Resolve(data, binding.Segments);
        if (TryGetRows(value, out items))
        {
            return true;
        }

        if (value is IEnumerable enumerable and not string &&
            value is not IDictionary<string, object?> and not IDictionary)
        {
            items = Enumerate(enumerable);
            return true;
        }

        items = Array.Empty<object>();
        return false;
    }

    private static object? Resolve(object? data, IReadOnlyList<string> segments)
    {
        object? current = data;
        foreach (var segment in segments)
        {
            current = ResolveSegment(current, segment);
        }

        return current;
    }

    internal static object? ResolveFallback(object? data, TemplateBinding binding)
    {
        if (data is not (DataSet or DataTable or DataRow))
        {
            return null;
        }

        return Resolve(data, binding.Segments);
    }

    internal static object? ResolveSegment(object? data, string segment)
    {
        if (data is DataSet dataSet)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                if (string.Equals(table.TableName, segment, StringComparison.Ordinal))
                {
                    return table;
                }
            }

            return null;
        }

        if (data is DataTable targetTable)
        {
            if (string.Equals(targetTable.TableName, segment, StringComparison.Ordinal))
            {
                return targetTable;
            }

            var column = FindColumn(targetTable, segment);
            return column is not null && targetTable.Rows.Count > 0
                ? Normalize(targetTable.Rows[0][column])
                : null;
        }

        if (data is DataRow row)
        {
            var column = FindColumn(row.Table, segment);
            return column is null ? null : Normalize(row[column]);
        }

        if (data is IDictionary<string, object?> or IDictionary)
        {
            return DictionaryValueResolver.ResolveSegment(data, segment);
        }

        return null;
    }

    internal static bool TryGetRows(object? value, out IEnumerable<object> rows)
    {
        if (value is DataTable table)
        {
            rows = EnumerateRows(table.Rows);
            return true;
        }

        rows = Array.Empty<object>();
        return false;
    }

    private static DataColumn? FindColumn(DataTable table, string name)
    {
        foreach (DataColumn column in table.Columns)
        {
            if (string.Equals(column.ColumnName, name, StringComparison.Ordinal))
            {
                return column;
            }
        }

        return null;
    }

    private static object? Normalize(object? value) => value is DBNull ? null : value;

    private static IEnumerable<object> EnumerateRows(DataRowCollection rows)
    {
        foreach (DataRow row in rows)
        {
            yield return row;
        }
    }

    private static IEnumerable<object> Enumerate(IEnumerable source)
    {
        foreach (var item in source)
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }
}
