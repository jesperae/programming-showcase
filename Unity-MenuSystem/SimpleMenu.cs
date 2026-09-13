using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class SimpleMenu : CursorSelectable
{
    #region UI REFERENCES
    public TextMeshProEffects OptionsText;
    public RectTransform PointerRect;
    public GameAnimation MenuAnimator;
    #endregion

    #region SOUNDS
    public AudioClip MoveSound;
    public AudioClip SelectSound;
    public AudioClip CancelSound;
    #endregion

    #region STATE
    private List<SimpleMenuOption> _options = new List<SimpleMenuOption>();
    private List<List<SimpleMenuOption>> _subMenuStack = new List<List<SimpleMenuOption>>();
    private List<SimpleMenuOption> ActiveOptions => _subMenuStack.Count > 0 ? _subMenuStack[_subMenuStack.Count - 1] : _options;
    private int _currentChoice = 0;
    private bool _isOpen;
    private bool _inputWait = false;
    private bool _pointerInitialized = false;
    private CursorObject _ownerCursor;
    private CursorSelectable _previousSelectable;

    public bool CancelEnabled = true; //Set to false to prevent canceling out of menu
    public bool PauseGameWhileOpen = false; //When true, registers as a pause source for the duration the menu is open
    public bool ReparentUnderCursor = true; //When true, detaches from editor parent and parents under the CursorObject's parent at show time
    private int _pauseCountAtOpen; //Pause sources that already existed when we opened (parent menus, etc)

    public bool IsOpen => _isOpen;
    public bool IsAnimatingOut => !_isOpen && MenuAnimator != null && !MenuAnimator.IsFullAnimationDone;
    public bool IsBusy => _isOpen || IsAnimatingOut;
    public CursorSelectable PreviousSelectable => _previousSelectable;
    public int CurrentChoice => _currentChoice;
    public int OptionCount => ActiveOptions.Count;
    public SimpleMenuOption CurrentOption => ActiveOptions.Count > 0 ? ActiveOptions[_currentChoice] : null;
    public bool InSubMenu => _subMenuStack.Count > 0;
    private const float POINTER_SMOOTH = 0.2f;

    //DELEGATES
    public Action OnClosed;
    public Action<SimpleMenuOption> OnOptionSelected;
    #endregion

    #region SETUP
    public void Setup(List<SimpleMenuOption> options)
    {
        _options = options ?? new List<SimpleMenuOption>();
        _subMenuStack.Clear();
        _currentChoice = 0;
        UpdateDisplay();
    }

    public void Setup(List<string> optionTexts, List<Action> optionActions)
    {
        _options.Clear();
        _subMenuStack.Clear();
        _currentChoice = 0;
        int count = Mathf.Min(optionTexts.Count, optionActions.Count);
        for (int i = 0; i < count; i++)
            _options.Add(new SimpleMenuOption(optionTexts[i], optionActions[i]));
        UpdateDisplay();
    }

    public void Setup(string[] optionTexts, Action[] optionActions)
    {
        _options.Clear();
        _subMenuStack.Clear();
        _currentChoice = 0;
        int count = Mathf.Min(optionTexts.Length, optionActions.Length);
        for (int i = 0; i < count; i++)
            _options.Add(new SimpleMenuOption(optionTexts[i], optionActions[i]));
        UpdateDisplay();
    }

    public void AddOption(string text, Action action, Func<bool> enabledCondition = null)
    {
        _options.Add(new SimpleMenuOption(text, action, enabledCondition));
        UpdateDisplay();
    }

    public void AddOption(SimpleMenuOption option)
    {
        _options.Add(option);
        UpdateDisplay();
    }

    /// Adds an option nested under a named group at the root level. The group is created
    /// automatically the first time it's referenced. Selecting the group opens it as a sub-menu
    /// (with an automatic "Back" entry) instead of firing an action directly.
    public void AddSubOption(string groupText, string optionText, Action action, Func<bool> enabledCondition = null)
    {
        AddSubOption(groupText, new SimpleMenuOption(optionText, action, enabledCondition));
    }

    public void AddSubOption(string groupText, SimpleMenuOption option)
    {
        var group = _options.OfType<SimpleMenuOptionGroup>().FirstOrDefault(g => g.Text == groupText);
        if (group == null)
        {
            group = new SimpleMenuOptionGroup(groupText);
            _options.Add(group);
        }
        group.Children.Add(option);
        UpdateDisplay();
    }

    public void ClearOptions()
    {
        _options.Clear();
        _subMenuStack.Clear();
        _currentChoice = 0;
    }
    #endregion

    #region SHOW/HIDE
    public void Show(CursorObject cursor)
    {
        _ownerCursor = cursor;
        _previousSelectable = cursor?.CurrentSelectable;
        Show();
        //SELECT this menu as the cursor's current selectable
        if (_isOpen && _ownerCursor != null) _ownerCursor.Selected(this);
    }

    private void ReparentUnderCursorIfNeeded()
    {
        if (!ReparentUnderCursor) return;
        var layer = MenuEffectLayer.GetOrCreate(transform.root);
        if (layer == null || transform.parent == layer) return;

        //Detach from any editor-time parent (e.g. nested under UI panels) and move to the shared effect layer
        //so the menu renders above normal menus but below detached effect roots and the cursor.
        transform.SetParent(layer, true);
        transform.SetAsFirstSibling();

        var selectable = GetComponent<CursorSelectable>();
        if (selectable != null) selectable.SavedAnchoredPosition = ((RectTransform)transform).anchoredPosition;
    }

    public void Show()
    {
        if (_options.Count == 0) return;
        _isOpen = true;
        _inputWait = false;
        _subMenuStack.Clear();
        _currentChoice = 0;
        _pointerInitialized = false;
        ReparentUnderCursorIfNeeded();
        gameObject.SetActive(true);
        PointerRect?.gameObject.SetActive(true);
        OptionsText?.gameObject.SetActive(true); //Ensure text is visible (MenuAnimator may have disabled it)
        if (MenuAnimator != null)
        {
            //Menus must animate even while paused (a pause source may set Time.timeScale to 0).
            MenuAnimator.UseUnscaledTime = true;
            MenuAnimator.SetShowQuick(true);
        }
        UpdateDisplay();
        ActiveOptions[_currentChoice]?.OnHoverEnter?.Invoke();
        if (MenuInput.Instance != null)
        {
            MenuInput.Instance.Navigate.action.performed += HandleMove;
            MenuInput.Instance.Navigate.action.canceled += HandleMoveCanceled;
            MenuInput.Instance.Confirm.action.performed += HandleConfirm;
            if (CancelEnabled) MenuInput.Instance.Cancel.action.performed += HandleCancel;
        }
        _pauseCountAtOpen = MenuPause.PauseSourceCount; //RECORD ambient pauses (parent menus) BEFORE we add ourselves
        if (PauseGameWhileOpen) MenuPause.Pause(this);
    }

    public void Hide()
    {
        if (!_isOpen) return;
        _isOpen = false;

        if (ActiveOptions.Count > 0) ActiveOptions[_currentChoice]?.OnHoverExit?.Invoke();

        //RETURN cursor to the selectable that opened us (silently - no move sound)
        if (_ownerCursor != null && _ownerCursor.CurrentSelectable == this)
        {
            var returnTo = _previousSelectable != null && _previousSelectable.gameObject.activeSelf
                ? _previousSelectable
                : _ownerCursor.MySelectables?.FirstOrDefault(s => s != null && s != this && s.gameObject.activeSelf);
            if (returnTo != null)
            {
                _ownerCursor.SuppressNextMoveSound = true;
                _ownerCursor.Selected(returnTo);
            }
        }
        _ownerCursor = null;
        _previousSelectable = null;

        if (MenuInput.Instance != null)
        {
            MenuInput.Instance.Navigate.action.performed -= HandleMove;
            MenuInput.Instance.Navigate.action.canceled -= HandleMoveCanceled;
            MenuInput.Instance.Confirm.action.performed -= HandleConfirm;
            if (CancelEnabled) MenuInput.Instance.Cancel.action.performed -= HandleCancel;
        }
        if (PauseGameWhileOpen) MenuPause.Unpause(this);

        if (PointerRect != null) PointerRect.gameObject.SetActive(false);
        if (MenuAnimator != null)
        {
            MenuAnimator.UseUnscaledTime = true;
            MenuAnimator.DeactivateWhenHidden = true;
            MenuAnimator.SetShowQuick(false);
        }
        else gameObject.SetActive(false);

        OnClosed?.Invoke();
    }

    /// True when paused by some OTHER system (e.g. a root menu) while we are still open.
    /// Used to gate input and hide our visuals so the overlaying menu has focus.
    /// Compares against pause source count at open time so ambient parent pauses don't count as overlays.
    public bool IsBlockedByOverlay => _isOpen && MenuPause.PauseSourceCount > _pauseCountAtOpen + (PauseGameWhileOpen ? 1 : 0);
    #endregion

    #region NAVIGATION
    public void NextChoice()
    {
        var options = ActiveOptions;
        if (options.Count == 0) return;
        ChangeChoice((_currentChoice + 1) % options.Count);
        MenuAudio.Play(MoveSound);
    }

    public void PreviousChoice()
    {
        var options = ActiveOptions;
        if (options.Count == 0) return;
        ChangeChoice((_currentChoice + options.Count - 1) % options.Count);
        MenuAudio.Play(MoveSound);
    }

    private void ChangeChoice(int newIndex)
    {
        var options = ActiveOptions;
        options[_currentChoice]?.OnHoverExit?.Invoke();
        _currentChoice = newIndex;
        options[_currentChoice]?.OnHoverEnter?.Invoke();
        UpdateDisplay();
        _ownerCursor?.RefreshCurrentSelection();
    }

    public void SelectCurrentOption()
    {
        var options = ActiveOptions;
        if (options.Count == 0) return;
        var option = options[_currentChoice];
        if (!option.CanSelect) return;

        if (option is SimpleMenuOptionGroup group)
        {
            MenuAudio.Play(SelectSound);
            EnterSubMenu(group);
            return;
        }

        MenuAudio.Play(SelectSound);
        OnOptionSelected?.Invoke(option);
        option.OnSelected?.Invoke();
    }

    private void EnterSubMenu(SimpleMenuOptionGroup group)
    {
        ActiveOptions[_currentChoice]?.OnHoverExit?.Invoke();
        var childList = new List<SimpleMenuOption>(group.Children) { new SimpleMenuOption("Back", "Back", "Return to the previous menu.", ExitSubMenu) };
        _subMenuStack.Add(childList);
        _currentChoice = 0;
        UpdateDisplay();
        ActiveOptions[_currentChoice]?.OnHoverEnter?.Invoke();
        _ownerCursor?.RefreshCurrentSelection();
    }

    private void ExitSubMenu()
    {
        if (_subMenuStack.Count == 0) return;
        ActiveOptions[_currentChoice]?.OnHoverExit?.Invoke();
        _subMenuStack.RemoveAt(_subMenuStack.Count - 1);
        _currentChoice = 0;
        UpdateDisplay();
        ActiveOptions[_currentChoice]?.OnHoverEnter?.Invoke();
        _ownerCursor?.RefreshCurrentSelection();
    }
    #endregion

    #region DISPLAY
    public string GetOptionsDisplayText()
    {
        var options = ActiveOptions;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < options.Count; i++)
        {
            if (i > 0) sb.Append("\n");
            sb.Append(options[i].GetDisplayText());
        }
        return sb.ToString();
    }

    private void UpdateDisplay()
    {
        if (OptionsText != null)
            OptionsText.Display(GetOptionsDisplayText());
    }

    public Vector2 GetPointerPosition(TMP_Text choicesText, bool center = false)
    {
        if (choicesText == null || choicesText.textInfo.lineCount <= _currentChoice)
            return Vector2.zero;

        var lineInfo = choicesText.textInfo.lineInfo[_currentChoice];
        float yPos = lineInfo.baseline;
        if (center) yPos += lineInfo.lineHeight * 0.5f;
        return new Vector2(0, yPos);
    }
    #endregion

    #region INPUT HANDLING
    private void HandleMove(InputAction.CallbackContext context)
    {
        if (!_isOpen || IsBlockedByOverlay) return;
        var move = context.ReadValue<Vector2>();
        if (move.y == 0) { _inputWait = false; return; }
        if (_inputWait) return;
        _inputWait = true;
        if (move.y < 0) NextChoice();
        if (move.y > 0) PreviousChoice();
    }

    private void HandleMoveCanceled(InputAction.CallbackContext context)
    {
        _inputWait = false;
    }

    private void HandleConfirm(InputAction.CallbackContext context)
    {
        if (!_isOpen || IsBlockedByOverlay || !MenuInput.ButtonPressedDown(context)) return;
        SelectCurrentOption();
    }

    private void HandleCancel(InputAction.CallbackContext context)
    {
        if (!_isOpen || IsBlockedByOverlay || !MenuInput.ButtonPressedDown(context)) return;
        MenuAudio.Play(CancelSound);
        if (_subMenuStack.Count > 0)
            ExitSubMenu();
        else
            Hide();
    }
    #endregion

    public override string GetTitle()
    {
        var option = CurrentOption;
        if (option != null)
        {
            var t = option.GetTitle();
            if (!string.IsNullOrEmpty(t)) return t;
        }
        return base.GetTitle();
    }

    public override string GetDescription()
    {
        var option = CurrentOption;
        if (option != null)
        {
            var d = option.GetDescription();
            if (!string.IsNullOrEmpty(d)) return d;
        }
        return base.GetDescription();
    }

    public override bool ControlsTitleDescription => true;

    #region CURSOR SELECTABLE OVERRIDES
    //CursorObject selects us for visual positioning only - input is self-managed
    public override bool Click(CursorObject cursor) => false; //Don't let CursorObject handle confirm

    public override bool CalculateIsEnabled() => _isOpen;

    public override void Initialize()
    {
        if (!_isOpen && !IsBusy) //Don't deactivate while animating in/out - would kill coroutines
        {
            gameObject.SetActive(false);
            if (PointerRect != null) PointerRect.gameObject.SetActive(false);
        }
    }
    #endregion

    #region UPDATE
    public override void Update()
    {
        //When MenuAnimator handles visuals, skip the CursorSelectable alpha fade
        if (MenuAnimator == null)
        {
            if (_selected || _isOpen)
                _imageAlpha = _imageAlpha.SmoothTowards(0.9f, 0.3f);
            else
                _imageAlpha = _imageAlpha.SmoothTowards(0.4f, 0.3f);
            for (int i = 0; i < _images.Length; i++)
            {
                var clr = _images[i].color;
                clr.a = _imageAlpha;
                _images[i].color = clr;
            }
        }
        if (!_isOpen) return;

        //HIDE child visuals while another menu has paused on top of us
        bool hideForOverlay = IsBlockedByOverlay;
        if (OptionsText != null && OptionsText.gameObject.activeSelf == hideForOverlay)
            OptionsText.gameObject.SetActive(!hideForOverlay);
        if (PointerRect != null && PointerRect.gameObject.activeSelf == hideForOverlay)
            PointerRect.gameObject.SetActive(!hideForOverlay);

        //REFRESH option text each frame so live data (e.g. cooldown counters) updates
        UpdateDisplay();

        if (hideForOverlay) return;

        //SMOOTH POINTER
        if (PointerRect != null && OptionsText != null && OptionsText.TmpText != null)
        {
            var targetPos = GetPointerPosition(OptionsText.TmpText, true);
            if (!_pointerInitialized)
            {
                PointerRect.anchoredPosition = targetPos;
                _pointerInitialized = true;
            }
            else
                PointerRect.anchoredPosition = PointerRect.anchoredPosition.SmoothTowards(targetPos, POINTER_SMOOTH);
        }
    }

    void OnEnable()
    {
        if (!_isOpen && MenuAnimator == null)
        {
            gameObject.SetActive(false);
            if (PointerRect != null) PointerRect.gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        if (_isOpen)
        {
            if (MenuInput.Instance != null)
            {
                MenuInput.Instance.Navigate.action.performed -= HandleMove;
                MenuInput.Instance.Navigate.action.canceled -= HandleMoveCanceled;
                MenuInput.Instance.Confirm.action.performed -= HandleConfirm;
                if (CancelEnabled) MenuInput.Instance.Cancel.action.performed -= HandleCancel;
            }
            if (PauseGameWhileOpen) MenuPause.Unpause(this); //SAFETY - release pause if disabled while open
        }
        _isOpen = false;
        _ownerCursor = null;
    }

    protected override void ApplyPositionOffset()
    {
        //When MenuAnimator handles show/hide, it controls the rect's position via MoveCoroutine.
        //Skip the base class position override so the animation isn't overwritten every LateUpdate.
        if (MenuAnimator != null) return;
        base.ApplyPositionOffset();
    }
    #endregion
}
