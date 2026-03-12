using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SOTL.API;

namespace SOTL.UI
{
    /// <summary>
    /// Tab toggles the StatsPanel and fetches live member state from Wix on open.
    /// Merged with PlayerStateDisplay to avoid assembly caching issues.
    /// </summary>
    public class StatsPanelToggler : MonoBehaviour
    {
        GameObject _panel;

        // Labels — found by name under StatsPanel
        Text _xpLabel, _levelLabel, _fameLabel, _prestigeLabel, _statusLabel;
        GameObject _linkedPanel, _linkPromptPanel;

        void Start()
        {
            // Find the panel
            var t = transform.Find("StatsPanel");
            _panel = t != null ? t.gameObject : GameObject.Find("StatsPanel");

            if (_panel == null) { Debug.LogWarning("[SOTL] StatsPanelToggler: StatsPanel not found."); return; }

            // Wire labels
            _xpLabel        = FindLabel("XPLabel");
            _levelLabel     = FindLabel("LevelLabel");
            _fameLabel      = FindLabel("FameLabel");
            _prestigeLabel  = FindLabel("PrestigeLabel");
            _statusLabel    = FindLabel("StatusLabel");
            _linkedPanel    = FindChild("LinkedPanel");
            _linkPromptPanel = FindChild("LinkPromptPanel");
        }

        void Update()
        {
            if (_panel == null) return;
            if (Keyboard.current == null) return;
            if (!Keyboard.current.tabKey.wasPressedThisFrame) return;

            bool nowOpen = !_panel.activeSelf;
            _panel.SetActive(nowOpen);
            Cursor.lockState = nowOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = nowOpen;

            if (nowOpen) Refresh();
        }

        void Refresh()
        {
            var api = SOTLApiManager.Instance;
            if (api == null) { Debug.LogWarning("[SOTL] Refresh: SOTLApiManager.Instance is null"); return; }
            Debug.Log("[SOTL] Refresh IsLinked=" + api.IsLinked + " memberId=" + api.LinkedMemberId);

            if (!api.IsLinked)
            {
                SetActive(_linkedPanel, false);
                SetActive(_linkPromptPanel, true);
                return;
            }

            SetText(_statusLabel, "Loading...");
            api.FetchMemberState(OnStateReceived);
        }

        void OnStateReceived(PlayerState state)
        {
            if (state == null) { SetText(_statusLabel, "Could not reach SOTL servers."); return; }

            SetActive(_linkedPanel, true);
            SetActive(_linkPromptPanel, false);
            SetText(_xpLabel,       "XP: " + state.totalXp);
            SetText(_levelLabel,    "Level " + state.xpLevel);
            SetText(_fameLabel,     "Fame: " + state.fame);
            SetText(_prestigeLabel, "Prestige: " + state.prestigeBalance);
            SetText(_statusLabel,   "");
        }

        Text FindLabel(string n)
        {
            if (_panel == null) return null;
            var pt = _panel.transform;
            var found = pt.Find(n) ?? pt.Find("LinkedPanel/" + n) ?? pt.Find("LinkPromptPanel/" + n);
            return found != null ? found.GetComponent<Text>() : null;
        }

        GameObject FindChild(string n)
        {
            if (_panel == null) return null;
            var found = _panel.transform.Find(n);
            return found != null ? found.gameObject : null;
        }

        void SetText(Text t, string v)       { if (t != null) t.text = v; }
        void SetActive(GameObject g, bool v) { if (g != null) g.SetActive(v); }
    }
}
