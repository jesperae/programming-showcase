using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class Menu : MonoBehaviour
{
    public static float CURSOR_SMOOTH = 0.25f;
    public static Menu CurrentOpen { get; private set; }

    /// Close any open menu and open this one (used by MenuAutomation; opens standalone without parent simulation)
    public void Open()
    {
        var previouslyOpen = CurrentOpen;
        if (previouslyOpen != null && previouslyOpen != this && previouslyOpen.IsActive)
        {
            previouslyOpen.DeactivateMenu(); //NOTE: nulls the static CurrentOpen if previouslyOpen was it - use the cached local below, not CurrentOpen
            previouslyOpen.WentIntoSubMenu = false; // clear stale submenu state so its PressToOpen still works later
        }
        if (IsActive) return;
        PreviousMenu = null; // automation opens have no parent chain, so back/return closes the menu
        ActivateMenu();
    }

    [HideInInspector] public bool IsActive;
    public bool IsOpened => WentIntoSubMenu || IsActive; //If has been opened and submenus have opened
    public InputActionReference PressToOpen;
    public bool StartOpen;
    public bool PauseGame;
    public bool DontLoadLastCursorPosition;
    public RectTransform[] ExtraElements;
    [HideInInspector] public CursorObject MyCursor;
    [HideInInspector] public RectTransform RectTransform;
    [HideInInspector] public Menu PreviousMenu;
    private bool _specialClosePreviousMenuWhenGoingBack;
    [HideInInspector] public bool WentIntoSubMenu;
    [HideInInspector] public CursorSelectable SavedCursorSelectable;
    [Header("Animation")]
    public GameAnimation OpenCloseAnimation;
    public bool SubMenuClosesParent = true;
    private MenuOption[] _menuOptions;
    public MenuOption SelectedMenuOption => _menuOptions.Where(mo => mo.Selected).FirstOrDefault();

    /// Fired at the end of ActivateMenu - hook for refreshing the data the menu displays.
    public Action OnActivated;

    public virtual void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        MyCursor = transform.parent.GetComponentInChildren<CursorObject>(true);
        _menuOptions = GetComponentsInChildren<MenuOption>(true);
        if (PressToOpen) PressToOpen.action.performed += ToggleOpen;
    }

    private void OnDestroy()
    {
        if (PressToOpen) PressToOpen.action.performed -= ToggleOpen;
        if (CurrentOpen == this) CurrentOpen = null;
    }

    public virtual void Start()
    {
        gameObject.SetActive(IsActive);
        foreach (var element in ExtraElements) element.gameObject.SetActive(IsActive);
        if (StartOpen) ActivateMenu();
    }

    #region Controls
    void ToggleOpen(InputAction.CallbackContext context)
    {
        if (MyCursor == null) return;
        if (WentIntoSubMenu)
        {
            // If a submenu of this menu is currently open, let the toggle button close it.
            // Don't allow this while the cursor is locked - the cursor is the authority for menu control.
            if (MyCursor != null && !MyCursor.CanMove) return;
            if (CurrentOpen != null && CurrentOpen != this && CurrentOpen.PreviousMenu == this)
                CurrentOpen.GoBack();
            return;
        }
        if (MenuAutomation.IsAnimationPlaying && MyCursor.MovementDisabled) return;
        if (MenuAutomation.IsAnimationPlaying && !IsActive) return;
        if (MenuAutomation.QueueCount > 0 && !IsActive) return;
        if (MyCursor.MovementDisabled) return;
        if (!IsActive && CurrentOpen != null && CurrentOpen != this) return; // don't open if another menu is already open
        if (!IsActive) ActivateMenu();
        else DeactivateMenu();
    }
    #endregion

    public void UpdateMenuItems()
    {

    }

    /// Re-grabs MenuOption components. Call after dynamically adding or removing options.
    public void RefreshOptions()
    {
        _menuOptions = GetComponentsInChildren<MenuOption>(true);
    }

    void Update()
    {

    }

    public void GoBack()
    {
        if (PreviousMenu != null)
        {
            MenuAudio.Play(MenuAudio.Cancel);
            DeactivateMenu();
            PreviousMenu.ActivateMenu();
            if (PauseGame) MenuPause.Pause();
            if (_specialClosePreviousMenuWhenGoingBack)
            {
                PreviousMenu.DeactivateMenu();
                _specialClosePreviousMenuWhenGoingBack = false;
            }
        }
        else
        {
            MenuAudio.Play(MenuAudio.Cancel);
            DeactivateMenu();
            MenuPause.Unpause(); // automation-opened menus may not have PauseGame but were paused by MenuAutomation
        }
    }

    public virtual void ActivateMenu()
    {
        CurrentOpen = this;
        IsActive = true;
        gameObject.SetActive(IsActive);
        WentIntoSubMenu = false;
        foreach (var element in ExtraElements) element.gameObject.SetActive(true);
        // Show animation FIRST so any synchronous Enable steps fire before cursor sets text,
        // preventing OnEnable/ResetForReuse from wiping text that was just set.
        if (OpenCloseAnimation != null)
        {
            OpenCloseAnimation.ActivateAllTargets();
            OpenCloseAnimation.SetShowQuick(true);
        }
        Canvas.ForceUpdateCanvases(); // rebuild TMP layouts so text/cursor sizing is valid immediately
        foreach (var option in _menuOptions) option.Initialize();

        //PREPARE visibility first so reveal groups hide/reveal nodes BEFORE cursor picks one
        var revealGroups = GetComponentsInChildren<IMenuRevealGroup>(true);
        foreach (var group in revealGroups) group.PrepareVisibility();

        //CURSOR - activate before ResetPosition so HandleSelected can fully run
        MyCursor.MyMenu = this;
        MyCursor.gameObject.SetActive(true);
        if (!DontLoadLastCursorPosition && SavedCursorSelectable != null && SavedCursorSelectable.gameObject.activeSelf)
            MyCursor.ResetPosition(SavedCursorSelectable);
        else
            MyCursor.ResetPosition();

        if (PauseGame) MenuPause.Pause();

        //NOTIFY listeners so they can refresh displayed data
        OnActivated?.Invoke();

        //ANIMATE reveal only when not driven by MenuAutomation (it calls CheckForRevealing itself after onPlop).
        if (!MenuAutomation.IsAnimationPlaying)
            foreach (var group in revealGroups) group.CheckForRevealing();
    }

    public virtual void DeactivateMenu()
    {
        if (CurrentOpen == this) CurrentOpen = null;
        MyCursor.gameObject.SetActive(false);
        IsActive = false;
        var current = MyCursor.CurrentSelectable;
        SavedCursorSelectable = (current is SimpleMenu sm) ? sm.PreviousSelectable : current;
        if (!WentIntoSubMenu && PauseGame)
        {
            MenuPause.Unpause();
        }
        foreach (var element in ExtraElements) element.gameObject.SetActive(false);

        if (WentIntoSubMenu)
        {
            if (!SubMenuClosesParent)
                return; // keep parent visible — submenu sits on top

            // NOT actually closed, just covered by the submenu — hide instantly, don't play the close animation/sound
            gameObject.SetActive(false);
            return;
        }

        //  MAY NEED REFACTOR - PauseMenu uses this to hide all of the objects so we cannot do it this way, just disable itself if sub-menu
        if (OpenCloseAnimation != null)
            OpenCloseAnimation.SetShowQuick(false);
        else
            gameObject.SetActive(false);
    }

    public bool IsOpen()
    {
        return IsActive;
    }


    /// Activates this menu AND the parent controllable menu (with PressToOpen)
    /// Simulates opening the parent menu and navigating into this submenu

    public void ActivateMenuAutomatically()
    {
        //FIND CONTROLLABLE MENU (sibling with PressToOpen)
        if (transform.parent != null)
        {
            Menu[] siblingMenus = transform.parent.GetComponentsInChildren<Menu>(true);
            foreach (var menu in siblingMenus)
            {
                if (menu.PressToOpen != null)
                {
                    //OPEN THE CONTROLLABLE MENU
                    menu.ActivateMenu();
                    menu.WentIntoSubMenu = true; // dont unpause
                    menu.DeactivateMenu();
                    menu.WentIntoSubMenu = false; // reset after simulated parent close
                    PreviousMenu = menu;
                    _specialClosePreviousMenuWhenGoingBack = true;
                    foreach (var element in menu.ExtraElements) element.gameObject.SetActive(true);
                    break;
                }
            }
        }

        //OPEN THIS MENU
        ActivateMenu();
    }
}