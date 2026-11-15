namespace PrintSharp.Cells;

/// <summary>Excel の数式と、他の出力先で使用する計算済みの値です。</summary>
public sealed record FormulaValue(string Formula, object? Value)
{
    /// <inheritdoc/>
    public override string ToString() => Formula;
}
