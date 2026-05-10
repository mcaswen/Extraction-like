using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterInputReader))]
[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(CharacterCombat))]
public class CharacterAnimationControllers : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AnimStateId debugCurrentState;
    [SerializeField] private float debugNormalizedTime;
    [SerializeField] private float debugStateTime;

    private AnimStateMachine _fsm;
    private AnimContext _ctx;
    private InputBuffer _inputBuffer;

    private CharacterInputReader _input;
    private CharacterMotor _motor;
    private CharacterCombat _combat;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        _input = GetComponent<CharacterInputReader>();
        _motor = GetComponent<CharacterMotor>();
        _combat = GetComponent<CharacterCombat>();

        _inputBuffer = new InputBuffer();

        _ctx = new AnimContext
        {
            Anim = new AnimationDriver(animator),
            Input = _input,
            Motor = _motor,
            Combat = _combat,
            InputBuffer = _inputBuffer
        };

        _fsm = new AnimStateMachine(_ctx);

        _fsm.AddState(new IdleState(_fsm));
        _fsm.AddState(new RunState(_fsm));
        _fsm.AddState(new AttackState(_fsm));
        _fsm.AddState(new DeathState());

        _fsm.Start(AnimStateId.Idle);
    }

    // Update is called once per frame
    private void Update()
    {
        _inputBuffer.Tick(Time.deltaTime);

        if (_combat.IsDead && _fsm.CurrentId != AnimStateId.Death)
        {
            _fsm.ForceTo(AnimStateId.Death);
            return;
        }

        _fsm.Tick(Time.deltaTime);

        debugCurrentState = _ctx.CurrentStateId;
        debugNormalizedTime = _ctx.NormalizedTime;
        debugStateTime = _ctx.StateTime;
    }

    public void PlayDeath()
    {
        _combat.Kill();
        _fsm.ForceTo(AnimStateId.Death);
    }
}
