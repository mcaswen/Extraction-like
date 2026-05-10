using UnityEngine;

public sealed class InputBuffer
{
    private float _attackTimer;

    public bool HasAttack => _attackTimer > 0f;
    public void Tick(float deltaTime)
    {
        _attackTimer = Mathf.Max(0f, _attackTimer - deltaTime);
    }

    public void BufferAttack(float duration)
    {
        _attackTimer = duration;
    }
    public bool ConsumeAttack()
    {
        if (_attackTimer <= 0f)
            return false;

        _attackTimer = 0f;
        return true;
    }

    public void Clear()
    {
        _attackTimer = 0f;
    }
}
