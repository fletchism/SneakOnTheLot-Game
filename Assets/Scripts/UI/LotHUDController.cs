using System.Collections;
using UnityEngine;
using TMPro;
using SOTL.API;

namespace SOTL.UI
{
    /// <summary>
    /// Drives the Synty HUD stat labels from SOTLApiManager.CurrentState.
    /// Polls on Start, then every <see cref="pollInterval"/> seconds.
    /// Falls back gracefully if not linked (shows dashes).
    /// </summary>
    public class LotHUDController : MonoBehaviour
    {
        [Header("Stat Labels")]
        [SerializeField] private TMP_Text _xpText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _fameText;
        [SerializeField] private TMP_Text _prestigeText;

        [Header("Settings")]
        [SerializeField] private float pollInterval = 5f;

        private SOTLApiManager _api;

        void Start()
        {
            _api = FindFirstObjectByType<SOTLApiManager>();
            SetDashes();
            StartCoroutine(PollLoop());
        }

        IEnumerator PollLoop()
        {
            while (true)
            {
                Refresh();
                yield return new WaitForSeconds(pollInterval);
            }
        }

        void Refresh()
        {
            if (_api == null) { SetDashes(); return; }

            var state = _api.CurrentState;
            if (state == null) { SetDashes(); return; }

            if (_xpText      != null) _xpText.text      = $"XP  {state.totalXp:N0}";
            if (_levelText   != null) _levelText.text   = $"LVL  {state.xpLevel}";
            if (_fameText    != null) _fameText.text    = $"FAME  {state.fame:N0}";
            if (_prestigeText != null) _prestigeText.text = $"PRESTIGE  {state.prestige:N0}";
        }

        void SetDashes()
        {
            if (_xpText       != null) _xpText.text      = "XP  —";
            if (_levelText    != null) _levelText.text   = "LVL  —";
            if (_fameText     != null) _fameText.text    = "FAME  —";
            if (_prestigeText != null) _prestigeText.text = "PRESTIGE  —";
        }

        /// <summary>Called externally (e.g. after XP pickup) to force an immediate refresh.</summary>
        public void ForceRefresh() => Refresh();
    }
}
