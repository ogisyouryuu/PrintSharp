using PrintSharp.Documents;

namespace PrintSharp.Excel;

/// <summary>
/// 型付きデータを使用して Excel テンプレートをレンダリングします。
/// </summary>
/// <typeparam name="TData">テンプレート データの型。</typeparam>
public sealed class ExcelTemplate<TData>
{
    private readonly byte[] _templateBytes;
    private readonly ExcelRenderOptions? _options;

    private ExcelTemplate(byte[] templateBytes, ExcelRenderOptions? options)
    {
        _templateBytes = templateBytes;
        _options = options;
    }

    /// <summary>
    /// Excel ファイルから型付きテンプレートを読み込みます。
    /// </summary>
    /// <param name="templatePath">テンプレート ファイルのパス。</param>
    /// <param name="options">レンダリング オプション。</param>
    /// <returns>読み込まれたテンプレート。</returns>
    public static ExcelTemplate<TData> Load(string templatePath, ExcelRenderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        return new ExcelTemplate<TData>(File.ReadAllBytes(templatePath), options);
    }

    /// <summary>
    /// Excel ストリームから型付きテンプレートを読み込みます。
    /// ストリームの現在位置から末尾までをテンプレートとして読み込みます。
    /// </summary>
    /// <param name="templateStream">テンプレートを含むストリーム。</param>
    /// <param name="options">レンダリング オプション。</param>
    /// <returns>読み込まれたテンプレート。</returns>
    public static ExcelTemplate<TData> Load(Stream templateStream, ExcelRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        using var buffer = new MemoryStream();
        templateStream.CopyTo(buffer);
        return new ExcelTemplate<TData>(buffer.ToArray(), options);
    }

    /// <summary>
    /// 指定したデータでテンプレートをレンダリングします。
    /// 生成済み Resolver が登録されている場合はそれを使用し、未登録の場合は既定 Resolver を使用します。
    /// </summary>
    /// <param name="data">テンプレート データ。</param>
    /// <param name="options">このレンダリングに使用するオプション。省略時は読み込み時のオプションを使用します。</param>
    /// <returns>レンダリングされたドキュメント。</returns>
    public Document Render(TData data, ExcelRenderOptions? options = null)
    {
        using var stream = new MemoryStream(_templateBytes, writable: false);
        var resolver = TemplateValueResolverRegistry<TData>.Resolver;
        var parser = resolver is null ? ExcelTemplateParser.Instance : new ExcelTemplateParser(resolver);
        return parser.Render(stream, data, options ?? _options);
    }
}
