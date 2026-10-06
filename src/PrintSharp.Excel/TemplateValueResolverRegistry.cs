namespace PrintSharp.Excel;

/// <summary>
/// 生成されたテンプレート Resolver をデータ型ごとに登録します。
/// </summary>
/// <typeparam name="TData">テンプレート データの型。</typeparam>
public static class TemplateValueResolverRegistry<TData>
{
    private static ITemplateValueResolver? _resolver;

    /// <summary>
    /// 登録済み Resolver を取得します。
    /// </summary>
    public static ITemplateValueResolver? Resolver => Volatile.Read(ref _resolver);

    /// <summary>
    /// 指定した Resolver をこのデータ型に登録します。
    /// </summary>
    /// <param name="resolver">登録する Resolver。</param>
    public static void Register(ITemplateValueResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Volatile.Write(ref _resolver, resolver);
    }
}
