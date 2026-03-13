using System.Collections.Generic;
using Photon.Client;
using Photon.Realtime;
using UnityEngine;

namespace SOTL.Multiplayer
{
    /// <summary>
    /// Manages remote player GameObjects based on Photon player property updates.
    /// Spawns/rebuilds remote characters when their "avatar" property changes.
    /// Destroys remote GameObjects when players leave.
    ///
    /// Phase 2: appearance sync only. Position sync comes in Phase 3.
    /// Attach to a GameObject under [Managers].
    /// </summary>
    public class RemotePlayerManager : MonoBehaviour, IInRoomCallbacks
    {
        public static RemotePlayerManager Instance { get; private set; }

        [Header("Spawn Settings")]
        [Tooltip("Base position for spawning remote players. Each gets an offset.")]
        [SerializeField] private Vector3 _spawnOrigin = new Vector3(0f, 0f, 5f);

        [Tooltip("Spacing between remote player spawns.")]
        [SerializeField] private float _spawnSpacing = 2f;

        /// <summary>ActorNumber → remote player GameObject.</summary>
        private readonly Dictionary<int, GameObject> _remotePlayers = new Dictionary<int, GameObject>();

        private bool _registered;

        // ── Lifecycle ─────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            TryRegister();
        }

        void Update()
        {
            // Retry registration if LotNetworkManager wasn't ready at Start
            if (!_registered) TryRegister();
        }

        void OnDestroy()
        {
            if (_registered)
            {
                var net = LotNetworkManager.Instance;
                if (net != null) net.UnregisterCallbacks(this);
            }

            // Clean up remote player objects
            foreach (var kvp in _remotePlayers)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _remotePlayers.Clear();
        }

        void TryRegister()
        {
            var net = LotNetworkManager.Instance;
            if (net == null || !net.IsConnected) return;

            net.RegisterCallbacks(this);
            _registered = true;
            Debug.Log("[SOTL Remote] Registered for Photon in-room callbacks.");

            // Process any players already in the room
            ProcessExistingPlayers();
        }

        // ── Process existing players on late join ─────────────────────────

        void ProcessExistingPlayers()
        {
            var net = LotNetworkManager.Instance;
            if (net == null || !net.IsInRoom) return;

            var players = net.GetRoomPlayers();
            if (players == null) return;

            foreach (var kvp in players)
            {
                var player = kvp.Value;
                if (player.IsLocal) continue;

                if (player.CustomProperties.TryGetValue(CharacterAppearanceData.PhotonKey, out var avatarJson))
                {
                    SpawnOrRebuild(player.ActorNumber, avatarJson as string);
                }
            }
        }

        // ── IInRoomCallbacks ──────────────────────────────────────────────

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[SOTL Remote] Player entered: {newPlayer.ActorNumber} ({newPlayer.NickName})");
            // Their avatar property will arrive via OnPlayerPropertiesUpdate
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[SOTL Remote] Player left: {otherPlayer.ActorNumber}");
            RemoveRemotePlayer(otherPlayer.ActorNumber);
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps)
        {
            if (targetPlayer.IsLocal) return;

            if (changedProps.TryGetValue(CharacterAppearanceData.PhotonKey, out var avatarJson))
            {
                Debug.Log($"[SOTL Remote] Avatar update from player {targetPlayer.ActorNumber}");
                SpawnOrRebuild(targetPlayer.ActorNumber, avatarJson as string);
            }
        }

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) { }
        public void OnMasterClientSwitched(Player newMasterClient) { }

        // ── Spawn / rebuild / remove ──────────────────────────────────────

        void SpawnOrRebuild(int actorNumber, string avatarJson)
        {
            var mgr = SidekickCharacterManager.Instance;
            if (mgr == null || !mgr.IsReady)
            {
                Debug.LogWarning($"[SOTL Remote] SidekickCharacterManager not ready, deferring spawn for actor {actorNumber}.");
                return;
            }

            var data = CharacterAppearanceData.FromJson(avatarJson);
            if (data == null)
            {
                Debug.LogWarning($"[SOTL Remote] Invalid avatar data from actor {actorNumber}.");
                return;
            }

            string modelName = $"RemotePlayer_{actorNumber}";

            // Check for existing
            _remotePlayers.TryGetValue(actorNumber, out var existing);

            // If existing was destroyed externally, clear the reference
            if (existing != null && existing.Equals(null)) existing = null;

            var character = mgr.BuildCharacter(data, modelName, existing);
            if (character == null)
            {
                Debug.LogWarning($"[SOTL Remote] Failed to build character for actor {actorNumber}.");
                return;
            }

            // Position: Phase 2 uses static placement. Phase 3 replaces with live sync.
            if (existing == null)
            {
                int slot = _remotePlayers.Count;
                character.transform.position = _spawnOrigin + Vector3.right * (slot * _spawnSpacing);
                character.transform.rotation = Quaternion.identity;
            }

            _remotePlayers[actorNumber] = character;
            Debug.Log($"[SOTL Remote] Spawned/rebuilt remote player {actorNumber} at {character.transform.position}");
        }

        void RemoveRemotePlayer(int actorNumber)
        {
            if (_remotePlayers.TryGetValue(actorNumber, out var go))
            {
                if (go != null) Destroy(go);
                _remotePlayers.Remove(actorNumber);
                Debug.Log($"[SOTL Remote] Removed remote player {actorNumber}.");
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Number of currently tracked remote players.</summary>
        public int RemotePlayerCount => _remotePlayers.Count;
    }
}
