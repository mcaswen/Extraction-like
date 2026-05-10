using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterCombat : MonoBehaviour
{
    [SerializeField] private Collider hitbox;

    public bool IsDead {  get; private set; }

    private void Awake()
    {
        CloseHitbox();
    }

    public void OpenHitbox()
    {
        if (hitbox != null)
            hitbox.enabled = true;
    }

    public void CloseHitbox()
    {
        if (hitbox != null)
            hitbox.enabled = false;
    }

    public void Kill()
    {
        IsDead = true;
    }
}
