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

        Vector3 move = new Vector3(_input.Move.x, 0f, _input.Move.y);
        _controller.Move(move.normalized*moveSpeed*Time.deltaTime);
    }

    public void SetMoveLocked(bool locked)
    {
        _moveLocked = locked;
    }

}
