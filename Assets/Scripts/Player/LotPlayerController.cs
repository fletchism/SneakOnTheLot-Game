using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace SOTL.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class LotPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float _walkSpeed   = 4f;
        [SerializeField] float _sprintSpeed = 8f;
        [SerializeField] float _gravity     = -18f;
        [SerializeField] float _mouseSensX  = 0.1f;
        [SerializeField] float _mouseSensY  = 0.1f;
        [SerializeField] float _pitchMin    = -60f;
        [SerializeField] float _pitchMax    =  60f;

        [Header("Cameras")]
        [SerializeField] CinemachineCamera _fpsCam;
        [SerializeField] CinemachineCamera _tpsCam;
        [SerializeField] Transform         _tpsPivot;

        CharacterController _cc;
        float _yaw, _pitch, _vertVel;
        bool  _isFPS = false;

        void Awake()  => _cc = GetComponent<CharacterController>();

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;
            HandleCameraToggle();
            HandleLook();
            HandleMove();
        }

        void HandleCameraToggle()
        {
            if (!Keyboard.current.vKey.wasPressedThisFrame) return;
            _isFPS = !_isFPS;
            if (_fpsCam) _fpsCam.Priority = _isFPS ? 10 : 5;
            if (_tpsCam) _tpsCam.Priority = _isFPS ?  5 : 10;
        }

        void HandleLook()
        {
            var mouseDelta = Mouse.current.delta.ReadValue();
            _yaw   += mouseDelta.x * _mouseSensX;
            _pitch -= mouseDelta.y * _mouseSensY;
            _pitch  = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            if (_tpsPivot)
                _tpsPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void HandleMove()
        {
            var kb    = Keyboard.current;
            float speed = kb.leftShiftKey.isPressed ? _sprintSpeed : _walkSpeed;

            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

            var move = (transform.right * h + transform.forward * v).normalized * speed;

            if (_cc.isGrounded && _vertVel < 0f) _vertVel = -2f;
            _vertVel += _gravity * Time.deltaTime;
            move.y = _vertVel;

            _cc.Move(move * Time.deltaTime);
        }
    }
}
