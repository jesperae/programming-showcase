Unity GamePath System

This is a path-based movement framework. It lets you define waypoint paths as ScriptableObjects and move objects along them with different loop modes, speeds, and pluggable movement systems. Fully generic - works for UI flows, guided tours, delivery routes, or any object that should follow a route.


HOW IT WORKS

The core idea is straightforward: you create a GamePath (a ScriptableObject) that holds an ordered list of GamePosition waypoints. Each waypoint has a position, an optional ID, and an optional occupant ID for filtering. Then you give a GamePathMover the path and a target to move, and it drives the target along the waypoints.

GamePathMover uses the adapter pattern. Instead of assuming how the target moves, it takes an IMovementAdapter interface. A built-in TransformMovementAdapter moves any Transform with SmoothDamp, and any MonoBehaviour on the target that implements IMovementAdapter is auto-detected - so you can plug in physics, tweening, or your own movement system without touching the mover.

Optional target integration is handled by IPathMoverTarget, also auto-detected on the target: it can lock external control while moving, focus a camera, report when it can't move, and release a payload at the end of the route.

There are four loop modes:
- Once: move forward through all points, stop at the end
- Loop: wrap around to the start when reaching the end
- PingPong: reverse direction at each end, repeat forever
- PingPongOnce: forward to end, backward to start, then stop

GamePathObject ties it all together. Activate() starts all its movers, Deactivate() stops them, and an optional UnityEvent fires when every mover has finished.

You can also do some cool stuff: skip the first point if you're already there, slow down when approaching the final point, lock external control while moving, focus a camera on the moving object, release a payload at the right time, and pick random positions (with optional occupant filtering) for placement.


USAGE EXAMPLE

// Create a path asset
var path = ScriptableObject.CreateInstance<GamePath>();
path.positions.Add(new GamePosition { id = "start", position = Vector2.zero });
path.positions.Add(new GamePosition { id = "end", position = new Vector2(10, 0) });
path.loop = false;

// Create a mover
var mover = new GamePathMover
{
    Path = path,
    Speed = 5f,
    SmoothFactor = 0.5f,
    Mode = LoopMode.Once
};

// Initialize with a Transform and start moving
mover.Initialize(targetTransform);
mover.StartMoving();

// Update each FixedUpdate
void FixedUpdate() => mover.Update();


FILES

- GamePath.cs -- ScriptableObject defining an ordered list of GamePosition waypoints. Supports ID-based lookup, random position selection (with occupant filtering), sequential iteration with looping, and transform overrides for relative positioning.
- GamePosition.cs -- serializable waypoint data: position vector, optional relative transform, occupant ID, and unique ID. Converts local positions to world space via GetWorldPosition().
- GamePathMover.cs -- the movement controller. Drives a target along a GamePath or SimplePathObject with configurable speed, smoothing, reach distance, and loop modes. Uses the adapter pattern (IMovementAdapter) plus optional IPathMoverTarget hooks.
- GamePathObject.cs -- activates/deactivates all child GamePathMover instances. Manages transform overrides, auto-activation, and a completion UnityEvent.


KEY FEATURES

- Direction reversal: StopMoving() can reverse direction instead of stopping (ReverseOnDeactivate)
- Speed control: optional slowdown when approaching the final point in Once mode
- First point skipping: IgnoreFirstPoint to avoid snapping to the start when already there
- Control locking: DisableControlWhileMoving locks external control during path movement (via IPathMoverTarget)
- Camera focus: FocusCameraWhileMoving asks the target to focus a camera on itself
- Release timing: automatically calls IPathMoverTarget.Release() at path end or on return
- Event system: OnStartedMovingForward, OnStartedMovingBackward, OnStopped, OnReachedEnd delegates
- Random positions: GamePath.GetPosition("random") picks a random waypoint, optionally filtered by occupant ID
- Occupant-specific positions: positions can be tagged with an occupantId for filtered lookup
- Relative positioning: transformOverride makes all positions relative to a parent transform
- SimplePath support: falls back to SimplePathObject if no GamePath asset is assigned


DEPENDENCIES

Self-contained apart from one optional helper:
- SimplePathObject -- alternative inline path definition (optional; GamePath assets work standalone)

Everything else is interface-driven: implement IMovementAdapter for custom movement and IPathMoverTarget for control-lock/camera/release hooks.


ORIGIN

Extracted from Wings of Vi 2 (Unity 2D action-platformer) developed with the Grynsoft 2D Engine.


LICENSE

MIT -- see LICENSE file.
