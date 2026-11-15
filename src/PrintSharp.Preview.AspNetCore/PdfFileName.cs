namespace PrintSharp.Preview.AspNetCore;

internal static class PdfFileName
{
    public static string Sanitize(string? name)
    {
        string clean = new((name ?? "").Where(c => !char.IsControl(c) && !"<>:\"/\\|?*".Contains(c)).ToArray());
        clean = clean.Trim().Trim('.');
        if (clean.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) clean = clean[..^4];
        if (clean.Length > 170) clean = clean[..170];
        if (clean.Length > 0 && char.IsHighSurrogate(clean[^1])) clean = clean[..^1];
        return (string.IsNullOrWhiteSpace(clean) ? "document" : clean) + ".pdf";
    }
}
