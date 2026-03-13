using UnityEngine;

namespace SOTL.Pickups
{
    /// <summary>
    /// Singleton. Accumulates prestige earned from pickups in PlayerPrefs and
    /// flushes the total to Wix every FLUSH_INTERVAL_SEC (default 900 = 15 min).
    ///
    /// Design:
    ///   - Pickups call AddPending(amount) — no network call per pickup.
    ///   - Timer fires every 15 min; if pending > 0, calls SOTLApiManager.AwardPrestige.
    ///   - Pending survives app restarts via PlayerPrefs (float stored as string).
    ///   - Fail-open: if the flush fails, pending is NOT cleared — retried next interval.
    /// </summary>
    public class PrestigeSyncManager : MonoBehaviour
    {
        public static PrestigeSyncManager Instance { get; private set; }

        private const string PREFS_KEY        = "sotl_pending_prestige";
        private const float  FLUSH_INTERVAL   = 900f; // 15 minutes

        private float _timer;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // Offset timer so first flush isn't immediate on session start
            _timer = FLUSH_INTERVAL;
            Debug.Log($"[SOTL] PrestigeSyncManager ready. Pending: {GetPending():F1}");
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = FLUSH_INTERVAL;
                TryFlush();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by PrestigePickup on collection.</summary>
        public void AddPending(float amount)
        {
            float current = GetPending();
            float updated = current + amount;
            PlayerPrefs.SetFloat(PREFS_KEY, updated);
            PlayerPrefs.Save();
            Debug.Log($"[SOTL] Prestige pending: +{amount} → total {updated:F1}");
        }

        /// <summary>Force an immediate flush (e.g. on clean app quit).</summary>
        public void ForceFlush() => TryFlush();

        // ── Internal ──────────────────────────────────────────────────────────

        float GetPending() => PlayerPrefs.GetFloat(PREFS_KEY, 0f);

        void TryFlush()
        {
            float pending = GetPending();
            if (pending <= 0f) return;

            var api = SOTL.API.SOTLApiManager.Instance;
            if (api == null || !api.IsLinked)
            {
                Debug.Log("[SOTL] PrestigeSyncManager: skip flush — not linked.");
                return;
            }

            Debug.Log($"[SOTL] PrestigeSyncManager: flushing {pending:F1} prestige to Wix.");

            // Optimistic clear before the call; if it fails the callback re-adds
            float toSend = pending;
            PlayerPrefs.SetFloat(PREFS_KEY, 0f);
            PlayerPrefs.Save();

            api.AwardPrestige(toSend, (success, newBalance) =>
            {
                if (success)
                {
                    Debug.Log($"[SOTL] Prestige flush OK. New balance: {newBalance:F1}");
                }
                else
                {
                    // Restore pending so next interval retries
                    float requeue = GetPending() + toSend;
                    PlayerPrefs.SetFloat(PREFS_KEY, requeue);
                    PlayerPrefs.Save();
                    Debug.LogWarning($"[SOTL] Prestige flush failed — requeued {toSend:F1}. Total pending: {requeue:F1}");
                }
            });
        }
    }
}
