using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Generic support layer for the menu system. Replaces app-specific services
/// (input, audio, pause, coroutine host) with small framework-owned pieces so the
/// menu code has no external dependencies beyond Unity itself.

#region PAUSE

/// Reference-counted pause tracker. Any system can register as a pause source;
/// IsPaused is true while at least one source is registered.
public static class MenuPause
{
    private static readonly HashSet<object> _sources = new HashSet<object>();
    private static readonly object _defaultSource = new object();

    public static int PauseSourceCount => _sources.Count;
    public static bool IsPaused => _sources.Count > 0;

    public static void Pause(object source = null) => _sources.Add(source ?? _defaultSource);
    public static void Unpause(object source = null) => _sources.Remove(source ?? _defaultSource);
    public static void Clear() => _sources.Clear();
}

#endregion

#region INPUT

/// Singleton holding the InputActionReferences the menu system listens to, plus
/// small input helpers. Assign the references in the inspector.
public class MenuInput : MonoBehaviour
{
    public static MenuInput Instance { get; private set; }

    [Header("Actions")]
    public InputActionReference Navigate;
    public InputActionReference Confirm;
    public InputActionReference Cancel;
    public InputActionReference Pause;

    [Header("Rebinding")]
    [Tooltip("Actions checked for conflicts when interactively rebinding.")]
    public InputActionReference[] BindableActions;
    [Tooltip("Which binding index on an action is rebound / displayed.")]
    public int BindingIndex = 0;

    /// When true, confirm/cancel presses are deferred a frame (set by menus that
    /// open sub-menus so the same press doesn't bleed through).
    public static bool WaitForLateUpdate;

    /// Set by whatever handles text entry; consumed and cleared by typing fields.
    public static string LastTypedLetter = "";

    /// Fired after an interactive rebind is applied so the host app can persist it.
    public static System.Action<InputAction> OnBindingChanged;

    public static bool IsOnConsole => false; // host app can override

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        WaitForLateUpdate = false;
    }

    /// True only on the frame the button went down (performed + pressed check).
    public static bool ButtonPressedDown(InputAction.CallbackContext context)
    {
        return context.performed && context.action.WasPressedThisFrame();
    }

    /// Display name of the current binding on an action (for ControlSetup options).
    public static string GetButtonName(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null) return "";
        int index = Instance != null ? Instance.BindingIndex : 0;
        return actionRef.action.GetBindingDisplayString(index);
    }

    /// Counts a float down to zero using unscaled delta time (frame-based auto-repeat timers).
    public static void FrameCountdownUnscaled(ref float counter)
    {
        if (counter > 0) counter = Mathf.Max(0, counter - Time.unscaledDeltaTime * 60f);
    }
}

#endregion

#region AUDIO

/// Static menu audio. Assign clips once (e.g. at boot) and play from anywhere.
public static class MenuAudio
{
    public static AudioClip Move;
    public static AudioClip Confirm;
    public static AudioClip Cancel;

    private static AudioSource _source;

    public static void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (_source == null)
        {
            var go = new GameObject("[MenuAudio]");
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }
        _source.PlayOneShot(clip);
    }
}

#endregion

#region COROUTINE HOST

/// Auto-created MonoBehaviour used as the coroutine host for ActionSequencer
/// sequences driven by menu code.
public class MenuRoutines : MonoBehaviour
{
    private static MenuRoutines _instance;
    public static MenuRoutines Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[MenuRoutines]");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<MenuRoutines>();
            }
            return _instance;
        }
    }
}

#endregion

#region NODES & REVEAL GROUPS

/// A group of menu nodes that can prepare visibility and play reveal animations
/// (e.g. rows that cascade-open when a node is revealed). Implement on a component
/// sitting between the Menu and its MenuNodes in the hierarchy.
public interface IMenuRevealGroup
{
    void PrepareVisibility();
    void CheckForRevealing();
    bool IsRevealPlaying { get; }
}

/// A selectable menu node registered with MenuAutomation under a Key, so flows can
/// navigate to it or reveal it without knowing the concrete type. Reveal() applies
/// the state change (unlock, approve, mark-as-new); Plop() plays the visual pop.
public class MenuNode : CursorSelectable
{
    [Tooltip("Registry key used by MenuAutomation to find this node (string, enum, asset - any object).")]
    public object Key;

    [HideInInspector] public Menu OwningMenu;
    [HideInInspector] public IMenuRevealGroup Group;

    public override void Awake()
    {
        base.Awake();
        OwningMenu = GetComponentInParent<Menu>(true);
        Group = GetComponentInParent<IMenuRevealGroup>();
    }

    void OnEnable() { if (Key != null) MenuAutomation.RegisterNode(this); }
    void OnDisable() { if (Key != null) MenuAutomation.UnregisterNode(this); }

    /// Apply the node's state change (e.g. unlock). Called by MenuAutomation before Plop().
    public virtual void Reveal() { }
}

#endregion
