using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunState : IAnimState
{
    private readonly AnimStateMachine _fsm;

    public AnimStateId Id => AnimStateId.Run;
    public int Priority => 0;

    public RunState(AnimStateMachine fsm)
    {
        _fsm = fsm;
    }

    public void Enter(AnimContext ctx)
    {
        ctx.Motor.SetMoveLocked(false);
        ctx.Anim.Play(AnimHash.Run, 0.12f);
    }

    public void Tick(AnimContext ctx, float deltaTime)
    {
        if (ctx.Input.AttackPressed)
        {
            ctx.InputBuffer.BufferAttack(0.25f);
            _fsm.ChangeTo(AnimStateId.Attack);
            return;
        }

        if (ctx.Input.Move.sqrMagnitude <= 0.01f)
        {
            _fsm.ChangeTo(AnimStateId.Idle);
        }
    }

    public void Exit(AnimContext ctx) { }

    public bool CanExit(AnimContext ctx, AnimStateId nextId)
    {
        return true;
    }
}
