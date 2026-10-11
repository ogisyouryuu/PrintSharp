using System.Data;
using System.Text.Json;
using PrintSharp.Api;
using Xunit;

namespace PrintSharp.Api.Tests;

public class AdapterTests
{
    internal static PrintDataSet Input(string type, string json, bool nullable = true, string? format = null) =>
        new([new("T", [new("Value", type, format, nullable)], [JsonSerializer.Deserialize<JsonElement>(json)])]);

    [Theory]
    [InlineData("string", "\"\"", typeof(string))]
    [InlineData("bool", "true", typeof(bool))]
    [InlineData("int", "42", typeof(int))]
    [InlineData("long", "9223372036854775807", typeof(long))]
    [InlineData("decimal", "79228162514264337593543950335", typeof(decimal))]
    [InlineData("double", "1.5", typeof(double))]
    [InlineData("date", "\"2026-10-10\"", typeof(DateTime))]
    [InlineData("datetime", "\"2026-10-10T12:00:00+09:00\"", typeof(DateTime))]
    [InlineData("guid", "\"00000000-0000-0000-0000-000000000001\"", typeof(Guid))]
    [InlineData("bytes", "\"AQI=\"", typeof(byte[]))]
    public void ConvertsEveryType(string type, string value, Type expected)
    {
        using var data = new PrintDataSetAdapter(new()).Convert(Input(type, "{\"Value\":" + value + "}"));
        Assert.IsType(expected, data.Tables[0].Rows[0][0]);
        if (type == "datetime")
        {
            Assert.Equal(new DateTime(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc), data.Tables[0].Rows[0][0]);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)data.Tables[0].Rows[0][0]).Kind);
        }
    }

    [Theory]
    [InlineData("int", "{\"Value\":1.5}", "invalid_int")]
    [InlineData("int", "{\"Value\":2147483648}", "invalid_int")]
    [InlineData("long", "{\"Value\":9223372036854775808}", "invalid_long")]
    [InlineData("long", "{\"Value\":1.5}", "invalid_long")]
    [InlineData("decimal", "{\"Value\":79228162514264337593543950336}", "invalid_decimal")]
    [InlineData("double", "{\"Value\":1e999}", "invalid_double")]
    [InlineData("date", "{\"Value\":\"2026/10/10\"}", "invalid_date")]
    [InlineData("datetime", "{\"Value\":\"2026-10-10T12:00:00\"}", "invalid_datetime")]
    [InlineData("guid", "{\"Value\":\"bad\"}", "invalid_guid")]
    [InlineData("bytes", "{\"Value\":\"bad\"}", "invalid_bytes")]
    [InlineData("string", "{}", "missing_column")]
    [InlineData("string", "{\"value\":\"a\"}", "unknown_or_duplicate_column")]
    [InlineData("string", "{\"Value\":\"a\",\"Extra\":1}", "unknown_or_duplicate_column")]
    [InlineData("string", "{\"Value\":\"a\",\"Value\":\"b\"}", "unknown_or_duplicate_column")]
    [InlineData("string", "[]", "invalid_row")]
    [InlineData("string", "42", "invalid_row")]
    [InlineData("System.String", "{\"Value\":\"a\"}", "invalid_type")]
    public void RejectsInvalidValues(string type, string row, string code)
    {
        var error = Assert.Throws<PrintValidationException>(() => new PrintDataSetAdapter(new()).Convert(Input(type, row)));
        Assert.Equal(code, error.Error.Code);
        Assert.StartsWith("data.tables[0]", error.Error.Path);
    }

    [Fact]
    public void NullRequiresNullable()
    {
        using var data = new PrintDataSetAdapter(new()).Convert(Input("string", "{\"Value\":null}"));
        Assert.Equal(DBNull.Value, data.Tables[0].Rows[0][0]);
        Assert.Throws<PrintValidationException>(() => new PrintDataSetAdapter(new()).Convert(Input("string", "{\"Value\":null}", false)));
    }

    [Fact]
    public void MultipleAndEmptyTablesKeepTheirSchema()
    {
        using var data = new PrintDataSetAdapter(new()).Convert(new([
            new("A", [new("X", "int")], []), new("B", [new("Y", "string")], [])]));
        Assert.Equal(typeof(int), data.Tables[0].Columns[0].DataType);
        Assert.Equal("Y", data.Tables[1].Columns[0].ColumnName);
        Assert.Empty(data.Tables[0].Rows.Cast<DataRow>());
    }

    [Fact]
    public void DuplicateNamesAreRejectedAndOrdinalCaseIsAllowed()
    {
        var adapter = new PrintDataSetAdapter(new());
        Assert.Throws<PrintValidationException>(() => adapter.Convert(new([new("T", [], []), new("T", [], [])])));
        Assert.Throws<PrintValidationException>(() => adapter.Convert(new([new("T", [new("X", "int"), new("X", "int")], [])])));
        using var data = adapter.Convert(new([new("T", [new("X", "int"), new("x", "int")], []), new("t", [], [])]));
        Assert.Equal(2, data.Tables.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("[Red]0")]
    [InlineData("=RUN()")]
    public void RejectsInvalidFormats(string format) => Assert.Throws<PrintValidationException>(() =>
        new PrintDataSetAdapter(new()).Convert(Input("decimal", "{\"Value\":1}", format: format)));

    [Fact]
    public void LimitsAndCancellationAreEnforced()
    {
        Assert.Equal(413, Assert.Throws<PrintValidationException>(() => new PrintDataSetAdapter(new() { MaxStringLength = 2 })
            .Convert(Input("string", "{\"Value\":\"abc\"}"))).Status);
        Assert.Equal(413, Assert.Throws<PrintValidationException>(() => new PrintDataSetAdapter(new() { MaxBytesLength = 1 })
            .Convert(Input("bytes", "{\"Value\":\"AQI=\"}"))).Status);
        Assert.Throws<OperationCanceledException>(() => new PrintDataSetAdapter(new()).Convert(Input("int", "{\"Value\":1}"), new(true)));
    }
}
