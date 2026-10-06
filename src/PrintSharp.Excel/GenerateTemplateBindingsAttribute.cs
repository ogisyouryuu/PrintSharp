namespace PrintSharp.Excel;

/// <summary>
/// 指定したデータ型に対して、テンプレート用の直接アクセス Resolver を生成します。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateTemplateBindingsAttribute : Attribute
{
}
