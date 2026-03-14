using System.Collections.Generic;
using Photon.Client;
using Photon.Realtime;
using UnityEngine;

namespace SOTL.Multiplayer
{
    /// <summary>
    /// Manages remote player GameObjects.
    /// Phase 2: spawns/rebuilds from avatar properties.
    /// Phase 3: receives position events, interpolates movement, drives animator.
    /// </summary>
    public class RemotePlayerManager : MonoBehaviour, IInRoomCallbacks, IOnEventCallback, IMatchmakingCallbacks
    {
        public static RemotePlayerManager Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 _spawnOrigin = new Vector3(0f, 0f, 5f);
        [SerializeField] private float _spawnSpacing = 2f;

        [Header("Interpolation")]
        [SerializeField] private float _positionLerpSpeed = 12f;
        [SerializeField] private float _rotationLerpSpeed = 12f;

        /// <summary>Position sync event code. Must match LocalPositionSync.EventCode.</summary>
        const byte PositionEventCode = 1;

        // Animator hashes (must match LotPlayerController)
        static readonly int MoveSpeedHash  = Animator.StringToHash("MoveSpeed");
        static readonly int CurrentGaitHash = Animator.StringToHash("CurrentGait");
        static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");
        static readonly int IsStoppedHash   = Animator.StringToHash("IsStopped");
        static readonly int IsStrafingHash  = Animator.StringToHash("IsStrafing");

        /// <summary>ActorNumber → remote player GameObject.</summary>
        private readonly Dictionary<int, GameObject> _remotePlayers = new Dictionary<int, GameObject>();

        /// <summary>ActorNumber → target position/rotation for interpolation.</summary>
        private readonly Dictionary<int, RemoteState> _remoteStates = new Dictionary<int, RemoteState>();

        private bool _registered;

        struct RemoteState
        {
            public Vector3 targetPos;
            public float targetRotY;
            public float moveSpeed;
            public int gait;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start() => TryRegister();

        void Update()
        {
            if (!_registered) TryRegister();
            InterpolateAll();
        }

        void OnDestroy()
        {
            if (_registered)
            {
                var net = LotNetworkManager.Instance;
                if (net != null) net.UnregisterCallbacks(this);
            }

            foreach (var kvp in _remotePlayers)
                if (kvp.Value != null) Destroy(kvp.Value);
            _remotePlayers.Clear();
            _remoteStates.Clear();
        }

        void TryRegister()
        {
            var net = LotNetworkManager.Instance;
            if (net == null || !net.IsConnected) return;

            net.RegisterCallbacks(this);
            _registered = true;
            Debug.Log("[SOTL Remote] Registered for Photon callbacks.");
        }

        // ── Process existing players on late join ─────────────────────────

        void ProcessExistingPlayers()
        {
            var net = LotNetworkManager.Instance;
            if (net == null || !net.IsInRoom) { Debug.Log("[SOTL Remote] ProcessExisting: not in room yet."); return; }

            var players = net.GetRoomPlayers();
            if (players == null) { Debug.Log("[SOTL Remote] ProcessExisting: no player list."); return; }

            int remoteCount = 0;
            foreach (var kvp in players)
            {
                var player = kvp.Value;
                if (player.IsLocal) continue;
                remoteCount++;

                Debug.Log($"[SOTL Remote] Found remote player {player.ActorNumber}, has avatar: {player.CustomProperties.ContainsKey(CharacterAppearanceData.PhotonKey)}");

                if (player.CustomProperties.TryGetValue(CharacterAppearanceData.PhotonKey, out var avatarJson))
                    SpawnOrRebuild(player.ActorNumber, avatarJson as string);
            }

            Debug.Log($"[SOTL Remote] ProcessExisting done. {remoteCount} remote player(s) found.");
        }

        // ── IInRoomCallbacks ──────────────────────────────────────────────

        public void OnPlayerEnteredRoom(Player newPlayer)
            => Debug.Log($"[SOTL Remote] Player entered: {newPlayer.ActorNumber}");

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

        // ── IMatchmakingCallbacks ─────────────────────────────────────────

        public void OnJoinedRoom()
        {
            Debug.Log("[SOTL Remote] Joined room — scanning for existing players.");
            ProcessExistingPlayers();
        }

        public void OnCreatedRoom() { }
        public void OnCreateRoomFailed(short code, string message) { }
        public void OnJoinRoomFailed(short code, string message) { }
        public void OnJoinRandomFailed(short code, string message) { }
        public void OnLeftRoom() { }
        public void OnFriendListUpdate(System.Collections.Generic.List<FriendInfo> friendList) { }
        public void OnMasterClientSwitched(Player newMasterClient) { }

        // ── IOnEventCallback ──────────────────────────────────────────────

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != PositionEventCode) return;

            int sender = photonEvent.Sender;

            // Ignore own events
            var net = LotNetworkManager.Instance;
            if (net != null && sender == net.LocalActorNumber) return;

            if (photonEvent.CustomData is float[] data && data.Length >= 6)
            {
                _remoteStates[sender] = new RemoteState
                {
                    targetPos  = new Vector3(data[0], data[1], data[2]),
                    targetRotY = data[3],
                    moveSpeed  = data[4],
                    gait       = (int)data[5]
                };

                // If we have a GO but no state yet, snap to first position
                if (_remotePlayers.TryGetValue(sender, out var go) && go != null)
                {
                    if (Vector3.Distance(go.transform.position, _remoteStates[sender].targetPos) > 10f)
                    {
                        // Snap if too far (first update or teleport)
                        go.transform.position = _remoteStates[sender].targetPos;
                        go.transform.eulerAngles = new Vector3(0f, _remoteStates[sender].targetRotY, 0f);
                    }
                }
            }
        }

        // ── Interpolation ─────────────────────────────────────────────────

        void InterpolateAll()
        {
            float dt = Time.deltaTime;

            foreach (var kvp in _remoteStates)
            {
                int actorId = kvp.Key;
                var state = kvp.Value;

                if (!_remotePlayers.TryGetValue(actorId, out var go)) continue;
                if (go == null) continue;

                // Position
                go.transform.position = Vector3.Lerp(
                    go.transform.position, state.targetPos, _positionLerpSpeed * dt);

                // Rotation
                var currentRot = go.transform.eulerAngles;
                float newY = Mathf.LerpAngle(currentRot.y, state.targetRotY, _rotationLerpSpeed * dt);
                go.transform.eulerAngles = new Vector3(0f, newY, 0f);

                // Animator
                var animator = go.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.SetFloat(MoveSpeedHash, state.moveSpeed);
                    animator.SetInteger(CurrentGaitHash, state.gait);
                    animator.SetBool(IsGroundedHash, true);
                    animator.SetBool(IsStoppedHash, state.moveSpeed < 0.1f);
                    animator.SetFloat(IsStrafingHash, 1f);
                }
            }
        }

        // ── Spawn / rebuild / remove ──────────────────────────────────────

        void SpawnOrRebuild(int actorNumber, string avatarJson)
        {
            var mgr = SidekickCharacterManager.Instance;
            if (mgr == null || !mgr.IsReady)
            {
                Debug.LogWarning($"[SOTL Remote] SidekickCharacterManager not ready for actor {actorNumber}.");
                return;
            }

            var data = CharacterAppearanceData.FromJson(avatarJson);
            if (data == null)
            {
                Debug.LogWarning($"[SOTL Remote] Invalid avatar data from actor {actorNumber}.");
                return;
            }

            string modelName = $"RemotePlayer_{actorNumber}";
            _remotePlayers.TryGetValue(actorNumber, out var existing);
            if (existing != null && existing.Equals(null)) existing = null;

            var character = mgr.BuildCharacter(data, modelName, existing);
            if (character == null)
            {
                Debug.LogWarning($"[SOTL Remote] Failed to build character for actor {actorNumber}.");
                return;
            }

            // Set correct gendered animator + initial grounded state
            var animator = character.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var correctController = mgr.GetAnimatorController(data.isFeminine);
                if (correctController != null)
                    animator.runtimeAnimatorController = correctController;

                animator.SetBool(IsGroundedHash, true);
                animator.SetBool(IsStoppedHash, true);
                animator.SetFloat(MoveSpeedHash, 0f);
            }

            // If we have a received position, snap to it. Otherwise use spawn slot.
            if (_remoteStates.TryGetValue(actorNumber, out var state))
            {
                character.transform.position = state.targetPos;
                character.transform.eulerAngles = new Vector3(0f, state.targetRotY, 0f);
            }
            else if (existing == null)
            {
                int slot = _remotePlayers.Count;
                character.transform.position = _spawnOrigin + Vector3.right * (slot * _spawnSpacing);
                character.transform.rotation = Quaternion.identity;
            }

            _remotePlayers[actorNumber] = character;
            Debug.Log($"[SOTL Remote] Spawned/rebuilt remote player {actorNumber}");
        }

        void RemoveRemotePlayer(int actorNumber)
        {
            if (_remotePlayers.TryGetValue(actorNumber, out var go))
            {
                if (go != null) Destroy(go);
                _remotePlayers.Remove(actorNumber);
            }
            _remoteStates.Remove(actorNumber);
            Debug.Log($"[SOTL Remote] Removed remote player {actorNumber}.");
        }

        // ── Public API ────────────────────────────────────────────────────

        public int RemotePlayerCount => _remotePlayers.Count;
    }
}
