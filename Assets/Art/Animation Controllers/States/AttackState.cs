public sealed class AttackState : IAnimState
{
    private readonly AnimStateMachine _fsm;
    private bool _hitboxOpened;
    private bool _hitboxClosed;

    public AnimStateId Id => AnimStateId.Attack;
    public int Priority => 10;

    public AttackState(AnimStateMachine fsm)
    {
        _fsm = fsm;
    }

    public void Enter(AnimContext ctx)
    {
        _hitboxOpened = false;
        _hitboxClosed = false;

        ctx.InputBuffer.ConsumeAttack();
        ctx.Motor.SetMoveLocked(true);
        ctx.Anim.Play(AnimHash.Attack, 0.05f);
    }

    public void Tick(AnimContext ctx, float deltaTime)
    {
        float t = ctx.NormalizedTime;

        if (t >= 0.20f && !_hitboxOpened)
        {
            _hitboxOpened = true;
            ctx.Combat.OpenHitbox();
        }

        if (t >= 0.45f && !_hitboxClosed)
        {
            _hitboxClosed = true;
            ctx.Combat.CloseHitbox();
        }

        if (t >= 0.95f)
        {
            bool hasMove = ctx.Input.Move.sqrMagnitude > 0.01f;
            _fsm.ChangeTo(hasMove ? AnimStateId.Run : AnimStateId.Idle);
        }
    }

    public void Exit(AnimContext ctx)
    {
        ctx.Combat.CloseHitbox();
        ctx.Motor.SetMoveLocked(false);
    }

    public bool CanExit(AnimContext ctx, AnimStateId nextState)
    {
        if (nextState == AnimStateId.Death)
            return true;

        return ctx.NormalizedTime >= 0.95f;
    }
}
