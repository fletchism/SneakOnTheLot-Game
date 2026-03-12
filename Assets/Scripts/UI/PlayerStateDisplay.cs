using UnityEngine;
using UnityEngine.UI;
using SOTL.API;

namespace SOTL.UI
{
    public class PlayerStateDisplay : MonoBehaviour
    {
        Text _xpLabel, _levelLabel, _fameLabel, _prestigeLabel, _statusLabel;
        GameObject _linkedPanel, _linkPromptPanel;

        void Awake()
        {
            _xpLabel        = FindLabel("XPLabel");
            _levelLabel     = FindLabel("LevelLabel");
            _fameLabel      = FindLabel("FameLabel");
            _prestigeLabel  = FindLabel("PrestigeLabel");
            _statusLabel    = FindLabel("StatusLabel");
            _linkedPanel    = FindChild("LinkedPanel");
            _linkPromptPanel = FindChild("LinkPromptPanel");
        }

        void OnEnable() { Refresh(); }

        public void Refresh()
        {
            var api = SOTLApiManager.Instance;
            if (api == null)
            {
                Debug.LogWarning("[SOTL] PlayerStateDisplay: SOTLApiManager.Instance is null");
                return;
            }
            Debug.Log("[SOTL] Refresh IsLinked=" + api.IsLinked);

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
            if (state == null)
            {
                SetText(_statusLabel, "Could not reach SOTL servers.");
                return;
            }
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
            var t = transform.Find(n);
            if (t == null) t = transform.Find("LinkedPanel/" + n);
            if (t == null) t = transform.Find("LinkPromptPanel/" + n);
            return t != null ? t.GetComponent<Text>() : null;
        }

        GameObject FindChild(string n)
        {
            var t = transform.Find(n);
            return t != null ? t.gameObject : null;
        }

        void SetText(Text t, string v)           { if (t != null) t.text = v; }
        void SetActive(GameObject g, bool v)     { if (g != null) g.SetActive(v); }
    }
}
