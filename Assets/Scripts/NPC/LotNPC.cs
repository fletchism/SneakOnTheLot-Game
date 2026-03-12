using UnityEngine;
using UnityEngine.InputSystem;
using SOTL.NPC;

/// <summary>
/// Attach to any NPC root. Handles proximity prompt and E-key dialogue.
/// Requires a SphereCollider (trigger) on this GameObject or a child.
/// </summary>
[RequireComponent(typeof(Animator))]
public class LotNPC : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] string _npcName = "Filmmaker";

    [Header("Dialogue")]
    [TextArea(2, 6)]
    [SerializeField] string[] _lines = new[]
    {
        "Welcome to Sneak On The Lot! Submit your film to get started.",
        "Every film you submit earns XP and levels up your profile.",
        "Good luck out there, filmmaker."
    };

    [Header("Interaction")]
    [SerializeField] float _interactRadius = 2.5f;

    Animator       _animator;
    LotDialogueUI  _dialogueUI;
    bool           _playerInRange;
    bool           _inDialogue;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null && _animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning($"[LotNPC] {gameObject.name}: no animator controller — Animator disabled.");
            _animator.enabled = false;
        }

        // Ensure a trigger collider exists for proximity
        var col = GetComponent<SphereCollider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = _interactRadius;
    }

    void Start()
    {
        _dialogueUI = Object.FindFirstObjectByType<LotDialogueUI>();
        if (_dialogueUI == null)
            Debug.LogWarning("[LotNPC] No LotDialogueUI found in scene. Run Build Lot Scene.");
    }

    void Update()
    {
        if (!_playerInRange || _dialogueUI == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (_inDialogue)
                _dialogueUI.AdvanceDialogue();
            else
                OpenDialogue();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        _dialogueUI?.ShowPrompt(true, _npcName);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        _inDialogue = false;
        _dialogueUI?.ShowPrompt(false, _npcName);
    }

    void OpenDialogue()
    {
        _inDialogue = true;
        _dialogueUI.OpenDialogue(_npcName, _lines, () => _inDialogue = false);
    }
}
