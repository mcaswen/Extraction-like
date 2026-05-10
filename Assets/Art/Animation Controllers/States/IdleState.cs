using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleState : IAnimState
{
    private readonly AnimStateMachine _fsm;

    public AnimStateId Id => AnimStateId.Idle;
    public int Priority => 0;

    public IdleState(AnimStateMachine fsm)
    {
        _fsm = fsm;
    }

    public void Enter(AnimContext ctx)
    {
        ctx.Motor.SetMoveLocked(false);
        ctx.Anim.Play(AnimHash.Idle, 0.15f);
    }

    public void Tick(AnimContext ctx,float deltaTime)
    {
        if(ctx.Input.AttackPressed)
        {
            ctx.InputBuffer.BufferAttack(0.25f);
            _fsm.ChangeTo(AnimStateId.Attack);
            return;
        }

        if(ctx.Input.Move.sqrMagnitude>0.01f)
        {
            _fsm.ChangeTo(AnimStateId.Run);
        }
    }

    public void Exit(AnimContext ctx) { }

    public bool CanExit(AnimContext ctx, AnimStateId nextId)
    {
        return true;
    }
}
