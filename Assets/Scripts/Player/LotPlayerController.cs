using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using System.Collections.Generic;
using UnityEngine;

namespace SOTL.Player
{
    /// <summary>
    /// Full character controller mirroring Synty SamplePlayerAnimationController.
    /// Requires: CharacterController, InputReader, Animator on same GameObject.
    /// Requires LotCameraController on the CameraRig.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(InputReader))]
    public class LotPlayerController : MonoBehaviour
    {
        // ── Animator hashes ───────────────────────────────────────────
        readonly int _movementInputTappedHash  = Animator.StringToHash("MovementInputTapped");
        readonly int _movementInputPressedHash = Animator.StringToHash("MovementInputPressed");
        readonly int _movementInputHeldHash    = Animator.StringToHash("MovementInputHeld");
        readonly int _shuffleDirectionXHash    = Animator.StringToHash("ShuffleDirectionX");
        readonly int _shuffleDirectionZHash    = Animator.StringToHash("ShuffleDirectionZ");
        readonly int _moveSpeedHash            = Animator.StringToHash("MoveSpeed");
        readonly int _currentGaitHash          = Animator.StringToHash("CurrentGait");
        readonly int _isJumpingHash            = Animator.StringToHash("IsJumping");
        readonly int _fallingDurationHash      = Animator.StringToHash("FallingDuration");
        readonly int _inclineAngleHash         = Animator.StringToHash("InclineAngle");
        readonly int _strafeDirectionXHash     = Animator.StringToHash("StrafeDirectionX");
        readonly int _strafeDirectionZHash     = Animator.StringToHash("StrafeDirectionZ");
        readonly int _forwardStrafeHash        = Animator.StringToHash("ForwardStrafe");
        readonly int _cameraRotationOffsetHash = Animator.StringToHash("CameraRotationOffset");
        readonly int _isStrafingHash           = Animator.StringToHash("IsStrafing");
        readonly int _isTurningInPlaceHash     = Animator.StringToHash("IsTurningInPlace");
        readonly int _isCrouchingHash          = Animator.StringToHash("IsCrouching");
        readonly int _isWalkingHash            = Animator.StringToHash("IsWalking");
        readonly int _isStoppedHash            = Animator.StringToHash("IsStopped");
        readonly int _isStartingHash           = Animator.StringToHash("IsStarting");
        readonly int _isGroundedHash           = Animator.StringToHash("IsGrounded");
        readonly int _leanValueHash            = Animator.StringToHash("LeanValue");
        readonly int _headLookXHash            = Animator.StringToHash("HeadLookX");
        readonly int _headLookYHash            = Animator.StringToHash("HeadLookY");
        readonly int _bodyLookXHash            = Animator.StringToHash("BodyLookX");
        readonly int _bodyLookYHash            = Animator.StringToHash("BodyLookY");
        readonly int _locomotionStartDirHash   = Animator.StringToHash("LocomotionStartDirection");

        // ── Inspector fields ──────────────────────────────────────────
        [Header("External")]
        [SerializeField] LotCameraController _cameraController;
        [SerializeField] Animator            _animator;

        [Header("Locomotion")]
        [SerializeField] bool  _alwaysStrafe       = true;
        [SerializeField] float _walkSpeed          = 1.4f;
        [SerializeField] float _runSpeed           = 2.5f;
        [SerializeField] float _sprintSpeed        = 7f;
        [SerializeField] float _speedChangeDamping = 10f;
        [SerializeField] float _rotationSmoothing  = 10f;
        [SerializeField] float _cameraRotationOffset;

        [Header("Capsule")]
        [SerializeField] float _capsuleStandHeight  = 1.8f;
        [SerializeField] float _capsuleStandCentre  = 0.93f;
        [SerializeField] float _capsuleCrouchHeight = 1.2f;
        [SerializeField] float _capsuleCrouchCentre = 0.6f;

        [Header("Strafing")]
        [SerializeField] float _forwardStrafeMin = -55f;
        [SerializeField] float _forwardStrafeMax = 125f;
        [SerializeField] float _forwardStrafe    = 1f;

        [Header("Grounded")]
        [SerializeField] Transform  _rearRayPos;
        [SerializeField] Transform  _frontRayPos;
        [SerializeField] LayerMask  _groundLayerMask;
        [SerializeField] float      _groundedOffset = -0.14f;

        [Header("In-Air")]
        [SerializeField] float _jumpForce       = 10f;
        [SerializeField] float _gravityMultiplier = 2f;

        [Header("Head/Body Look")]
        [SerializeField] bool           _enableHeadTurn = true;
        [SerializeField] bool           _enableBodyTurn = true;
        [SerializeField] AnimationCurve _headLookXCurve;
        [SerializeField] AnimationCurve _bodyLookXCurve;
        [SerializeField] bool           _enableLean = true;
        [SerializeField] AnimationCurve _leanCurve;

        [Header("Shuffle")]
        [SerializeField] float _buttonHoldThreshold = 0.15f;

        // ── Runtime state ─────────────────────────────────────────────
        enum AnimState  { Base, Locomotion, Jump, Fall, Crouch }
        enum GaitState  { Idle, Walk, Run, Sprint }

        InputReader  _inputReader;
        CharacterController _controller;

        AnimState _state = AnimState.Base;
        GaitState _currentGait;

        bool  _isGrounded = true, _isCrouching, _isSprinting, _isWalking;
        bool  _isStrafing, _isLockedOn, _isAiming, _isStarting, _isStopped = true;
        bool  _isTurningInPlace, _isSliding, _cannotStandUp, _crouchKeyPressed;
        bool  _movementInputTapped, _movementInputPressed, _movementInputHeld;

        float _speed2D, _currentMaxSpeed, _targetMaxSpeed;
        float _fallingDuration, _inclineAngle;
        float _strafeDirectionX, _strafeDirectionZ, _strafeAngle;
        float _shuffleDirectionX, _shuffleDirectionZ;
        float _leanValue, _headLookX, _headLookY, _bodyLookX, _bodyLookY;
        float _leanDelay, _headLookDelay, _bodyLookDelay, _leansHeadLooksDelay;
        float _locomotionStartDirection, _locomotionStartTimer;
        float _newDirectionDifferenceAngle, _lookingAngle, _rotationRate;
        float _fallStartTime, _initialLeanValue, _initialTurnValue;
        float _cameraRotationOffsetField;

        Vector3 _velocity, _moveDirection, _targetVelocity;
        Vector3 _currentRotation, _previousRotation, _cameraForward;

        Transform _targetLockOnPos;

        Vector3 CamForward() {
            if (_cameraController != null) return _cameraController.GetCameraForwardZeroedYNormalised();
            if (Camera.main == null) return transform.forward;
            var f = Camera.main.transform.forward; return new Vector3(f.x, 0f, f.z).normalized;
        }
        Vector3 CamRight() {
            if (_cameraController != null) return _cameraController.GetCameraRightZeroedYNormalised();
            if (Camera.main == null) return transform.right;
            var r = Camera.main.transform.right; return new Vector3(r.x, 0f, r.z).normalized;
        }

        const float ANIM_DAMP  = 5f;
        const float STRAFE_DAMP = 20f;

        // ── Lifecycle ─────────────────────────────────────────────────

        void Start()
        {
            _inputReader = GetComponent<InputReader>();
            _controller  = GetComponent<CharacterController>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null) Debug.LogError("[LotPlayerController] No Animator found. Player animations will not work.", this);
            if (_cameraController == null)
                _cameraController = Object.FindFirstObjectByType<LotCameraController>();
            _targetLockOnPos = transform.Find("TargetLockOnPos");

            _inputReader.onWalkToggled     += ToggleWalk;
            _inputReader.onSprintActivated  += ActivateSprint;
            _inputReader.onSprintDeactivated += DeactivateSprint;
            _inputReader.onCrouchActivated  += ActivateCrouch;
            _inputReader.onCrouchDeactivated += DeactivateCrouch;
            _inputReader.onAimActivated     += ActivateAim;
            _inputReader.onAimDeactivated   += DeactivateAim;
            _inputReader.onLockOnToggled    += ToggleLockOn;
            _inputReader.onJumpPerformed    += Jump;

            _isStrafing = _alwaysStrafe;
            SwitchState(AnimState.Locomotion);
        }

        void Update()
        {
            switch (_state)
            {
                case AnimState.Locomotion: UpdateLocomotion(); break;
                case AnimState.Jump:       UpdateJump();       break;
                case AnimState.Fall:       UpdateFall();       break;
                case AnimState.Crouch:     UpdateCrouch();     break;
            }
        }

        // ── State machine ─────────────────────────────────────────────

        void SwitchState(AnimState next) { ExitState(); EnterState(next); }

        void EnterState(AnimState s)
        {
            _state = s;
            switch (s)
            {
                case AnimState.Locomotion: _previousRotation = transform.forward; break;
                case AnimState.Jump:       EnterJump(); break;
                case AnimState.Fall:       _fallStartTime = Time.time; break;
                case AnimState.Crouch:     CapsuleCrouchSize(true); break;
            }
        }

        void ExitState()
        {
            if (_animator == null) return;
            switch (_state)
            {
                case AnimState.Jump:   _animator.SetBool(_isJumpingHash, false); break;
                case AnimState.Crouch: CapsuleCrouchSize(false); break;
            }
        }

        // ── Input callbacks ───────────────────────────────────────────

        void ToggleWalk()     { _isWalking = !_isWalking && _isGrounded && !_isSprinting; }
        void ActivateSprint() { if (!_isCrouching) { _isWalking = false; _isSprinting = true; _isStrafing = false; } }
        void DeactivateSprint() { _isSprinting = false; _isStrafing = _alwaysStrafe || _isAiming || _isLockedOn; }
        void ActivateAim()    { _isAiming = true;  _isStrafing = !_isSprinting; }
        void DeactivateAim()  { _isAiming = false; _isStrafing = !_isSprinting && (_alwaysStrafe || _isLockedOn); }

        void ToggleLockOn()
        {
            _isLockedOn = !_isLockedOn;
            _isStrafing = _isLockedOn ? !_isSprinting : _alwaysStrafe || _isAiming;
            if (_cameraController != null) _cameraController.LockOn(_isLockedOn, _targetLockOnPos);
        }

        void ActivateCrouch()
        {
            _crouchKeyPressed = true;
            if (_isGrounded) { CapsuleCrouchSize(true); DeactivateSprint(); _isCrouching = true; }
        }

        void DeactivateCrouch()
        {
            _crouchKeyPressed = false;
            if (!_cannotStandUp && !_isSliding) { CapsuleCrouchSize(false); _isCrouching = false; }
        }

        void Jump()
        {
            if (_isGrounded && _state == AnimState.Locomotion)
                SwitchState(AnimState.Jump);
        }

        void CapsuleCrouchSize(bool crouching)
        {
            _controller.center = new Vector3(0f, crouching ? _capsuleCrouchCentre : _capsuleStandCentre, 0f);
            _controller.height = crouching ? _capsuleCrouchHeight : _capsuleStandHeight;
        }

        // ── Locomotion state ──────────────────────────────────────────

        void UpdateLocomotion()
        {
            CheckGrounded();
            CalculateMoveDirection();
            FaceMoveDirection();
            CheckIfStopped();
            CheckIfStarting();
            ApplyGravity();
            Move();
            UpdateAnimator();

            if (!_isGrounded) SwitchState(AnimState.Fall);
        }

        void CalculateInput()
        {
            if (_inputReader._movementInputDetected)
            {
                if (_inputReader._movementInputDuration == 0)          { _movementInputTapped = true; _movementInputPressed = false; _movementInputHeld = false; }
                else if (_inputReader._movementInputDuration < _buttonHoldThreshold) { _movementInputTapped = false; _movementInputPressed = true; _movementInputHeld = false; }
                else                                                    { _movementInputTapped = false; _movementInputPressed = false; _movementInputHeld = true; }
                _inputReader._movementInputDuration += Time.deltaTime;
            }
            else
            {
                _inputReader._movementInputDuration = 0;
                _movementInputTapped = _movementInputPressed = _movementInputHeld = false;
            }

            _moveDirection = (CamForward() * _inputReader._moveComposite.y)
                           + (CamRight()   * _inputReader._moveComposite.x);
        }

        void CalculateMoveDirection()
        {
            CalculateInput();

            if      (!_isGrounded) _targetMaxSpeed = _currentMaxSpeed;
            else if (_isCrouching || _isWalking) _targetMaxSpeed = _walkSpeed;
            else if (_isSprinting) _targetMaxSpeed = _sprintSpeed;
            else                   _targetMaxSpeed = _runSpeed;

            _currentMaxSpeed = Mathf.Lerp(_currentMaxSpeed, _targetMaxSpeed, ANIM_DAMP * Time.deltaTime);

            _targetVelocity.x = _moveDirection.x * _currentMaxSpeed;
            _targetVelocity.z = _moveDirection.z * _currentMaxSpeed;
            _velocity.x = Mathf.Lerp(_velocity.x, _targetVelocity.x, _speedChangeDamping * Time.deltaTime);
            _velocity.z = Mathf.Lerp(_velocity.z, _targetVelocity.z, _speedChangeDamping * Time.deltaTime);

            _speed2D = Mathf.Round(new Vector3(_velocity.x, 0f, _velocity.z).magnitude * 1000f) / 1000f;

            var pf = transform.forward;
            _newDirectionDifferenceAngle = pf != _moveDirection ? Vector3.SignedAngle(pf, _moveDirection, Vector3.up) : 0f;

            CalculateGait();
        }

        void CalculateGait()
        {
            float runT = (_walkSpeed + _runSpeed) / 2f;
            float sprintT = (_runSpeed + _sprintSpeed) / 2f;

            if      (_speed2D < 0.01f)  _currentGait = GaitState.Idle;
            else if (_speed2D < runT)   _currentGait = GaitState.Walk;
            else if (_speed2D < sprintT) _currentGait = GaitState.Run;
            else                        _currentGait = GaitState.Sprint;
        }

        void FaceMoveDirection()
        {
            var charFwd   = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            var charRight = new Vector3(transform.right.x,   0f, transform.right.z).normalized;
            var dirFwd    = new Vector3(_moveDirection.x,    0f, _moveDirection.z).normalized;

            _cameraForward = CamForward();
            var strafingRot = Quaternion.LookRotation(_cameraForward);

            _strafeAngle = charFwd != dirFwd ? Vector3.SignedAngle(charFwd, dirFwd, Vector3.up) : 0f;
            _isTurningInPlace = false;

            if (_isStrafing)
            {
                if (_moveDirection.magnitude > 0.01f)
                {
                    _shuffleDirectionZ = Vector3.Dot(charFwd, dirFwd);
                    _shuffleDirectionX = Vector3.Dot(charRight, dirFwd);
                    UpdateStrafeDir(Vector3.Dot(charFwd, dirFwd), Vector3.Dot(charRight, dirFwd));
                    _cameraRotationOffset = Mathf.Lerp(_cameraRotationOffset, 0f, _rotationSmoothing * Time.deltaTime);

                    float target = _strafeAngle > _forwardStrafeMin && _strafeAngle < _forwardStrafeMax ? 1f : 0f;
                    _forwardStrafe = Mathf.Abs(_forwardStrafe - target) <= 0.001f ? target
                        : Mathf.SmoothStep(_forwardStrafe, target, Mathf.Clamp01(STRAFE_DAMP * Time.deltaTime));

                    transform.rotation = Quaternion.Slerp(transform.rotation, strafingRot, _rotationSmoothing * Time.deltaTime);
                }
                else
                {
                    UpdateStrafeDir(1f, 0f);
                    float newOffset = charFwd != _cameraForward ? Vector3.SignedAngle(charFwd, _cameraForward, Vector3.up) : 0f;
                    _cameraRotationOffset = Mathf.Lerp(_cameraRotationOffset, newOffset, 20f * Time.deltaTime);
                    _isTurningInPlace = Mathf.Abs(_cameraRotationOffset) > 10f;

                    // Rotate character toward camera so the offset closes and the turn animation completes
                    if (_isTurningInPlace)
                        transform.rotation = Quaternion.Slerp(transform.rotation, strafingRot, _rotationSmoothing * Time.deltaTime);
                }
            }
            else
            {
                UpdateStrafeDir(1f, 0f);
                _cameraRotationOffset = Mathf.Lerp(_cameraRotationOffset, 0f, _rotationSmoothing * Time.deltaTime);
                _shuffleDirectionZ = 1f; _shuffleDirectionX = 0f;

                var faceDir = new Vector3(_velocity.x, 0f, _velocity.z);
                if (faceDir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), _rotationSmoothing * Time.deltaTime);
            }
        }

        void UpdateStrafeDir(float z, float x)
        {
            _strafeDirectionZ = Mathf.Round(Mathf.Lerp(_strafeDirectionZ, z, ANIM_DAMP * Time.deltaTime) * 1000f) / 1000f;
            _strafeDirectionX = Mathf.Round(Mathf.Lerp(_strafeDirectionX, x, ANIM_DAMP * Time.deltaTime) * 1000f) / 1000f;
        }

        void CheckIfStopped()  { _isStopped = _moveDirection.magnitude == 0f && _speed2D < 0.5f; }

        void CheckIfStarting()
        {
            if (_animator == null) return;
            _locomotionStartTimer = _locomotionStartTimer > 0f ? _locomotionStartTimer - Time.deltaTime : 0f;
            bool check = false;
            if (_locomotionStartTimer <= 0f)
            {
                check = _moveDirection.magnitude > 0.01f && _speed2D < 1f && !_isStrafing;
                if (check)
                {
                    if (!_isStarting) { _locomotionStartDirection = _newDirectionDifferenceAngle; _animator.SetFloat(_locomotionStartDirHash, _locomotionStartDirection); }
                    _leanDelay = _headLookDelay = _bodyLookDelay = 0.2f;
                    _locomotionStartTimer = 0.2f;
                }
            }
            else check = true;
            _isStarting = check;
            _animator.SetBool(_isStartingHash, _isStarting);
        }

        // ── Jump / Fall / Crouch stubs (expand later) ─────────────────

        void EnterJump()
        {
            if (_animator == null) return;
            _velocity.y = _jumpForce;
            _animator.SetBool(_isJumpingHash, true);
        }

        void UpdateJump()
        {
            ApplyGravity(); Move(); UpdateAnimator();
            CheckGrounded();
            if (_isGrounded) SwitchState(AnimState.Locomotion);
            else if (_velocity.y < 0f) SwitchState(AnimState.Fall);
        }

        void UpdateFall()
        {
            _fallingDuration = Time.time - _fallStartTime;
            ApplyGravity(); Move(); UpdateAnimator();
            CheckGrounded();
            if (_isGrounded) { _fallingDuration = 0f; SwitchState(AnimState.Locomotion); }
        }

        void UpdateCrouch() { CalculateMoveDirection(); FaceMoveDirection(); ApplyGravity(); Move(); UpdateAnimator(); }

        // ── Physics ───────────────────────────────────────────────────

        void CheckGrounded()
        {
            var p = transform.position;
            var mask = _groundLayerMask.value == 0 ? Physics.DefaultRaycastLayers : (int)_groundLayerMask;
            _isGrounded = Physics.CheckSphere(new Vector3(p.x, p.y + _groundedOffset, p.z), 0.28f, mask, QueryTriggerInteraction.Ignore);
        }

        void ApplyGravity()
        {
            if (_velocity.y > Physics.gravity.y)
                _velocity.y += Physics.gravity.y * _gravityMultiplier * Time.deltaTime;
        }

        void Move() { _controller.Move(_velocity * Time.deltaTime); }

        // ── Animator sync ─────────────────────────────────────────────

        void UpdateAnimator()
        {
            if (_animator == null) return;
            _animator.SetFloat(_leanValueHash,            _leanValue);
            _animator.SetFloat(_headLookXHash,            _headLookX);
            _animator.SetFloat(_headLookYHash,            _headLookY);
            _animator.SetFloat(_bodyLookXHash,            _bodyLookX);
            _animator.SetFloat(_bodyLookYHash,            _bodyLookY);
            _animator.SetFloat(_isStrafingHash,           _isStrafing ? 1f : 0f);
            _animator.SetFloat(_inclineAngleHash,         _inclineAngle);
            _animator.SetFloat(_moveSpeedHash,            _speed2D);
            _animator.SetInteger(_currentGaitHash,        (int)_currentGait);
            _animator.SetFloat(_strafeDirectionXHash,     _strafeDirectionX);
            _animator.SetFloat(_strafeDirectionZHash,     _strafeDirectionZ);
            _animator.SetFloat(_forwardStrafeHash,        _forwardStrafe);
            _animator.SetFloat(_cameraRotationOffsetHash, _cameraRotationOffset);
            _animator.SetBool(_movementInputHeldHash,     _movementInputHeld);
            _animator.SetBool(_movementInputPressedHash,  _movementInputPressed);
            _animator.SetBool(_movementInputTappedHash,   _movementInputTapped);
            _animator.SetFloat(_shuffleDirectionXHash,    _shuffleDirectionX);
            _animator.SetFloat(_shuffleDirectionZHash,    _shuffleDirectionZ);
            _animator.SetBool(_isTurningInPlaceHash,      _isTurningInPlace);
            _animator.SetBool(_isCrouchingHash,           _isCrouching);
            _animator.SetFloat(_fallingDurationHash,      _fallingDuration);
            _animator.SetBool(_isGroundedHash,            _isGrounded);
            _animator.SetBool(_isWalkingHash,             _isWalking);
            _animator.SetBool(_isStoppedHash,             _isStopped);
        }
    }
}
