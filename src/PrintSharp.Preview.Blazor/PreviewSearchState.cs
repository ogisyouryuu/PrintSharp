namespace PrintSharp.Preview.Blazor;

/// <summary>ブラウザー検索の現在位置と件数。</summary>
public sealed record PreviewSearchState(string Query, int Total, int Current, bool Busy, bool Truncated, string? Error);
