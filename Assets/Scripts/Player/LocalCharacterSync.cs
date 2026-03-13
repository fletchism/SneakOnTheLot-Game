using SOTL.API;
using SOTL.Multiplayer;
using UnityEngine;

namespace SOTL.Player
{
    /// <summary>
    /// Sender side of character appearance sync. Attached to the local player GO.
    ///
    /// Responsibilities:
    /// 1. On session start: load saved avatar from Wix or show character creator.
    /// 2. Build/rebuild the local player's Sidekick mesh from CharacterAppearanceData.
    /// 3. Broadcast appearance to Photon custom player properties.
    /// 4. Swap LotPlayerController's Animator reference to the new mesh's Animator.
    ///
    /// Lives in Assembly-CSharp (Player folder) because it bridges SOTL.Multiplayer
    /// types (SidekickCharacterManager, LotNetworkManager) and SOTL.Player types
    /// (LotPlayerController).
    ///
    /// Phase 2: appearance only. Position sync is Phase 3.
    /// </summary>
    public class LocalCharacterSync : MonoBehaviour
    {
        [Header("State")]
        [Tooltip("Current appearance. Set via ApplyAppearance() or loaded from Wix.")]
        [SerializeField] private CharacterAppearanceData _currentAppearance;

        private GameObject _builtCharacter;
        private Animator _originalAnimator;
        private SkinnedMeshRenderer[] _originalMeshes;
        private bool _applied;
        private bool _broadcastPending;
        private bool _sessionCheckDone;

        // ── Lifecycle ─────────────────────────────────────────────────────

        void Start()
        {
            // Cache original meshes and animator so we can restore if needed
            _originalMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            _originalAnimator = GetComponent<Animator>();
        }

        void Update()
        {
            // Session start: check for saved avatar once managers are ready
            if (!_sessionCheckDone)
            {
                TrySessionCheck();
            }

            // Deferred build: wait for SidekickCharacterManager to be ready
            if (!_applied && _currentAppearance != null && _currentAppearance.parts.Count > 0)
            {
                var mgr = SidekickCharacterManager.Instance;
                if (mgr != null && mgr.IsReady)
                {
                    RebuildLocal();
                    _applied = true;
                }
            }

            // Deferred broadcast: wait for Photon room join
            if (_broadcastPending)
            {
                var net = LotNetworkManager.Instance;
                if (net != null && net.IsInRoom)
                {
                    BroadcastToPhoton();
                    _broadcastPending = false;
                }
            }
        }

        // ── Session start check ───────────────────────────────────────────

        void TrySessionCheck()
        {
            var api = SOTLApiManager.Instance;
            var mgr = SidekickCharacterManager.Instance;

            // Need both managers ready
            if (api == null || mgr == null || !mgr.IsReady) return;

            // Not linked yet — skip for now, will trigger after link
            if (!api.IsLinked)
            {
                _sessionCheckDone = true;
                // Show creator with local-only mode (no save)
                ShowCreatorIfAvailable();
                return;
            }

            // If we already have state loaded with an avatar, use it
            if (api.CurrentState != null)
            {
                HandleLoadedState(api.CurrentState);
                return;
            }

            // Fetch state from Wix
            _sessionCheckDone = true; // Prevent re-entry
            api.FetchMemberState(state => HandleLoadedState(state));
        }

        void HandleLoadedState(PlayerState state)
        {
            _sessionCheckDone = true;

            if (state != null && !string.IsNullOrEmpty(state.avatarJson))
            {
                // Saved avatar exists — apply it
                var data = CharacterAppearanceData.FromJson(state.avatarJson);
                if (data != null && data.parts.Count > 0)
                {
                    Debug.Log("[SOTL LocalSync] Loaded saved avatar from Wix.");
                    ApplyAppearance(data);
                    return;
                }
            }

            // No saved avatar — show creator
            Debug.Log("[SOTL LocalSync] No saved avatar. Opening character creator.");
            ShowCreatorIfAvailable();
        }

        void ShowCreatorIfAvailable()
        {
            var creator = Object.FindFirstObjectByType<CharacterCustomizationUI>();
            if (creator != null)
            {
                creator.Show();
            }
            else
            {
                Debug.LogWarning("[SOTL LocalSync] CharacterCustomizationUI not found in scene.");
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Apply new appearance data. Rebuilds local mesh and broadcasts to Photon.
        /// Called by the customization UI on confirm.
        /// </summary>
        public void ApplyAppearance(CharacterAppearanceData data)
        {
            if (data == null || data.parts.Count == 0)
            {
                Debug.LogWarning("[SOTL LocalSync] Cannot apply empty appearance data.");
                return;
            }

            _currentAppearance = data;

            var mgr = SidekickCharacterManager.Instance;
            if (mgr != null && mgr.IsReady)
            {
                RebuildLocal();
                _applied = true;
            }
            else
            {
                _applied = false; // Will retry in Update
            }

            _broadcastPending = true;
        }

        /// <summary>Current appearance data (read-only).</summary>
        public CharacterAppearanceData CurrentAppearance => _currentAppearance;

        // ── Local mesh rebuild ────────────────────────────────────────────

        void RebuildLocal()
        {
            var mgr = SidekickCharacterManager.Instance;
            if (mgr == null || !mgr.IsReady) return;

            // 1. Build the new character as a standalone GO
            var newChar = mgr.BuildCharacter(_currentAppearance, "LocalPlayerModel");
            if (newChar == null)
            {
                Debug.LogError("[SOTL LocalSync] BuildCharacter returned null.");
                return;
            }

            // 2. Destroy previous built character if rebuilding
            if (_builtCharacter != null)
            {
                Destroy(_builtCharacter);
            }

            // 3. Hide original meshes (keep for fallback / restore)
            if (_originalMeshes != null)
            {
                foreach (var smr in _originalMeshes)
                {
                    if (smr != null) smr.enabled = false;
                }
            }

            // 4. Disable original Animator so it doesn't conflict
            if (_originalAnimator != null)
            {
                _originalAnimator.enabled = false;
            }

            // 5. Parent new character under the player GO
            newChar.transform.SetParent(transform, false);
            newChar.transform.localPosition = Vector3.zero;
            newChar.transform.localRotation = Quaternion.identity;
            newChar.transform.localScale = Vector3.one;

            _builtCharacter = newChar;

            // 6. Point LotPlayerController at the new Animator
            var newAnimator = newChar.GetComponentInChildren<Animator>();
            if (newAnimator != null)
            {
                var playerController = GetComponent<LotPlayerController>();
                if (playerController != null)
                {
                    playerController.SwapAnimator(newAnimator);
                }
                else
                {
                    Debug.LogWarning("[SOTL LocalSync] LotPlayerController not found on player GO.");
                }
            }
            else
            {
                Debug.LogWarning("[SOTL LocalSync] No Animator on built character.");
            }

            Debug.Log("[SOTL LocalSync] Local player mesh rebuilt from Sidekick data.");
        }

        // ── Photon broadcast ──────────────────────────────────────────────

        void BroadcastToPhoton()
        {
            if (_currentAppearance == null) return;

            var net = LotNetworkManager.Instance;
            if (net == null || !net.IsInRoom) return;

            string json = _currentAppearance.ToJson();
            bool ok = net.SetLocalPlayerProperty(CharacterAppearanceData.PhotonKey, json);
            Debug.Log($"[SOTL LocalSync] Broadcast avatar ({json.Length} chars): {(ok ? "OK" : "FAILED")}");
        }
    }
}
