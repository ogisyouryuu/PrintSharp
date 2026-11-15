using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintSharp.Preview.Blazor;

/// <summary>プレビューの表示文言。null の項目は選択言語の既定値を使用します。</summary>
public sealed class PreviewLabels
{
    /// <summary>previewTitle の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("previewTitle"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviewTitle { get; set; }
    /// <summary>pageDescription の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("pageDescription"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PageDescription { get; set; }
    /// <summary>first の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("first"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? First { get; set; }
    /// <summary>previous の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("previous"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Previous { get; set; }
    /// <summary>next の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("next"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Next { get; set; }
    /// <summary>last の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("last"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Last { get; set; }
    /// <summary>zoomIn の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("zoomIn"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ZoomIn { get; set; }
    /// <summary>zoomOut の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("zoomOut"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ZoomOut { get; set; }
    /// <summary>fitWidth の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("fitWidth"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FitWidth { get; set; }
    /// <summary>fitPage の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("fitPage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FitPage { get; set; }
    /// <summary>retry の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("retry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Retry { get; set; }
    /// <summary>loading の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("loading"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Loading { get; set; }
    /// <summary>empty の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("empty"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Empty { get; set; }
    /// <summary>singlePage の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("singlePage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SinglePage { get; set; }
    /// <summary>continuous の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("continuous"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Continuous { get; set; }
    /// <summary>thumbnails の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("thumbnails"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Thumbnails { get; set; }
    /// <summary>search の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("search"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Search { get; set; }
    /// <summary>textSelection の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("textSelection"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TextSelection { get; set; }
    /// <summary>copy の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("copy"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Copy { get; set; }
    /// <summary>pageNumber の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("pageNumber"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PageNumber { get; set; }
    /// <summary>thumbnailPage の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("thumbnailPage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ThumbnailPage { get; set; }
    /// <summary>pageText の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("pageText"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PageText { get; set; }
    /// <summary>copied の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("copied"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Copied { get; set; }
    /// <summary>selectText の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("selectText"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SelectText { get; set; }
    /// <summary>clipboardUnavailable の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("clipboardUnavailable"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClipboardUnavailable { get; set; }
    /// <summary>searchPlaceholder の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("searchPlaceholder"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SearchPlaceholder { get; set; }
    /// <summary>searchLabel の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("searchLabel"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SearchLabel { get; set; }
    /// <summary>previousMatch の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("previousMatch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousMatch { get; set; }
    /// <summary>nextMatch の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("nextMatch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextMatch { get; set; }
    /// <summary>closeSearch の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("closeSearch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CloseSearch { get; set; }
    /// <summary>searching の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("searching"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Searching { get; set; }
    /// <summary>searchLimit の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("searchLimit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SearchLimit { get; set; }
    /// <summary>noMatches の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("noMatches"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NoMatches { get; set; }
    /// <summary>queryTooLong の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("queryTooLong"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QueryTooLong { get; set; }
    /// <summary>expired の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("expired"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expired { get; set; }
    /// <summary>searchUnsupported の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("searchUnsupported"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SearchUnsupported { get; set; }
    /// <summary>invalidTextIndex の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("invalidTextIndex"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvalidTextIndex { get; set; }
    /// <summary>invalidTextPage の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("invalidTextPage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvalidTextPage { get; set; }
    /// <summary>invalidTextRegion の表示文言。{page} と {total} はページ番号に置換されます。</summary>
    [JsonPropertyName("invalidTextRegion"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvalidTextRegion { get; set; }
    /// <summary>従来の辞書形式から変換します。</summary>
    public static implicit operator PreviewLabels(Dictionary<string, string> values) => JsonSerializer.Deserialize<PreviewLabels>(JsonSerializer.Serialize(values))!;
}
