using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// Hosts a list of GamePathMovers on one object. Activate() starts them all,
/// Deactivate() stops them all. Optionally fires OnAllMoversComplete when every
/// mover has finished.
public class GamePathObject : MonoBehaviour
{
    public GamePath gamePath;
    public bool AutoActivateOnStart = false;
    public bool DebugLog = false;

    [Tooltip("List of path movers - each handles its own movement settings")]
    public List<GamePathMover> PathMovers = new List<GamePathMover>();

    [Tooltip("Fired when every mover has stopped (requires InvokeOnComplete).")]
    public UnityEvent OnAllMoversComplete;
    public bool InvokeOnComplete = false;

    [HideInInspector] public bool IsActive { get; private set; }

    public void Activate()
    {
        IsActive = true;
        // Start movement for all configured path movers
        foreach (var mover in PathMovers)
        {
            if (mover.Path == null) mover.Path = gamePath;
            mover.StartMoving();
        }
    }

    public void Deactivate()
    {
        IsActive = false;
        // Stop all movers
        foreach (var mover in PathMovers)
        {
            mover.StopMoving();
        }
    }

    protected virtual void Start()
    {
        //SET THIS OBJECT AS TRANSFORM OVERRIDE - positions are relative to this object
        if (gamePath != null && gamePath.transformOverride == null)
        {
            gamePath.transformOverride = transform;
            if (DebugLog) Debug.Log($"[GamePathObject] Set transform override to {name}", this);
        }

        // Auto-activate if needed
        if (AutoActivateOnStart)
        {
            Activate();
        }
    }

    public virtual void FixedUpdate()
    {
        // Update all movers
        bool allStopped = PathMovers.Count > 0;
        foreach (var mover in PathMovers)
        {
            mover.Update();
            if (mover.IsMoving) allStopped = false;
        }

        if (InvokeOnComplete && IsActive && allStopped)
        {
            IsActive = false; // fire once per activation
            OnAllMoversComplete?.Invoke();
            if (DebugLog) Debug.Log($"Path movement completed, executing completion actions", this);
        }
    }
}
