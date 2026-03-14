using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

namespace SOTL.Player
{
    /// <summary>
    /// Third-person orbit camera. Mirrors Synty SampleCameraController pattern.
    /// Attach to a CameraRig GameObject; place Main Camera as its child.
    /// </summary>
    public class LotCameraController : MonoBehaviour
    {
        private const int LAG_DELTA = 20;

        [Header("References")]
        [SerializeField] private GameObject _character;
        [SerializeField] private Camera     _mainCamera;

        [Header("Camera Settings")]
        [SerializeField] private bool  _hideCursor       = true;
        [SerializeField] private bool  _invertCamera     = false;
        [SerializeField] private float _mouseSensitivity = 5f;
        [SerializeField] private float _cameraDistance   = 3.5f;
        [SerializeField] private float _heightOffset      = 0.5f;
        [SerializeField] private float _horizontalOffset  = 0f;
        [SerializeField] private float _tiltOffset        = 5f;
        [SerializeField] private Vector2 _tiltBounds     = new Vector2(-10f, 45f);
        [SerializeField] private float _positionalLag    = 1f;
        [SerializeField] private float _rotationalLag    = 1f;

        InputReader _inputReader;
        Transform   _playerTarget;
        Transform   _lockOnTarget;
        Transform   _camChild;

        bool  _isLockedOn;
        float _cameraInversion;
        float _newAngleX, _newAngleY;
        float _lastAngleX, _lastAngleY;
        Vector3 _newPosition, _lastPosition;

        void Start()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            _camChild = _mainCamera != null ? _mainCamera.transform : transform.GetChild(0);
            if (_character == null)
                _character = GameObject.FindWithTag("Player");
            _inputReader = _character.GetComponent<InputReader>();
            _playerTarget = _character.transform.Find("SyntyPlayer_LookAt");
            _lockOnTarget = _character.transform.Find("TargetLockOnPos");

            _cameraInversion = _invertCamera ? 1f : -1f;

            if (_hideCursor)
            {
                // Don't lock cursor if a UI overlay is active (LinkOverlay, CustomizationUI)
                var linkOverlay = Object.FindFirstObjectByType<SOTL.UI.LinkOverlay>();
                bool overlayActive = linkOverlay != null && linkOverlay.gameObject.activeSelf;
                if (!overlayActive)
                {
                    Cursor.visible   = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }

            transform.position = _playerTarget.position;
            transform.rotation = _playerTarget.rotation;
            _lastPosition      = transform.position;

            ApplyCamOffset();
        }

        void Update()
        {
            float posSpeed = 1f / (_positionalLag / LAG_DELTA);
            float rotSpeed = 1f / (_rotationalLag  / LAG_DELTA);

            _newAngleX += _inputReader._mouseDelta.y * _cameraInversion * _mouseSensitivity;
            _newAngleX  = Mathf.Clamp(_newAngleX, _tiltBounds.x, _tiltBounds.y);
            _newAngleX  = Mathf.Lerp(_lastAngleX, _newAngleX, rotSpeed * Time.deltaTime);

            if (_isLockedOn && _lockOnTarget != null)
            {
                var aim = _lockOnTarget.position - _playerTarget.position;
                var q   = Quaternion.LookRotation(aim);
                q       = Quaternion.Lerp(transform.rotation, q, rotSpeed * Time.deltaTime);
                _newAngleY = q.eulerAngles.y;
            }
            else
            {
                _newAngleY += _inputReader._mouseDelta.x * _mouseSensitivity;
                _newAngleY  = Mathf.Lerp(_lastAngleY, _newAngleY, rotSpeed * Time.deltaTime);
            }

            _newPosition       = Vector3.Lerp(_lastPosition, _playerTarget.position, posSpeed * Time.deltaTime);
            transform.position = _newPosition;
            transform.eulerAngles = new Vector3(_newAngleX, _newAngleY, 0f);

            ApplyCamOffset();

            _lastPosition = _newPosition;
            _lastAngleX   = _newAngleX;
            _lastAngleY   = _newAngleY;
        }

        void ApplyCamOffset()
        {
            _camChild.localPosition    = new Vector3(_horizontalOffset, _heightOffset, -_cameraDistance);
            _camChild.localEulerAngles = new Vector3(_tiltOffset, 0f, 0f);
        }

        public void LockOn(bool enable, Transform target)
        {
            _isLockedOn = enable;
            if (target != null) _lockOnTarget = target;
        }

        public Vector3 GetCameraForwardZeroedYNormalised()
        {
            var f = _mainCamera.transform.forward;
            return new Vector3(f.x, 0f, f.z).normalized;
        }

        public Vector3 GetCameraRightZeroedYNormalised()
        {
            var r = _mainCamera.transform.right;
            return new Vector3(r.x, 0f, r.z).normalized;
        }
    }
}
