using UnityEngine;

/// <summary>
/// 锚点守卫的符文弱点。
/// 玩家按正确顺序命中这些符文时可解除守卫。
/// </summary>
[RequireComponent(typeof(Collider))]
public class AnchorSentinelRuneWeakpoint : MonoBehaviour
{
    public int RuneOrderIndex;
    public Renderer RuneRenderer;

    private AnchorSentinelBehaviorController _owner;
    private bool _isDisabled;

    private void Reset()
    {
        Collider hitCollider = GetComponent<Collider>();
        hitCollider.isTrigger = true;
    }

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

    public void NotifyHit()
    {
        if (_isDisabled || _owner == null)
        {
            return;
        }

        _owner.NotifyRuneHit(this);
    }

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

    public void MarkFailed()
    {
        SetRuneColor(new Color(1f, 0.35f, 0.18f, 1f));
    }

    public void MarkAlert()
    {
        if (!_isDisabled)
        {
            SetRuneColor(new Color(1f, 0.82f, 0.24f, 1f));
        }
    }

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
