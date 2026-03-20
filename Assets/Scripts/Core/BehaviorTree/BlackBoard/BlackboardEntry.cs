using System;

/// <summary>
/// 单个黑板值的类定义
/// </summary>
public sealed class BlackboardEntry
{
    public object Value;
    public Type ValueType;
    public int Version;
    public double Timestamp;
}