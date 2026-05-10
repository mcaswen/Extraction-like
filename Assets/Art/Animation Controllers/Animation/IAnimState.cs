using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IAnimState 
{
    AnimStateId Id { get; }
    int Priority { get; }

    void Enter(AnimContext ctx);
    void Tick(AnimContext ctx, float deltaTime);
    void Exit(AnimContext ctx);

    bool CanExit(AnimContext ctx, AnimStateId nextState);
}
