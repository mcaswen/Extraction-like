using UnityEngine;

/// <summary>
/// 锚点守卫的符文弱点。
/// 玩家按正确顺序命中这些符文时可解除守卫。
/// </summary>
[RequireComponent(typeof(Collider))]
public class AnchorSentinelRuneWeakpoint : MonoBehaviour
{
    /// <summary>
    /// 该符文在谜题命中序列中的顺序。
    /// </summary>
    public int RuneOrderIndex;

    /// <summary>
    /// 用于表现符文状态颜色的渲染器。
    /// </summary>
    public Renderer RuneRenderer;

    private AnchorSentinelBehaviorController _owner;
    private bool _isDisabled;

    private void Reset()
    {
        Collider hitCollider = GetComponent<Collider>();
        hitCollider.isTrigger = true;
    }

    /// <summary>
    /// 初始化符文弱点归属、顺序和默认可命中状态。
    /// </summary>
    /// <param name="owner">拥有该符文的锚点守卫。</param>
    /// <param name="runeOrderIndex">谜题顺序索引。</param>
    public void Initialize(AnchorSentinelBehaviorController owner, int runeOrderIndex)
    {
        _owner = owner;
        RuneOrderIndex = runeOrderIndex;
        _isDisabled = false;

        if (RuneRenderer == null)
        {
            RuneRenderer = GetComponentInChildren<Renderer>();
        }

        SetRuneColor(new Color(0.32f, 0.74f, 1f, 1f));
        Collider hitCollider = GetComponent<Collider>();
        if (hitCollider != null)
        {
            hitCollider.enabled = true;
        }
    }

    /// <summary>
    /// 通知锚点守卫该符文被命中。
    /// </summary>
    public void NotifyHit()
    {
        if (_isDisabled || _owner == null)
        {
            return;
        }

        _owner.NotifyRuneHit(this);
    }

    /// <summary>
    /// 将符文标记为已解开，并关闭碰撞。
    /// </summary>
    public void MarkSolved()
    {
        _isDisabled = true;
        SetRuneColor(new Color(0.42f, 1f, 0.56f, 1f));
        Collider hitCollider = GetComponent<Collider>();
        if (hitCollider != null)
        {
            hitCollider.enabled = false;
        }
    }

    /// <summary>
    /// 将符文标记为命中错误状态。
    /// </summary>
    public void MarkFailed()
    {
        SetRuneColor(new Color(1f, 0.35f, 0.18f, 1f));
    }

    /// <summary>
    /// 将符文标记为警戒状态。
    /// </summary>
    public void MarkAlert()
    {
        if (!_isDisabled)
        {
            SetRuneColor(new Color(1f, 0.82f, 0.24f, 1f));
        }
    }

    /// <summary>
    /// 将符文重置回休眠可命中状态。
    /// </summary>
    public void ResetToDormant()
    {
        _isDisabled = false;
        SetRuneColor(new Color(0.32f, 0.74f, 1f, 1f));
        Collider hitCollider = GetComponent<Collider>();
        if (hitCollider != null)
        {
            hitCollider.enabled = true;
        }
    }

    private void SetRuneColor(Color color)
    {
        if (RuneRenderer != null && RuneRenderer.material.HasProperty("_Color"))
        {
            RuneRenderer.material.color = color;
        }
    }
}
