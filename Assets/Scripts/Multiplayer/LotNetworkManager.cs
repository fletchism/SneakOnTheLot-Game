using System.Collections.Generic;
using UnityEngine;
using Photon.Realtime;
using SOTL.API;

namespace SOTL.Multiplayer
{
    /// <summary>
    /// Manages Photon Realtime connection lifecycle for the SOTL Unity game.
    ///
    /// Room strategy: one room per school, named by schoolId from SOTLApiManager.
    /// Students in the same school share a room and see each other move.
    /// Max players per room: 30 (one classroom). Rooms are auto-created on first join.
    ///
    /// Phase 1: Connect + join school-scoped room.
    /// Phase 2: Position sync.
    /// Phase 3: Remote player spawn/despawn.
    /// </summary>
    public class LotNetworkManager : MonoBehaviour,
        IConnectionCallbacks,
        IMatchmakingCallbacks
    {
        public static LotNetworkManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private SOTLConfig _config;

        private RealtimeClient _client;
        private bool _joiningRoom;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (_config == null)
                _config = Resources.Load<SOTLConfig>("SOTLConfig");

            if (string.IsNullOrEmpty(_config?.PhotonAppId))
            {
                Debug.LogWarning("[SOTL Net] PhotonAppId not set in SOTLConfig. Set it and restart.");
                return;
            }

            Connect();
        }

        void Update()
        {
            // Photon Realtime 5.x requires manual service tick
            _client?.Service();
        }

        void OnDestroy()
        {
            if (_client != null)
            {
                _client.RemoveCallbackTarget(this);
                if (_client.IsConnected)
                    _client.Disconnect();
            }
        }

        // ── Connection ────────────────────────────────────────────────────────

        public void Connect()
        {
            _client = new RealtimeClient();
            _client.AddCallbackTarget(this);

            var settings = new AppSettings
            {
                AppIdRealtime = _config.PhotonAppId,
                FixedRegion   = _config.PhotonRegion,
                AppVersion    = "1.0",
            };

            bool ok = _client.ConnectUsingSettings(settings);
            Debug.Log($"[SOTL Net] Connecting to Photon (region: {_config.PhotonRegion})... {(ok ? "OK" : "FAILED")}");
        }

        void JoinSchoolRoom()
        {
            // Room name = schoolId so students in the same school share a room.
            // Falls back to "sotl-default" until identity is linked.
            var api    = SOTLApiManager.Instance;
            var roomId = (api != null && api.IsLinked && !string.IsNullOrEmpty(api.CurrentState?.memberId))
                ? $"school-{api.CurrentState.memberId.Substring(0, 8)}"
                : "sotl-default";

            // TODO: replace substring heuristic with actual schoolId once
            // SOTLApiManager.CurrentState exposes it.

            var options = new RoomOptions
            {
                MaxPlayers         = 30,
                PublishUserId      = true,
                DeleteNullProperties = true,
            };

            _client.OpJoinOrCreateRoom(new EnterRoomArgs
            {
                RoomName    = roomId,
                RoomOptions = options,
            });

            _joiningRoom = false;
            Debug.Log($"[SOTL Net] Joining room: {roomId}");
        }

        // ── IConnectionCallbacks ──────────────────────────────────────────────

        public void OnConnected() { }

        public void OnConnectedToMaster()
        {
            Debug.Log("[SOTL Net] Connected to Photon master. Joining room...");
            _joiningRoom = true;
            JoinSchoolRoom();
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[SOTL Net] Disconnected: {cause}");
            // Simple reconnect on unexpected drop
            if (cause != DisconnectCause.DisconnectByClientLogic)
            {
                Invoke(nameof(Connect), 5f);
            }
        }

        public void OnRegionListReceived(RegionHandler regionHandler) { }
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
        public void OnCustomAuthenticationFailed(string debugMessage)
            => Debug.LogError($"[SOTL Net] Auth failed: {debugMessage}");

        // ── IMatchmakingCallbacks ─────────────────────────────────────────────

        public void OnJoinedRoom()
        {
            Debug.Log($"[SOTL Net] Joined room: {_client.CurrentRoom.Name} " +
                      $"({_client.CurrentRoom.PlayerCount} player(s))");
        }

        public void OnCreatedRoom()
            => Debug.Log($"[SOTL Net] Created room: {_client.CurrentRoom.Name}");

        public void OnCreateRoomFailed(short code, string message)
            => Debug.LogError($"[SOTL Net] Create room failed [{code}]: {message}");

        public void OnJoinRoomFailed(short code, string message)
            => Debug.LogError($"[SOTL Net] Join room failed [{code}]: {message}");

        public void OnJoinRandomFailed(short code, string message) { }
        public void OnLeftRoom() => Debug.Log("[SOTL Net] Left room.");

        // ── Public API ────────────────────────────────────────────────────────

        public bool IsConnected  => _client?.IsConnected  ?? false;
        public bool IsInRoom     => _client?.InRoom        ?? false;
        public int  PlayerCount  => _client?.CurrentRoom?.PlayerCount ?? 0;
    }
}
