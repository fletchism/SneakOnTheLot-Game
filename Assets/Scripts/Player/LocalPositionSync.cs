using SOTL.Multiplayer;
using UnityEngine;

namespace SOTL.Player
{
    /// <summary>
    /// Sends local player position/rotation/animation state to Photon at ~10Hz.
    /// Attach to the Player GO alongside LotPlayerController.
    ///
    /// Sends float[6]: [posX, posY, posZ, rotY, moveSpeed, gait]
    /// via unreliable Photon events for low-latency position sync.
    /// </summary>
    public class LocalPositionSync : MonoBehaviour
    {
        /// <summary>Photon event code for position sync.</summary>
        public const byte EventCode = 1;

        [Header("Sync Settings")]
        [SerializeField] private float _sendRate = 10f; // Hz

        private float _sendInterval;
        private float _timer;
        private Animator _animator;

        // Animator parameter hashes
        static readonly int MoveSpeedHash  = Animator.StringToHash("MoveSpeed");
        static readonly int CurrentGaitHash = Animator.StringToHash("CurrentGait");

        void Start()
        {
            _sendInterval = 1f / _sendRate;
            _animator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _sendInterval) return;
            _timer -= _sendInterval;

            var net = LotNetworkManager.Instance;
            if (net == null || !net.IsInRoom) return;

            // Refresh animator ref in case it was swapped by LocalCharacterSync
            if (_animator == null || !_animator.enabled)
                _animator = GetComponentInChildren<Animator>();

            float moveSpeed = 0f;
            float gait = 0f;
            if (_animator != null)
            {
                moveSpeed = _animator.GetFloat(MoveSpeedHash);
                gait = _animator.GetInteger(CurrentGaitHash);
            }

            var pos = transform.position;
            var data = new float[]
            {
                pos.x, pos.y, pos.z,
                transform.eulerAngles.y,
                moveSpeed,
                gait
            };

            net.RaiseEvent(EventCode, data, reliable: false);
        }
    }
}
