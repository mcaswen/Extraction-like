using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AnimStateMachine
{
    private readonly AnimContext _ctx;
    private readonly Dictionary<AnimStateId, IAnimState> _states = new();

    private IAnimState _current;

    public AnimStateId CurrentId => _current != null ? _current.Id : default;

    public AnimStateMachine(AnimContext ctx)
    {
        _ctx = ctx;
    }

    public void AddState(IAnimState state)
    {
        _states.Add(state.Id, state);
    }

    public void Start(AnimStateId id)
    {
        _current = _states[id];
        _ctx.StateTime = 0f;
        _ctx.CurrentStateId = id;
        _current.Enter(_ctx);
    }

    public void Tick(float deltaTime)
    {
        _ctx.StateTime += deltaTime;
        _ctx.NormalizedTime = _ctx.Anim.GetNormalizedTime();

        _current.Tick(_ctx, deltaTime);
    }

    public bool ChangeTo(AnimStateId nextId)
    {
        if (_current.Id == nextId)
            return false;

        if(!_states.TryGetValue(nextId,out IAnimState next))
            return false;

        if(!_current.CanExit(_ctx,nextId))
            return false;

        SwitchTo(next);
        return true;
    }

    public bool ForceTo(AnimStateId nextId)
    {
        if (!_states.TryGetValue(nextId, out IAnimState next))
            return false;

        SwitchTo(next);
        return true;
    }

    private void SwitchTo(IAnimState next)
    {
        _current?.Exit(_ctx);

        _current = next;
        _ctx.StateTime = 0f;
        _ctx.CurrentStateId = next.Id;

        _current.Enter(_ctx);
    }
}
