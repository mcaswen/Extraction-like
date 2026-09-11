/// <summary>普通快捷转移的结果原因，不含显示文本或自动化策略。</summary>
public enum InventoryQuickTransferFailure
{
    None,
    SearchPending,
    NotInteractive,
    NoOpenSession,
    InvalidSource,
    GridPolicy,
    NoSpace
}
