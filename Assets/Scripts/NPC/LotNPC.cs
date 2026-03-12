using UnityEngine;

/// <summary>
/// Attached to every NPC in the SOTL scene.
/// Safely disables the Animator if no controller is assigned
/// rather than leaving the character in T-pose.
/// </summary>
public class LotNPC : MonoBehaviour
{
    private Animator _animator;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null && _animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[LotNPC] {gameObject.name}: no animator controller assigned — Animator disabled.");
            _animator.enabled = false;
        }
    }
}
