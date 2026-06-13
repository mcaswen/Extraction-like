using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class CharacterMotor : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4.0f;

    private CharacterController _controller;
    private CharacterInputReader _input;

    private bool _moveLocked;
    private float _speedBoostDurationRemaining;
    private float _speedBoostMultiplier = 1f;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<CharacterInputReader>();
    }
    // Update is called once per frame
    void Update()
    {
        if (_controller == null || _input == null)
            return;

        if (_moveLocked) return;

        TickSpeedBoost();
        Vector3 move = new Vector3(_input.Move.x, 0f, _input.Move.y);
        _controller.Move(move.normalized * moveSpeed * _speedBoostMultiplier * Time.deltaTime);
    }

    public void SetMoveLocked(bool locked)
    {
        _moveLocked = locked;
    }

    public void ApplyMoveSpeedMultiplier(float multiplier, float duration)
    {
        if (duration <= 0f || multiplier <= 0f)
        {
            return;
        }

        _speedBoostDurationRemaining = Mathf.Max(_speedBoostDurationRemaining, duration);
        _speedBoostMultiplier = Mathf.Max(_speedBoostMultiplier, multiplier);
    }

    private void TickSpeedBoost()
    {
        if (_speedBoostDurationRemaining <= 0f)
        {
            _speedBoostMultiplier = 1f;
            return;
        }

        _speedBoostDurationRemaining = Mathf.Max(0f, _speedBoostDurationRemaining - Time.deltaTime);
        if (_speedBoostDurationRemaining <= 0f)
        {
            _speedBoostMultiplier = 1f;
        }
    }
}
