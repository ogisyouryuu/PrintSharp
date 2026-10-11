using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace PrintSharp.Api;

public static class MultipartPrintRequestReader
{
    public static async Task<(byte[] Template, PrintDataSet Data)> ReadAsync(HttpRequest request, PrintApiOptions limits, CancellationToken token)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType) ||
            !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw new PrintValidationException("request", "unsupported_media_type", "Content-Type must be multipart/form-data.", 415);
        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 128)
            throw new PrintValidationException("request", "invalid_multipart", "A valid multipart boundary is required.");

        using var body = new LimitedReadStream(request.Body, limits.MaxRequestBytes);
        var reader = new MultipartReader(boundary, body) { HeadersCountLimit = 16, HeadersLengthLimit = 16384 };
        byte[]? template = null;
        PrintDataSet? data = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(token)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) ||
                    !disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                    throw new PrintValidationException("request", "invalid_multipart", "Every part must have a form-data disposition.");
                var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
                if (name is not ("template" or "data") || !seen.Add(name))
                    throw new PrintValidationException("request", "invalid_multipart", "Only one template and one data part are accepted.");
                if (name == "template" && !disposition.FileName.HasValue && !disposition.FileNameStar.HasValue)
                    throw new PrintValidationException("template", "required", "Template must be a file part.");
                using var content = new MemoryStream();
                var buffer = new byte[8192];
                int count;
                while ((count = await section.Body.ReadAsync(buffer, token)) > 0)
                {
                    PrintDataSetAdapter.Limit(content.Length + count, name == "template" ? limits.MaxTemplateBytes : limits.MaxRequestBytes, name);
                    await content.WriteAsync(buffer.AsMemory(0, count), token);
                }
                if (name == "template") template = content.ToArray();
                else
                {
                    content.Position = 0;
                    try { data = await JsonSerializer.DeserializeAsync<PrintDataSet>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web), token); }
                    catch (JsonException) { throw new PrintValidationException("data", "invalid_json", "Data part must contain a valid JSON data set."); }
                }
            }
            // Count epilogue bytes too; MultipartReader can stop before the underlying stream ends.
            var tail = new byte[8192];
            while (await body.ReadAsync(tail, token) > 0) { }
        }
        catch (InvalidDataException)
        { throw new PrintValidationException("request", "invalid_multipart", "Malformed multipart request."); }
        catch (IOException) when (!token.IsCancellationRequested)
        { throw new PrintValidationException("request", "invalid_multipart", "Incomplete multipart request."); }

        if (template is null || template.Length == 0)
            throw new PrintValidationException("template", "required", "A nonempty template file is required.");
        if (data is null) throw new PrintValidationException("data", "required", "A JSON data part is required.");
        return (template, data);
    }

    // Counts real bytes, including chunked requests. Disposing leaves the server-owned body open.
    private sealed class LimitedReadStream(Stream inner, long limit) : Stream
    {
        private long _read;
        private int Count(int count)
        {
            _read += count;
            PrintDataSetAdapter.Limit(_read, limit, "request");
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Count(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
