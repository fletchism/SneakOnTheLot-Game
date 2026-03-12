using System;
using UnityEngine;
using UnityEngine.UI;

namespace SOTL.NPC
{
    /// <summary>
    /// Single screen-space UI that all NPCs share.
    /// Created by LotSceneBuilder; NPCs find it via FindFirstObjectByType.
    /// </summary>
    public class LotDialogueUI : MonoBehaviour
    {
        [Header("Prompt")]
        [SerializeField] GameObject _promptRoot;
        [SerializeField] Text       _promptText;

        [Header("Dialogue Panel")]
        [SerializeField] GameObject _panelRoot;
        [SerializeField] Text       _nameText;
        [SerializeField] Text       _lineText;
        [SerializeField] Text       _continueHint;

        string[]  _lines;
        int       _lineIndex;
        Action    _onEnd;

        // ── Prompt ────────────────────────────────────────────────────

        public void ShowPrompt(bool show, string npcName)
        {
            if (_promptRoot == null) return;
            _promptRoot.SetActive(show && !_panelRoot.activeSelf);
            if (_promptText != null)
                _promptText.text = $"[E]  Talk to {npcName}";
        }

        // ── Dialogue ──────────────────────────────────────────────────

        public void OpenDialogue(string npcName, string[] lines, Action onEnd)
        {
            _lines     = lines;
            _lineIndex = 0;
            _onEnd     = onEnd;

            if (_promptRoot != null) _promptRoot.SetActive(false);
            if (_panelRoot  != null) _panelRoot.SetActive(true);
            if (_nameText   != null) _nameText.text = npcName;

            ShowLine();
        }

        public void AdvanceDialogue()
        {
            _lineIndex++;
            if (_lineIndex >= _lines.Length)
                CloseDialogue();
            else
                ShowLine();
        }

        void ShowLine()
        {
            if (_lineText != null)
                _lineText.text = _lines[_lineIndex];
            if (_continueHint != null)
                _continueHint.text = _lineIndex < _lines.Length - 1 ? "[E] Continue" : "[E] Close";
        }

        void CloseDialogue()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            _onEnd?.Invoke();
            _onEnd = null;
        }
    }
}
