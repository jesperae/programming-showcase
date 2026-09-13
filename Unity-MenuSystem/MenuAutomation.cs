using System.Collections.Generic;
using UnityEngine;


/// Centralized system for driving menus programmatically: queued node reveals,
/// navigation-to-node, and batched reveal animations. Generic - works with any
/// MenuNode / IMenuRevealGroup, no domain types required.

public static class MenuAutomation
{
    //REGISTRY - nodes keyed by their MenuNode.Key
    private static Dictionary<object, MenuNode> _nodeRegistry = new Dictionary<object, MenuNode>();

    //ANIMATION TYPES
    private enum AnimationType { NodeReveal, NodeNavigate, Custom }
    private class QueuedAnimation
    {
        public AnimationType Type;
        public MenuNode Node;
        public Menu Menu;                            //Custom: menu to open
        public System.Func<CursorSelectable> ResolveTarget; //Custom: locate target after open
        public bool DontWaitForClose;
    }

    //QUEUES
    private static List<QueuedAnimation> _animationQueue = new List<QueuedAnimation>();

    //STATE
    private static bool _isAnimationPlaying;
    private static bool _isMenuOpen;
    private static bool _interrupted;
    private static Menu _currentMenu;
    public static bool IsAnimationPlaying => _isAnimationPlaying;

    /// The top-level menu treated as "root" (its open state counts toward IsBusy and
    /// its OpenCloseAnimation is driven during automation). Assign once at startup.
    public static Menu RootMenu;

    /// Delay between batched node reveals.
    public static float MultiRevealDelay = 0.4f;

    /// Fired when automation takes over (host app can pause dialogs/workflows),
    /// and when the queue fully drains (host app can resume them).
    public static System.Action OnAutomationBegin;
    public static System.Action OnAutomationEnd;

    public static bool IsBusy => (RootMenu != null && RootMenu.IsOpened) || _isAnimationPlaying || _isMenuOpen || _animationQueue.Count > 0;

    //INITIALIZATION
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        _nodeRegistry.Clear();
        _animationQueue.Clear();
        _isAnimationPlaying = false;
        _isMenuOpen = false;
        _interrupted = false;
        _currentMenu = null;
        RootMenu = null;
        // Clear stale menu static reference from previous session
        typeof(Menu).GetProperty("CurrentOpen", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.SetValue(null, null);
    }

    //REGISTRATION
    public static void RegisterNode(MenuNode node)
    {
        if (node == null || node.Key == null) return;
        _nodeRegistry[node.Key] = node;
    }

    public static void UnregisterNode(MenuNode node)
    {
        if (node == null || node.Key == null) return;
        _nodeRegistry.Remove(node.Key);
    }

    #region QUEUEING

    /// Queue a node reveal animation (opens its menu, applies Reveal(), pops it).
    public static void QueueNodeReveal(MenuNode node)
    {
        if (node == null) return;
        _animationQueue.Add(new QueuedAnimation
        {
            Type = AnimationType.NodeReveal,
            Node = node
        });
    }

    /// Queue a node reveal by registry key.
    public static void QueueNodeReveal(object key)
    {
        var node = FindNode(key);
        if (node != null) QueueNodeReveal(node);
    }

    /// Find the MenuNode registered for a given key.
    public static MenuNode FindNode(object key)
    {
        if (key == null) return null;
        _nodeRegistry.TryGetValue(key, out MenuNode node);
        return node;
    }

    /// Navigate directly to a node immediately - closes any open automation menu first.
    public static void QueueSelectNode(object key)
    {
        var node = FindNode(key);
        if (node == null) return;
        //SHORTCUT: bypass queue entirely, interrupt any running animation
        InterruptAndClose();
        PlayNodeSelectAnimation(node, shortcut: true);
    }

    /// Open the node's menu and select it (queued).
    public static void QueueNavigateToNode(MenuNode node, bool dontWaitForClose = false)
    {
        if (node == null) return;
        _animationQueue.Add(new QueuedAnimation
        {
            Type = AnimationType.NodeNavigate,
            Node = node,
            DontWaitForClose = dontWaitForClose
        });
    }

    /// Queue a generic reveal: opens the menu, then resolves the target selectable
    /// (e.g. a row that only exists after the menu refreshes) and pops it.
    public static void QueueReveal(Menu menu, System.Func<CursorSelectable> resolveTarget)
    {
        if (menu == null || resolveTarget == null) return;
        _animationQueue.Add(new QueuedAnimation
        {
            Type = AnimationType.Custom,
            Menu = menu,
            ResolveTarget = resolveTarget
        });
    }

    #endregion


    /// Check and play next queued animation if ready

    public static void Update()
    {
        if (_isAnimationPlaying || _isMenuOpen) return;

        //PROCESS NEXT ANIMATION IN QUEUE
        if (_animationQueue.Count > 0)
        {
            //NOTIFY host app that automation is taking over
            OnAutomationBegin?.Invoke();

            var animation = _animationQueue[0];
            _animationQueue.RemoveAt(0);

            if (animation.Type == AnimationType.NodeReveal)
            {
                // BATCH consecutive node reveals that share the same menu so the menu opens once
                var batch = new List<MenuNode> { animation.Node };
                var firstMenu = animation.Node?.OwningMenu;

                while (_animationQueue.Count > 0)
                {
                    var next = _animationQueue[0];
                    if (next.Type != AnimationType.NodeReveal) break;
                    if (next.Node?.OwningMenu != firstMenu) break;
                    batch.Add(next.Node);
                    _animationQueue.RemoveAt(0);
                }

                if (batch.Count > 1)
                    PlayMultiNodeRevealAnimation(batch);
                else
                    PlayNodeRevealAnimation(animation.Node);
            }
            else if (animation.Type == AnimationType.NodeNavigate)
                PlayNodeSelectAnimation(animation.Node, animation.DontWaitForClose);
            else if (animation.Type == AnimationType.Custom)
                PlayCustomRevealAnimation(animation.Menu, animation.ResolveTarget);
        }
    }

    #region ANIMATION PLAYBACK

    static void PlayNodeRevealAnimation(MenuNode node)
    {
        if (node == null || node.OwningMenu == null) return;

        PlayUnifiedAnimation(
            menu: node.OwningMenu,
            targetSelectable: node,
            onPlop: () => node.Reveal(),
            onMenuOpened: null
        );
    }

    /// Play multiple node reveals in one menu session: open once, plop each with MultiRevealDelay, then wait for close once
    static void PlayMultiNodeRevealAnimation(List<MenuNode> nodes)
    {
        if (nodes == null || nodes.Count == 0) return;

        var menu = nodes[0].OwningMenu;
        if (menu == null) return;

        _isAnimationPlaying = true;
        _isMenuOpen = true;
        _interrupted = false;
        _currentMenu = menu;

        var seq = ActionSequencer.CreateSequence(MenuRoutines.Instance, "MenuAutomation.MultiReveal");
        bool openedAsSubMenu = false;

        //1. OPEN MENU ONCE
        seq.Add(() =>
        {
            openedAsSubMenu = TryOpenAsSubMenu(menu);
            if (!openedAsSubMenu)
                menu.Open(); //CLOSES any previously open menu first
            menu.MyCursor.MovementDisabled = true;
            MenuPause.Pause();
            if (!openedAsSubMenu)
                SetRootMenuAnimation(true);
        });

        seq.AddDelay(0.66f);

        //2. PLOP EACH NODE IN ORDER: select → reveal → cascade check → wait → delay
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            // Move cursor to this node
            seq.Add(() =>
            {
                if (node == null) return;
                menu.MyCursor?.Selected(node);
            });

            // Reveal + plop
            seq.Add(() =>
            {
                if (node == null) return;
                node.Reveal();
                node.Plop();
                menu.MyCursor?.FocusAttention();
            });

            // Trigger reveal on THIS node's group only
            seq.Add(() =>
            {
                node?.Group?.CheckForRevealing();
            });

            // Wait for THIS group's reveal to fully complete before moving on
            seq.AddWaitWhile(() => node?.Group != null && node.Group.IsRevealPlaying);

            // Gap before next node (except after the last one)
            if (i < nodes.Count - 1)
                seq.AddDelay(MultiRevealDelay);
        }

        //3. WAIT BEFORE RE-ENABLING CURSOR
        seq.AddDelay(0.5f);

        //4. ALLOW USER MOVEMENT
        seq.Add(() =>
        {
            menu.MyCursor.MovementDisabled = false;
            _isAnimationPlaying = false; //Hand control to the user; keep _isMenuOpen until close
        });

        //5. WAIT FOR USER TO CLOSE MENU (unpause) OR interrupt signal
        seq.AddWaitWhile(() => MenuPause.IsPaused && !_interrupted);

        seq.Add(() =>
        {
            _isMenuOpen = false;
            _currentMenu = null;
            if (!openedAsSubMenu)
                SetRootMenuAnimation(false);

            //ONLY notify end if no more animations will immediately re-run
            if (_animationQueue.Count == 0)
                OnAutomationEnd?.Invoke();
        });

        seq.Run();
    }

    static void PlayNodeSelectAnimation(MenuNode node, bool shortcut = false)
    {
        if (node == null || node.OwningMenu == null) return;

        PlayUnifiedAnimation(
            menu: node.OwningMenu,
            targetSelectable: node,
            onPlop: null,
            onMenuOpened: null,
            shortcut: shortcut
        );
    }

    static void PlayCustomRevealAnimation(Menu menu, System.Func<CursorSelectable> resolveTarget)
    {
        if (menu == null) return;

        PlayUnifiedAnimation(
            menu: menu,
            targetSelectable: null, //Will find after menu opens
            onPlop: null,
            onMenuOpened: resolveTarget //Resolve target AFTER menu opens
        );
    }

    /// If the menu sits under a controllable parent menu (one with PressToOpen),
    /// simulate the normal "open parent → enter submenu" flow so visuals don't overlap.
    static bool TryOpenAsSubMenu(Menu menu)
    {
        if (menu == null || menu.transform.parent == null) return false;
        foreach (var m in menu.transform.parent.GetComponentsInChildren<Menu>(true))
            if (m != menu && m.PressToOpen != null)
            {
                menu.ActivateMenuAutomatically();
                return true;
            }
        return false;
    }

    /// Interrupt any running animation: close its menu, stay paused, let running sequence self-terminate
    static void InterruptAndClose()
    {
        _interrupted = true; //SIGNAL running sequence to self-terminate via AddWaitWhile
        if (_currentMenu != null && _currentMenu.IsActive)
        {
            _currentMenu.WentIntoSubMenu = true; // prevent pause
            _currentMenu.DeactivateMenu();
            _currentMenu.WentIntoSubMenu = false;
        }
        SetRootMenuAnimation(false);
        _currentMenu = null;
        _isAnimationPlaying = false;
        _isMenuOpen = false;
        _animationQueue.Clear();
    }


    /// Unified animation: Open menu → Plop target → Allow user movement → Wait for unpause

    static void SetRootMenuAnimation(bool show)
    {
        var anim = RootMenu?.OpenCloseAnimation;
        if (anim == null) return;
        if (show)
        {
            anim.ActivateAllTargets();
            anim.SetShowQuick(true);
        }
        else
        {
            anim.SetShowQuick(false);
        }
    }

    static void PlayUnifiedAnimation(Menu menu, CursorSelectable targetSelectable, System.Action onPlop, System.Func<CursorSelectable> onMenuOpened, bool shortcut = false)
    {
        _isAnimationPlaying = true;
        _isMenuOpen = true;
        _interrupted = false;
        _currentMenu = menu;

        var seq = ActionSequencer.CreateSequence(MenuRoutines.Instance, "MenuAutomation");
        bool openedAsSubMenu = false;

        //1. OPEN MENU
        seq.Add(() =>
        {
            openedAsSubMenu = TryOpenAsSubMenu(menu);
            if (!openedAsSubMenu)
                menu.Open(); //CLOSES any previously open menu first
            menu.MyCursor.MovementDisabled = !shortcut; //SHORTCUT: give control immediately

            //CALLBACK to find target after menu opens (for rows that need display refresh)
            if (onMenuOpened != null)
                targetSelectable = onMenuOpened.Invoke();

            if (targetSelectable != null)
                menu.MyCursor.Selected(targetSelectable);

            if (!shortcut)
            {
                MenuPause.Pause();
                if (!openedAsSubMenu)
                    SetRootMenuAnimation(true);
            }
        });

        if (shortcut)
        {
            //SHORTCUT: open and hand off - instantly reveal accessible groups, then give control.
            seq.Add(() =>
            {
                foreach (var group in menu.GetComponentsInChildren<IMenuRevealGroup>(true))
                    group.CheckForRevealing();
                _isAnimationPlaying = false;
                _isMenuOpen = false;
                _currentMenu = null;
            });
            seq.Run();
            return;
        }

        seq.AddDelay(0.66f);

        //2. PLOP IT IN (shake + optional action)
        seq.Add(() =>
        {
            targetSelectable?.Plop();
            onPlop?.Invoke();
            //TRIGGER reveal check after the state change so newly unlocked rows cascade-animate.
            foreach (var group in menu.GetComponentsInChildren<IMenuRevealGroup>(true))
                group.CheckForRevealing();
            //FOCUS ATTENTION on cursor text fields
            menu.MyCursor?.FocusAttention();
        });

        //3. WAIT FOR ANY REVEAL SEQUENCE before re-enabling cursor
        seq.AddWaitWhile(() =>
        {
            foreach (var group in menu.GetComponentsInChildren<IMenuRevealGroup>(true))
                if (group.IsRevealPlaying) return true;
            return false;
        });

        seq.AddDelay(0.5f);

        //4. ALLOW USER MOVEMENT
        seq.Add(() =>
        {
            menu.MyCursor.MovementDisabled = false;
            _isAnimationPlaying = false; //Hand control to the user; keep _isMenuOpen until close
        });

        //5. WAIT FOR USER TO CLOSE MENU (unpause) OR interrupt signal
        seq.AddWaitWhile(() => MenuPause.IsPaused && !_interrupted);

        seq.Add(() =>
        {
            _isMenuOpen = false;
            _currentMenu = null;
            if (!openedAsSubMenu)
                SetRootMenuAnimation(false);

            //ONLY notify end if no more animations will immediately re-run
            if (_animationQueue.Count == 0)
                OnAutomationEnd?.Invoke();
        });

        seq.Run();
    }

    #endregion

    #region DEBUG

    public static void ClearQueues()
    {
        _animationQueue.Clear();
        _isAnimationPlaying = false;
        Debug.Log("[MenuAutomation] Queues cleared");
    }

    public static int QueueCount => _animationQueue.Count;

    #endregion
}
