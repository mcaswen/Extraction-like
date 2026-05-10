using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AnimationDriver
{
    private readonly Animator _animator;
    private int _currentHash;

    public AnimationDriver(Animator animator)
    {
        _animator = animator;
    }

    public void Play(int hash,float fadeTime = 0.1f,float stateNormalizedTime = 0f)
    {
        if (_animator == null)
            return;

        _currentHash = hash;
        _animator.CrossFade(hash,fadeTime,0,stateNormalizedTime);
    }

    public float GetNormalizedTime()
    {
        if (_animator == null)
            return 0f;

        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);

        if (info.shortNameHash != _currentHash)
            return 0f;

        return info.normalizedTime;
    }
}
