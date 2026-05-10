public sealed class DeathState : IAnimState
{
    public AnimStateId Id => AnimStateId.Death;
    public int Priority => 999;

    public void Enter(AnimContext ctx)
    {
        ctx.InputBuffer.Clear();
        ctx.Combat.CloseHitbox();
        ctx.Motor.SetMoveLocked(true);
        ctx.Anim.Play(AnimHash.Death, 0.05f);
    }

    public void Tick(AnimContext ctx, float deltaTime) { }

    public void Exit(AnimContext ctx) { }

    public bool CanExit(AnimContext ctx, AnimStateId nextState)
    {
        return false;
    }
}
