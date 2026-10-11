namespace PrintSharp.Api;

public sealed class PrintApiOptions
{
    public long MaxRequestBytes { get; set; } = 20 * 1024 * 1024;
    public int MaxTemplateBytes { get; set; } = 5 * 1024 * 1024;
    public long MaxExpandedBytes { get; set; } = 40 * 1024 * 1024;
    public int MaxZipEntries { get; set; } = 2000;
    public int MaxWorksheets { get; set; } = 20;
    public int MaxTemplateCells { get; set; } = 100000;
    public int MaxGeneratedCells { get; set; } = 500000;
    public int MaxTables { get; set; } = 20;
    public int MaxColumns { get; set; } = 100;
    public int MaxRows { get; set; } = 10000;
    public int MaxTotalCells { get; set; } = 200000;
    public int MaxStringLength { get; set; } = 32767;
    public int MaxBytesLength { get; set; } = 65536;
    public int MaxConcurrency { get; set; } = 2;
    public int ProcessingTimeoutSeconds { get; set; } = 60;

    public bool IsValid() => MaxRequestBytes > 0 && MaxTemplateBytes > 0 && MaxExpandedBytes > 0 &&
        MaxZipEntries > 0 && MaxWorksheets > 0 && MaxTemplateCells > 0 && MaxGeneratedCells > 0 && MaxTables > 0 &&
        MaxColumns > 0 && MaxRows > 0 && MaxTotalCells > 0 && MaxStringLength > 0 &&
        MaxBytesLength > 0 && MaxConcurrency > 0 && ProcessingTimeoutSeconds > 0;
}
