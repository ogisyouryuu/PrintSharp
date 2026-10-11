using System.Data;
using System.IO.Compression;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using PrintSharp.Excel;
using PrintSharp.Pdf;

namespace PrintSharp.Api;

public interface IPrintGenerationService
{
    Task<byte[]> GenerateAsync(PrintRequest request, bool pdf, CancellationToken cancellationToken);
    Task<byte[]> GenerateAsync(byte[] template, PrintDataSet data, bool pdf, CancellationToken cancellationToken);
}

public sealed class PrintGenerationService : IPrintGenerationService, IDisposable
{
    private readonly PrintApiOptions _limits;
    private readonly SemaphoreSlim _slots;
    public PrintGenerationService(IOptions<PrintApiOptions> options)
    {
        _limits = options.Value;
        _slots = new(_limits.MaxConcurrency);
    }

    public Task<byte[]> GenerateAsync(PrintRequest request, bool pdf, CancellationToken cancellationToken) =>
        RunAsync(token => Generate(request, pdf, token), cancellationToken);

    public Task<byte[]> GenerateAsync(byte[] template, PrintDataSet data, bool pdf, CancellationToken cancellationToken) =>
        RunAsync(token =>
        {
            using var converted = new PrintDataSetAdapter(_limits).Convert(data, token);
            return Generate(converted, template, pdf, token);
        }, cancellationToken);

    private async Task<byte[]> RunAsync(Func<CancellationToken, byte[]> generate, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_limits.ProcessingTimeoutSeconds));
        await _slots.WaitAsync(timeout.Token);
        try
        {
            // Synchronous renderers do not support interruption. Keep the slot until they finish.
            return await Task.Run(() => generate(timeout.Token), CancellationToken.None);
        }
        finally { _slots.Release(); }
    }

    private byte[] Generate(PrintRequest request, bool pdf, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var data = new PrintDataSetAdapter(_limits).Convert(request.Data, token);
        var encoded = request.Template?.ContentBase64;
        if (string.IsNullOrWhiteSpace(encoded)) PrintDataSetAdapter.Fail("template.contentBase64", "required", "Template content is required.");
        PrintDataSetAdapter.Limit(encoded!.Length, ((long)_limits.MaxTemplateBytes + 2) / 3 * 4, "template.contentBase64");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new PrintValidationException("template.contentBase64", "invalid_base64", "Template must be valid Base64."); }
        return Generate(data, bytes, pdf, token);
    }

    private byte[] Generate(DataSet data, byte[] bytes, bool pdf, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        PrintDataSetAdapter.Limit(bytes.Length, _limits.MaxTemplateBytes, "template");
        ValidateArchive(bytes, token);
        using var input = new MemoryStream(bytes, writable: false);
        using var prepared = new MemoryStream();
        try
        {
            using var workbook = new XLWorkbook(input);
            PrintDataSetAdapter.Limit(workbook.Worksheets.Count, _limits.MaxWorksheets, "template");
            long cellCount = 0;
            long generatedCells = 0;
            foreach (var sheet in workbook.Worksheets)
            {
                token.ThrowIfCancellationRequested();
                // Bound the used rectangle before the core parser traverses it.
                var range = sheet.RangeUsed();
                if (range is not null) cellCount += (long)range.LastRow().RowNumber() * range.LastColumn().ColumnNumber();
                PrintDataSetAdapter.Limit(cellCount, _limits.MaxTemplateCells, "template");
                if (range is not null)
                {
                    long expandedRows = range.LastRow().RowNumber();
                    foreach (var row in sheet.RowsUsed())
                    {
                        var rowTables = row.CellsUsed().Where(c => c.DataType == XLDataType.Text)
                            .SelectMany(c => Regex.Matches(c.GetString(), @"\{\{([^}]+)\}\}", RegexOptions.None, TimeSpan.FromSeconds(1)).Cast<Match>())
                            .Select(m => m.Groups[1].Value.Trim().Split('.')[0]);
                        var repeat = rowTables.Select(name => data.Tables.Cast<DataTable>().FirstOrDefault(t => t.TableName == name)?.Rows.Count ?? 0).DefaultIfEmpty(0).Max();
                        expandedRows += Math.Max(0, repeat - 1);
                    }
                    generatedCells += expandedRows * range.LastColumn().ColumnNumber();
                    PrintDataSetAdapter.Limit(generatedCells, _limits.MaxGeneratedCells, "template");
                }
                foreach (var cell in sheet.CellsUsed())
                {
                    if (cell.DataType != XLDataType.Text) continue;
                    var text = cell.GetString();
                    foreach (Match match in Regex.Matches(text, @"\{\{([^}]+)\}\}", RegexOptions.None, TimeSpan.FromSeconds(1)))
                    {
                        var parts = match.Groups[1].Value.Trim().Split('.');
                        var table = data.Tables.Cast<DataTable>().FirstOrDefault(t => t.TableName == parts[0]);
                        var column = parts.Length == 2 ? table?.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName == parts[1]) : null;
                        if (column is null) throw new PrintValidationException("template", "invalid_binding", "Template placeholder must reference a declared Table.Column.");
                        if (text == match.Value && cell.Style.NumberFormat.NumberFormatId == 0 &&
                            string.IsNullOrEmpty(cell.Style.NumberFormat.Format) && column.ExtendedProperties["format"] is string format)
                            cell.Style.NumberFormat.Format = format;
                    }
                }
            }
            workbook.SaveAs(prepared);
        }
        catch (PrintValidationException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or XmlException or InvalidOperationException)
        { throw new PrintValidationException("template", "invalid_template", "Template is not a supported XLSX workbook."); }
        token.ThrowIfCancellationRequested();
        // byte[] is deliberately rendered as text rather than being treated as an image.
        using var bindingData = new DataSet { CaseSensitive = true, Locale = CultureInfo.InvariantCulture };
        foreach (DataTable table in data.Tables)
        {
            var target = new DataTable(table.TableName);
            bindingData.Tables.Add(target);
            foreach (DataColumn column in table.Columns)
            {
                var bindingColumn = target.Columns.Add(column.ColumnName, column.DataType == typeof(byte[]) || column.DataType == typeof(Guid) ? typeof(string) : column.DataType);
                if (column.DataType == typeof(DateTime)) bindingColumn.DateTimeMode = column.DateTimeMode;
            }
            foreach (DataRow row in table.Rows)
                target.Rows.Add(row.ItemArray.Select(v => v is byte[] b ? Convert.ToBase64String(b) : v is Guid g ? g.ToString("D") : v).ToArray());
        }
        prepared.Position = 0;
        var document = ExcelTemplate<DataSet>.Load(prepared).Render(bindingData);
        token.ThrowIfCancellationRequested();
        byte[] output;
        if (pdf)
        {
            // PDF core draws values as text and does not apply Excel number formats.
            // Ask the existing Excel renderer to format cells, then parse that styled workbook.
            using var formatted = document.ToExcelWorkbook();
            foreach (var sheet in formatted.Worksheets)
                foreach (var cell in sheet.CellsUsed())
                    if (!cell.HasFormula && cell.DataType != XLDataType.Blank)
                        cell.Value = cell.GetFormattedString(CultureInfo.InvariantCulture);
            using var stream = new MemoryStream();
            formatted.SaveAs(stream);
            stream.Position = 0;
            token.ThrowIfCancellationRequested();
            output = ExcelTemplateParser.Instance.Parse(stream).ToPdfBytes();
        }
        else output = document.ToExcelBytes();
        token.ThrowIfCancellationRequested();
        return output;
    }

    private void ValidateArchive(byte[] bytes, CancellationToken token)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            PrintDataSetAdapter.Limit(archive.Entries.Count, _limits.MaxZipEntries, "template");
            long expanded = 0;
            var names = new HashSet<string>(StringComparer.Ordinal);
            var buffer = new byte[8192];
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (!names.Add(entry.FullName)) throw new InvalidDataException();
                PrintDataSetAdapter.Limit(entry.Length, _limits.MaxExpandedBytes, "template");
                using var content = entry.Open();
                int count;
                while ((count = content.Read(buffer)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    expanded += count;
                    PrintDataSetAdapter.Limit(expanded, _limits.MaxExpandedBytes, "template");
                }
            }
            var contentTypes = archive.GetEntry("[Content_Types].xml") ?? throw new InvalidDataException();
            if (archive.GetEntry("xl/workbook.xml") is null || archive.GetEntry("xl/vbaProject.bin") is not null) throw new InvalidDataException();
            using var xmlStream = contentTypes.Open();
            using var reader = XmlReader.Create(xmlStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = _limits.MaxExpandedBytes });
            var xml = XDocument.Load(reader);
            var workbookType = xml.Descendants().FirstOrDefault(e => (string?)e.Attribute("PartName") == "/xl/workbook.xml")?.Attribute("ContentType")?.Value
                ?? xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "Default" && (string?)e.Attribute("Extension") == "xml")?.Attribute("ContentType")?.Value;
            if (workbookType != "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml") throw new InvalidDataException();
            // Check worksheet count and sparse cell coordinates before ClosedXML allocates cells.
            var worksheets = archive.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToArray();
            PrintDataSetAdapter.Limit(worksheets.Length, _limits.MaxWorksheets, "template");
            long totalRectangle = 0;
            foreach (var worksheet in worksheets)
            {
                using var sheetStream = worksheet.Open();
                using var sheetReader = XmlReader.Create(sheetStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = _limits.MaxExpandedBytes });
                long maxRow = 0, maxColumn = 0, countCells = 0;
                while (sheetReader.Read())
                {
                    token.ThrowIfCancellationRequested();
                    if (sheetReader.NodeType != XmlNodeType.Element || sheetReader.LocalName != "c") continue;
                    PrintDataSetAdapter.Limit(++countCells, _limits.MaxTemplateCells, "template");
                    var reference = sheetReader.GetAttribute("r");
                    if (reference is null) throw new InvalidDataException();
                    var match = Regex.Match(reference, @"^([A-Z]{1,3})([1-9][0-9]{0,6})$", RegexOptions.None, TimeSpan.FromSeconds(1));
                    if (!match.Success) throw new InvalidDataException();
                    long column = 0;
                    foreach (var letter in match.Groups[1].Value) column = column * 26 + letter - 'A' + 1;
                    maxColumn = Math.Max(maxColumn, column);
                    maxRow = Math.Max(maxRow, long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
                    PrintDataSetAdapter.Limit(maxColumn * maxRow, _limits.MaxTemplateCells, "template");
                }
                totalRectangle += maxColumn * maxRow;
                PrintDataSetAdapter.Limit(totalRectangle, _limits.MaxTemplateCells, "template");
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException or XmlException)
        { throw new PrintValidationException("template", "invalid_template", "Template must be an XLSX ZIP package."); }
    }
    public void Dispose() => _slots.Dispose();
}
