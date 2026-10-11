using System.Text.Json;

namespace PrintSharp.Api;

public sealed record PrintRequest(TemplateRequest Template, PrintDataSet Data);
public sealed record TemplateRequest(string ContentBase64, string? FileName = null);
public sealed record PrintDataSet(IReadOnlyList<PrintDataTable> Tables);
public sealed record PrintDataTable(string Name, IReadOnlyList<PrintColumn> Columns, IReadOnlyList<JsonElement> Rows);
public sealed record PrintColumn(string Name, string Type, string? Format = null, bool Nullable = true);
public sealed record ValidationError(string Path, string Code, string Message);

public sealed class PrintValidationException(string path, string code, string message, int status = 400)
    : Exception(message)
{
    public int Status { get; } = status;
    public ValidationError Error { get; } = new(path, code, message);
}
