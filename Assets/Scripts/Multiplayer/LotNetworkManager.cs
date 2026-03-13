using UnityEngine;

namespace SOTL.Multiplayer
{
    /// <summary>
    /// Photon Realtime network manager — stub.
    /// Wires in after Photon SDK is imported and App ID is configured.
    /// Responsibilities:
    ///   - Connect to Photon cloud using App ID from SOTLConfig
    ///   - Join or create a room per school (room name = schoolId)
    ///   - Broadcast local player position/rotation to other clients
    ///   - Spawn remote player representations for other connected students
    /// </summary>
    public class LotNetworkManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string appId = ""; // Set after Photon account created
        [SerializeField] private string gameVersion = "1.0";
        [SerializeField] private string region = "us"; // Photon region

        void Start()
        {
            Debug.Log("[SOTL Net] LotNetworkManager stub loaded. Pending Photon App ID.");
        }

        // TODO — Phase 1: Connect + room join
        // TODO — Phase 2: Position sync (SendRate-limited, not per-frame)
        // TODO — Phase 3: Remote player spawn/despawn
        // TODO — Phase 4: School-scoped rooms (room name = schoolId from SOTLApiManager)
    }
}
