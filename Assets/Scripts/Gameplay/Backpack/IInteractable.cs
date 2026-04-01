/// <summary>
/// 场景内可交互实体的统一入口
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 获取悬浮提示文本
    /// </summary>
    /// <returns>展示给玩家的交互文案</returns>
    string GetPromptText();

    /// <summary>
    /// 执行交互行为
    /// </summary>
    void Interact();
}

/// <summary>
/// 支持第二交互键的场景实体接口
/// </summary>
public interface ISecondaryInteractable
{
    /// <summary>
    /// 获取第二交互提示文本
    /// </summary>
    /// <returns>展示给玩家的第二交互文案</returns>
    string GetSecondaryPromptText();

    /// <summary>
    /// 执行第二交互行为
    /// </summary>
    void SecondaryInteract();
}
