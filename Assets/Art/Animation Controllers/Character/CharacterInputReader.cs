using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterInputReader : MonoBehaviour
{
    public Vector2 Move {  get; private set; }
    public bool AttackPressed {  get; private set; }

    // Update is called once per frame
    void Update()
    {
        Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        AttackPressed = Input.GetMouseButtonDown(0);
    }
}
