using UnityEngine;

namespace SOTL.Pickups
{
    /// <summary>
    /// Place on any world GameObject with a trigger Collider.
    /// The visual is FX_Pickup_Boost_01 (looping particles) — no bob/spin needed.
    /// On Player contact: queues prestige via PrestigeSyncManager, destroys this GameObject.
    /// </summary>
    public class PrestigePickup : MonoBehaviour
    {
        [Header("Prestige")]
        [SerializeField] private float prestigeAmount = 1f;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            var sync = PrestigeSyncManager.Instance
                    ?? FindFirstObjectByType<PrestigeSyncManager>();
            if (sync != null)
            {
                sync.AddPending(prestigeAmount);
                sync.ForceFlush();   // immediate Wix call — don't wait for 15-min batch
            }
            else
                Debug.LogWarning("[SOTL] PrestigePickup: PrestigeSyncManager not found.");

            Debug.Log($"[SOTL] Prestige pickup collected. Queued: {prestigeAmount}");
            Destroy(gameObject);
        }
    }
}
