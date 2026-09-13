using System.Collections.Generic;
using UnityEngine;

public enum LoopMode { Once, Loop, PingPong, PingPongOnce }

/// When the mover should ask its target to release whatever it's carrying/holding
/// (e.g. a passenger dismounting, a payload being dropped).
public enum ReleaseTiming { None, OnReturn, OnEnd }

/// Optional hooks a target MonoBehaviour can implement to integrate with GamePathMover.
/// Auto-detected on the target transform - no wiring required.
public interface IPathMoverTarget
{
    /// False while the target can't be moved (busy, disabled, etc.) - the mover waits.
    bool CanMove { get; }
    /// Called when DisableControlWhileMoving is set: lock/unlock external control of the target.
    void SetControlLocked(bool locked);
    /// Cancel any movement the target is performing itself before the path takes over.
    void StopMovement();
    /// Release whatever the target is carrying (called per ReleaseTiming).
    void Release();
    /// Focus/unfocus a camera or highlight on the target while the path runs.
    void FocusCamera();
    void UnfocusCamera();
}

[System.Serializable]
public class GamePathMover
{
    #region SETTINGS

    public GamePath Path;
    public SimplePathObject SimplePath;

    public float Speed = 5f;
    public float SmoothFactor = 0.5f;
    public float ReachDistance = 0.1f;
    public LoopMode Mode = LoopMode.Loop;
    public bool HardStopAtEnd = false;
    [Tooltip("Direction passed to the movement adapter when the path finishes. 0 = keep current.")]
    public int EndDirection = 0;
    public ReleaseTiming ReleaseAt = ReleaseTiming.None;
    public bool DisableControlWhileMoving = false;
    public bool FocusCameraWhileMoving = false;

    [SerializeField] private Transform _target;
    [SerializeField] private int _firstPositionIndex = 0;
    [SerializeField] private bool _ignoreFirstPoint = false;
    [SerializeField] private bool _slowDownAtLastPoint = false;
    [SerializeField] private bool _reverseOnDeactivate = false;

    #endregion

    #region EVENTS

    public System.Action OnStartedMovingForward;
    public System.Action OnStartedMovingBackward;
    public System.Action OnStopped;
    public System.Action OnReachedEnd;

    #endregion

    #region STATE (Non-Serialized)

    [System.NonSerialized] private Transform _targetTransform;
    [System.NonSerialized] private IPathMoverTarget _hooks;
    [System.NonSerialized] private IMovementAdapter _movement;
    [System.NonSerialized] private List<Vector3> _positions;
    [System.NonSerialized] private int _currentIndex = 0;
    [System.NonSerialized] private bool _isMoving = false;
    [System.NonSerialized] private bool _movingForward = true;
    [System.NonSerialized] private bool _initialized = false;
    [System.NonSerialized] private Vector2 _lastPosition;
    [System.NonSerialized] private bool _reachedEnd = false;
    [System.NonSerialized] private bool _reachedStart = false;
    [System.NonSerialized] private bool _pendingDirectionChange = false;
    [System.NonSerialized] private bool _previousMovingForward = true;
    [System.NonSerialized] private bool _paused = false;

    public bool IsMoving => _isMoving;
    public int CurrentIndex => _currentIndex;
    public Vector3 CurrentVelocity { get; private set; }
    public bool ReachedEnd => _reachedEnd;
    public bool ReachedStart => _reachedStart;
    public bool MovingForward => _movingForward;

    #endregion

    #region PUBLIC API

    // Initialize with target transform (auto-detects IMovementAdapter and IPathMoverTarget on it)
    public void Initialize(Transform target)
    {
        _targetTransform = target;
        _hooks = target != null ? target.GetComponent<IPathMoverTarget>() : null;

        //AUTO-DETECT a custom movement adapter component, else fall back to transform movement
        var adapter = target != null ? target.GetComponent<IMovementAdapter>() : null;
        _movement = adapter ?? new TransformMovementAdapter(target, Speed, SmoothFactor);

        InitializePath();
        _initialized = true;
    }

    // Initialize with custom adapter
    public void Initialize(IMovementAdapter adapter)
    {
        _movement = adapter;
        InitializePath();
        _initialized = true;
    }

    // Start moving (auto-initializes if needed)
    public void StartMoving(Transform target = null, bool triggeredBySwitch = false)
    {
        //ALREADY MOVING FORWARD: nothing to do
        if (_isMoving && _movingForward) return;

        if (Path == null && SimplePath == null) Debug.LogError("PATH NOT FOUND for GamePathMover", target?.gameObject);
        // Auto-detect target if not initialized
        if (!_initialized)
        {
            if (target != null)
            {
                Initialize(target);
            }
            else if (_target != null)
            {
                Initialize(_target);
            }
            else
            {
                Debug.LogWarning("[GamePathMover] No target assigned! Set _target field or call Initialize() first.");
                return;
            }
        }

        if (_positions == null || _positions.Count == 0)
        {
            Debug.LogWarning("[GamePathMover] No positions in path!");
            return;
        }

        //CANCEL any movement the target is doing itself before the path takes over
        _hooks?.StopMovement();

        //ACTIVATION: Check if we're already moving backward (direction change)
        bool wasMovingBackward = _isMoving && !_movingForward;

        _isMoving = true;
        _movingForward = true;
        _reachedEnd = false;
        _reachedStart = false;
        _lastPosition = _movement.GetPosition();

        //IGNORE FIRST POINT: Skip index 0 going forward since we're already at that position
        if (!wasMovingBackward && _ignoreFirstPoint && _currentIndex == 0 && _positions.Count > 1)
            _currentIndex = 1;

        //LOCK external control while the path drives the target
        if (DisableControlWhileMoving)
            _hooks?.SetControlLocked(true);

        //CAMERA FOCUS on the moving object while path is active
        if (FocusCameraWhileMoving)
            _hooks?.FocusCamera();

        if (wasMovingBackward)
        {
            //TARGET was moving backward, needs to finish current point before moving forward
            _pendingDirectionChange = true;
            //NOTE: Event will fire in AdvanceToNextPoint() when target actually starts moving forward
        }
        else
        {
            //TARGET was stopped or already moving forward, start immediately
            _previousMovingForward = true;
            _pendingDirectionChange = false;
            //EVENT: Started moving forward
            OnStartedMovingForward?.Invoke();
        }
    }

    // Stop moving (or reverse if ReverseOnDeactivate)
    public void StopMoving(bool triggeredBySwitch = false)
    {
        //SWITCH BEHAVIOR: If triggered by switch and ReverseOnDeactivate, reverse direction instead of stopping
        if (triggeredBySwitch && _reverseOnDeactivate)
        {
            _movingForward = !_movingForward;
            _reachedEnd = false;
            _reachedStart = false;
            _pendingDirectionChange = true;

            //ENSURE movement continues
            if (!_isMoving)
            {
                _isMoving = true;
                _lastPosition = _movement.GetPosition();
            }

            //NOTE: Event will fire in AdvanceToNextPoint() when target actually starts moving in new direction
        }
        else
        {
            FinishPathMovement(true, false);
        }
    }

    // Reset to start position
    public void ResetToStart()
    {
        _currentIndex = _firstPositionIndex;
        _movingForward = true;
        _reachedEnd = false;
        _reachedStart = false;
    }

    // Instant reset: stop movement, reset state, teleport to start position
    public void InstantReset()
    {
        _isMoving = false;
        _movingForward = true;
        _currentIndex = _firstPositionIndex;
        _reachedEnd = false;
        _reachedStart = false;
        _pendingDirectionChange = false;
        _previousMovingForward = true;
        _paused = false;
        CurrentVelocity = Vector3.zero;

        if (_movement != null)
        {
            _movement.StopMoving(false);
            if (_positions != null && _positions.Count > 0)
            {
                _movement.TeleportTo(_positions[_firstPositionIndex]);
                _lastPosition = _movement.GetPosition();
            }
        }

        RestoreControl();
        StopCameraFocus();
        OnStopped?.Invoke();
    }

    // Set movement direction (for external control like SimplePathGuide)
    public void SetDirection(bool forward)
    {
        if (_movingForward == forward) return;

        _movingForward = forward;
        _reachedEnd = false;
        _reachedStart = false;
        _pendingDirectionChange = true;
    }

    // Move forward on path (spammable - call every frame)
    public void MoveForwardOnPath()
    {
        bool wasMoving = _isMoving;
        bool directionChanged = !_movingForward;

        //DIRECTION CHANGE: If was backward, retarget to next point
        if (!_movingForward && _positions != null)
            _currentIndex = Mathf.Min(_positions.Count - 1, _currentIndex + 1);

        _movingForward = true;
        _reachedStart = false;

        if (_reachedEnd) return;
        _isMoving = true;

        if (!wasMoving || directionChanged)
        {
            _previousMovingForward = true;
            _pendingDirectionChange = false;
            OnStartedMovingForward?.Invoke();
        }
    }

    // Move backward on path (spammable - call every frame)
    public void MoveBackwardOnPath()
    {
        bool wasMoving = _isMoving;
        bool directionChanged = _movingForward;

        //DIRECTION CHANGE: If was forward, retarget to previous point
        if (_movingForward && _positions != null)
            _currentIndex = Mathf.Max(0, _currentIndex - 1);

        _movingForward = false;
        _reachedEnd = false;

        if (_reachedStart) return;
        _isMoving = true;

        if (!wasMoving || directionChanged)
        {
            _previousMovingForward = false;
            _pendingDirectionChange = false;
            OnStartedMovingBackward?.Invoke();
        }
    }

    // Pause movement (spammable - call every frame)
    public void Pause()
    {
        _paused = true;
        _isMoving = false;
    }

    // Resume paused movement
    public void Resume()
    {
        if (!_paused) return;
        _paused = false;
        _isMoving = true;
        _lastPosition = _movement.GetPosition();
    }

    // Call this from FixedUpdate
    public void Update()
    {
        if (!_isMoving || !_initialized || _positions == null || _positions.Count == 0)
            return;

        //STOP if target is teleporting
        if (_movement.IsTeleporting)
        {
            RestoreControl();
            StopCameraFocus();
            _isMoving = false;
            CurrentVelocity = Vector3.zero;
            OnStopped?.Invoke();
            return;
        }

        //SKIP if target can't move right now
        if (_hooks != null && !_hooks.CanMove)
        {
            _hooks.StopMovement();
            return;
        }

        UpdateInternalMovement();
    }

    #endregion

    #region PRIVATE METHODS

    // INITIALIZE PATH
    private void InitializePath()
    {
        _positions = new List<Vector3>();

        //AUTO-DETECT SimplePathObject if not assigned
        if (SimplePath == null && _targetTransform != null)
        {
            SimplePath = _targetTransform.GetComponent<SimplePathObject>();
            if (SimplePath == null)
                SimplePath = _targetTransform.GetComponentInChildren<SimplePathObject>();
        }

        if (Path != null && Path.positions != null && Path.positions.Count > 0)
        {
            // GAMEPATH
            for (int i = 0; i < Path.positions.Count; i++)
                _positions.Add(Path.GetWorldPosition(i));
        }
        else if (SimplePath != null && SimplePath.PointCount > 0)
        {
            // SIMPLEPATH
            for (int i = 0; i < SimplePath.PointCount; i++)
                _positions.Add(SimplePath.GetWorldPosition(i));
        }

        if (_positions.Count == 0)
            Debug.LogWarning("[GamePathMover] No positions found in Path or SimplePath!");
    }

    // UPDATE INTERNAL MOVEMENT
    private void UpdateInternalMovement()
    {
        Vector3 targetPos = _positions[_currentIndex];
        bool stopAtTarget = (Mode == LoopMode.Once && _movingForward && _currentIndex == _positions.Count - 1);

        if (stopAtTarget && _movement.WaitForStopAtTarget && !_movement.IsMovingToTarget && Vector2.Distance(_movement.GetPosition(), targetPos) <= ReachDistance)
        {
            AdvanceToNextPoint();
            return;
        }

        // Move towards target
        _movement.MoveToTarget(targetPos, ReachDistance, stopAtTarget, SmoothFactor);
        _movement.Update();

        // Calculate velocity for momentum transfer
        Vector2 currentPos = _movement.GetPosition();
        CurrentVelocity = (currentPos - _lastPosition) / Time.fixedDeltaTime;
        _lastPosition = currentPos;

        // Apply slowdown if approaching last point
        if (Mode == LoopMode.Once && _currentIndex == _positions.Count - 1 && _positions.Count >= 2)
        {
            //slow as approaching
            if (_slowDownAtLastPoint)
            {
                Vector3 secondLastPoint = _positions[_positions.Count - 2];
                Vector3 lastPoint = _positions[_positions.Count - 1];
                float totalDistance = Vector2.Distance(secondLastPoint, lastPoint);
                float distanceToFinal = Vector2.Distance(currentPos, lastPoint);

                float speedMultiplier = totalDistance > 0 ?
                    Mathf.Clamp(distanceToFinal / totalDistance, 0.01f, 1.0f) :
                    0.01f;

                _movement.SetSpeedMultiplier(speedMultiplier);
            }
        }
        else
        {
            _movement.SetSpeedMultiplier(1.0f);
        }

        // Check if reached target
        float distance = Vector2.Distance(currentPos, targetPos);
        if (distance <= ReachDistance && (!stopAtTarget || !_movement.WaitForStopAtTarget || !_movement.IsMovingToTarget))
        {
            AdvanceToNextPoint();
        }
    }

    // ADVANCE TO NEXT POINT
    private void AdvanceToNextPoint()
    {
        //DIRECTION CHANGE: Check if direction changed and fire event
        if (_pendingDirectionChange && _movingForward != _previousMovingForward)
        {
            _pendingDirectionChange = false;
            _previousMovingForward = _movingForward;

            if (_movingForward)
                OnStartedMovingForward?.Invoke();
            else
                OnStartedMovingBackward?.Invoke();
        }

        if (_movingForward)
        {
            //MOVING FORWARD
            _currentIndex++;

            if (_currentIndex >= _positions.Count)
            {
                //REACHED END
                _reachedEnd = true;
                _reachedStart = false;

                switch (Mode)
                {
                    case LoopMode.Once:
                        //STOP at last point
                        _currentIndex = _positions.Count - 1;

                        //REVERSIBLE: Stop and wait for switch to reverse direction
                        if (_reverseOnDeactivate)
                            _isMoving = false;
                        else
                            FinishPathMovement(HardStopAtEnd, true);
                        _isMoving = false;
                        break;

                    case LoopMode.Loop:
                        _currentIndex = _firstPositionIndex;
                        _reachedEnd = false;
                        break;

                    case LoopMode.PingPong:
                    case LoopMode.PingPongOnce:
                        _movingForward = false;
                        _currentIndex = Mathf.Max(0, _positions.Count - 2);
                        _reachedEnd = false;

                        //RELEASE at end point (ping → release → pong continues)
                        OnReachedEnd?.Invoke();
                        if (ReleaseAt == ReleaseTiming.OnReturn)
                            ReleaseTarget();
                        break;
                }
            }
        }
        else
        {
            //MOVING BACKWARD
            _currentIndex--;

            if (_currentIndex < 0)
            {
                //REACHED START
                _reachedStart = true;
                _reachedEnd = false;
                _currentIndex = 0;

                if (Mode == LoopMode.PingPong)
                {
                    //PINGPONG: Reverse direction
                    _movingForward = true;
                    _currentIndex = Mathf.Min(1, _positions.Count - 1);
                    _reachedStart = false;
                }
                else if (Mode == LoopMode.PingPongOnce)
                {
                    FinishPathMovement(HardStopAtEnd, true);
                }
                else
                {
                    //ONCE/LOOP: Stop at start
                    _isMoving = false;
                }
            }
        }
    }

    private void FinishPathMovement(bool hardStop, bool applyEndDirection)
    {
        _isMoving = false;
        CurrentVelocity = Vector3.zero;
        _movement.StopMoving(hardStop);

        if (applyEndDirection && EndDirection != 0)
            _movement.SetDirection(EndDirection);

        RestoreControl();
        StopCameraFocus();

        //RELEASE after full path completes
        if (ReleaseAt == ReleaseTiming.OnEnd)
            ReleaseTarget();

        OnStopped?.Invoke();
    }

    private void ReleaseTarget()
    {
        _hooks?.Release();
    }

    private void RestoreControl()
    {
        if (DisableControlWhileMoving)
            _hooks?.SetControlLocked(false);
    }

    private void StopCameraFocus()
    {
        if (FocusCameraWhileMoving)
            _hooks?.UnfocusCamera();
    }

    #endregion

    #region MOVEMENT ADAPTERS

    // Base interface for movement adapters. Implement on a MonoBehaviour on the target
    // for auto-detection, or pass one to Initialize().
    public interface IMovementAdapter
    {
        void MoveToTarget(Vector3 target, float reachDistance, bool stopAtTarget, float smoothFactor);
        void StopMoving(bool hardStop);
        void SetDirection(int direction);
        void SetSpeedMultiplier(float multiplier);
        void TeleportTo(Vector3 position);
        void Update();
        Vector2 GetPosition();
        bool IsMovingToTarget { get; }
        bool WaitForStopAtTarget { get; }
        bool IsTeleporting { get; }
    }

    // Transform-based movement adapter (default fallback)
    public class TransformMovementAdapter : IMovementAdapter
    {
        private Transform _transform;
        private Vector3 _targetPosition;
        private Vector3 _velocity = Vector3.zero;
        private bool _isMoving = false;
        private float _speed;
        private float _baseSpeed;
        private float _smoothFactor;

        public bool IsMovingToTarget => _isMoving;
        public bool WaitForStopAtTarget => false;
        public bool IsTeleporting => false;

        public TransformMovementAdapter(Transform transform, float speed, float smoothFactor)
        {
            _transform = transform;
            _speed = speed;
            _baseSpeed = speed;
            _smoothFactor = smoothFactor;
        }

        public void MoveToTarget(Vector3 target, float reachDistance, bool stopAtTarget, float smoothFactor)
        {
            _targetPosition = target;
            _isMoving = true;
        }

        public void StopMoving(bool hardStop)
        {
            _isMoving = false;
            _velocity = Vector3.zero;
        }

        public void SetDirection(int direction) { }

        public void SetSpeedMultiplier(float multiplier)
        {
            _speed = _baseSpeed * multiplier;
        }

        public void TeleportTo(Vector3 position)
        {
            _isMoving = false;
            _velocity = Vector3.zero;
            _transform.position = new Vector3(position.x, position.y, _transform.position.z);
        }

        public Vector2 GetPosition() => _transform.position;

        public void Update()
        {
            if (!_isMoving) return;

            // Preserve Z coordinate
            Vector3 destination = new Vector3(_targetPosition.x, _targetPosition.y, _transform.position.z);

            // Smooth movement
            Vector3 newPosition = Vector3.SmoothDamp(
                _transform.position,
                destination,
                ref _velocity,
                _smoothFactor,
                _speed,
                Time.fixedDeltaTime
            );

            _transform.position = newPosition;
        }
    }

    #endregion
}
